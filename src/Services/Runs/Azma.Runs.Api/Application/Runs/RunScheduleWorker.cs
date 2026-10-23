using Azma.Runs.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Azma.Runs.Api.Application.Runs;

public sealed class RunScheduleWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RunScheduleSignal _signal;
    private readonly ILogger<RunScheduleWorker> _logger;

    public RunScheduleWorker(
        IServiceScopeFactory scopeFactory,
        RunScheduleSignal signal,
        ILogger<RunScheduleWorker> logger)
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
                await ClaimAndExecuteDueSchedulesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Run schedule worker tick failed.");
            }

            await _signal.WaitAsync(PollInterval, stoppingToken);
        }
    }

    private async Task ClaimAndExecuteDueSchedulesAsync(CancellationToken ct)
    {
        List<Guid> scheduleIds;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RunsDbContext>();
            var now = DateTime.UtcNow;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var due = await db.RunSchedules
                .FromSqlInterpolated($"""
                    SELECT * FROM runs."run_schedules"
                    WHERE "IsActive" = true
                      AND "DeletedAtUtc" IS NULL
                      AND "NextRunAtUtc" IS NOT NULL
                      AND "NextRunAtUtc" <= {now}
                      AND ("LockedUntilUtc" IS NULL OR "LockedUntilUtc" < {now})
                    ORDER BY "NextRunAtUtc"
                    LIMIT 5
                    FOR UPDATE SKIP LOCKED
                    """)
                .AsTracking()
                .ToListAsync(ct);

            foreach (var schedule in due)
                schedule.ClaimUntil(now.AddMinutes(30));

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            scheduleIds = due.Select(x => x.Id).ToList();
        }

        foreach (var scheduleId in scheduleIds)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<RunScheduleExecutor>();
            await executor.ExecuteAsync(scheduleId, "scheduled", ct);
        }
    }
}
