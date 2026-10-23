using Azma.EventBus.Contracts.Runs;
using Azma.Runs.Api.Domain;
using Azma.Runs.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Azma.Runs.Api.Application.Runs;

public sealed class RunAutomationWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RunAutomationSignal _signal;
    private readonly ILogger<RunAutomationWorker> _logger;

    public RunAutomationWorker(
        IServiceScopeFactory scopeFactory,
        RunAutomationSignal signal,
        ILogger<RunAutomationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _signal = signal;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ClaimAndExecuteQueuedRunsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Run automation worker tick failed.");
            }

            await _signal.WaitAsync(PollInterval, stoppingToken);
        }
    }

    private async Task ClaimAndExecuteQueuedRunsAsync(CancellationToken ct)
    {
        List<Guid> executionIds;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var queued = await db.RunAutomationExecutions
                .FromSqlRaw("""
                    SELECT * FROM runs."run_automation_executions"
                    WHERE "Status" = 0
                    ORDER BY "StartedAtUtc"
                    LIMIT 3
                    FOR UPDATE SKIP LOCKED
                    """)
                .AsTracking()
                .ToListAsync(ct);

            foreach (var execution in queued)
                execution.Start();

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            executionIds = queued.Select(x => x.Id).ToList();
        }

        foreach (var executionId in executionIds)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<RunAutomationExecutor>();
            await executor.ExecuteAsync(executionId, ct);
        }
    }
}

public sealed class RunAutomationExecutor
{
    private readonly RunsDbContext _db;
    private readonly AutomaticRunCaseExecutor _caseExecutor;
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<RunAutomationExecutor> _logger;

    public RunAutomationExecutor(
        RunsDbContext db,
        AutomaticRunCaseExecutor caseExecutor,
        IPublishEndpoint bus,
        ILogger<RunAutomationExecutor> logger)
    {
        _db = db;
        _caseExecutor = caseExecutor;
        _bus = bus;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid executionId, CancellationToken ct)
    {
        var execution = await _db.RunAutomationExecutions
            .FirstOrDefaultAsync(x => x.Id == executionId, ct);
        if (execution is null)
            return;

        var run = await _db.Runs
            .Include(x => x.Cases)
            .FirstOrDefaultAsync(x => x.Id == execution.RunId, ct);

        try
        {
            if (run is null)
                throw new InvalidOperationException("Run was not found for automatic execution.");
            if (run.IsDeleted)
                throw new InvalidOperationException("Run is deleted.");
            if (run.Status == RunStatus.Closed)
                throw new InvalidOperationException("Run is already closed.");
            if (run.Cases.Count == 0)
                throw new InvalidOperationException("Run has no test cases.");
            if (run.Cases.Any(x => !x.HasAutomation))
                throw new InvalidOperationException("Run contains a test case without automation.");

            var passed = 0;
            var failed = 0;
            foreach (var runCase in run.Cases.OrderBy(x => x.TestCaseCode))
            {
                var outcome = await _caseExecutor.ExecuteAsync(
                    run,
                    runCase,
                    run.CreatedByUserId,
                    new AutomaticRunCaseExecutionOptions(
                        CommentPrefix: "Run automatic execution",
                        RecordFailures: true),
                    ct);

                if (outcome.IsFailure)
                    throw new InvalidOperationException(outcome.Error.Message);

                if (outcome.Value.Result.Status == ResultStatus.Passed) passed++;
                else if (outcome.Value.Result.Status == ResultStatus.Failed) failed++;
            }

            execution.Complete(passed, failed);
            await _db.SaveChangesAsync(ct);

            var counts = RunMapping.CountOf(run.Cases);
            await _bus.Publish(new TestRunCompleted(
                run.WorkspaceId, run.ProjectId, run.Id,
                counts.Passed, counts.Failed, counts.Blocked, counts.Skipped), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automatic run execution {ExecutionId} failed.", execution.Id);
            execution.Fail(ex.Message);
            await _db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
