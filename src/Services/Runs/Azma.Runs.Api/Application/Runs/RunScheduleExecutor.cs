using Azma.EventBus.Contracts.Runs;
using Azma.Runs.Api.Domain;
using Azma.Runs.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Azma.Runs.Api.Application.Runs;

public sealed class RunScheduleExecutor
{
    private readonly RunsDbContext _db;
    private readonly AutomaticRunCaseExecutor _caseExecutor;
    private readonly IPublishEndpoint _bus;
    private readonly ILogger<RunScheduleExecutor> _logger;

    public RunScheduleExecutor(
        RunsDbContext db,
        AutomaticRunCaseExecutor caseExecutor,
        IPublishEndpoint bus,
        ILogger<RunScheduleExecutor> logger)
    {
        _db = db;
        _caseExecutor = caseExecutor;
        _bus = bus;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid scheduleId, string trigger, CancellationToken ct)
    {
        var schedule = await _db.RunSchedules
            .Include(x => x.Cases)
            .FirstOrDefaultAsync(x => x.Id == scheduleId && x.DeletedAtUtc == null, ct);

        if (schedule is null)
            return;

        var execution = RunScheduleExecution.Start(schedule.Id, trigger, schedule.Cases.Count);
        _db.RunScheduleExecutions.Add(execution);
        await _db.SaveChangesAsync(ct);
        TestRun? run = null;

        try
        {
            if (schedule.Cases.Count == 0)
                throw new InvalidOperationException("Schedule has no test cases.");

            run = await CreateRunAsync(schedule, ct);
            execution.AttachRun(run.Id);
            await _db.SaveChangesAsync(ct);

            await _bus.Publish(new TestRunCreated(
                run.WorkspaceId, run.ProjectId, run.Id, run.Name, run.Cases.Count), ct);

            var passed = 0;
            var failed = 0;
            foreach (var runCase in run.Cases)
            {
                var status = await ExecuteCaseAsync(schedule, run, runCase, ct);
                if (status == ResultStatus.Passed) passed++;
                else if (status == ResultStatus.Failed)
                {
                    failed++;
                }
            }

            if (failed == 0)
                run.Close(schedule.CreatedByUserId);

            var counts = RunMapping.CountOf(run.Cases);
            schedule.MarkExecuted(run.Id, DateTime.UtcNow);
            schedule.ReleaseClaim();
            schedule.SetNextRun(NextRun(schedule));
            execution.Complete(passed, failed);
            await _db.SaveChangesAsync(ct);

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
            _logger.LogError(ex, "Scheduled run {ScheduleId} failed.", schedule.Id);
            schedule.ReleaseClaim();
            schedule.SetNextRun(NextRun(schedule));
            execution.Fail(ex.Message);
            await _db.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task<TestRun> CreateRunAsync(RunSchedule schedule, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        await RunScheduleAutomationSynchronizer.RefreshBeforeRunAsync(_db, schedule, ct);
        // Bulk synchronization does not update the already tracked schedule.Cases.
        // Take a fresh, fixed snapshot once so publication during execution cannot
        // replace the definition midway through this run.
        var cases = await _db.RunScheduleCases.AsNoTracking()
            .Where(c => c.RunScheduleId == schedule.Id)
            .OrderBy(c => c.TestCaseCode)
            .ToListAsync(ct);

        var projectRef = await _db.ProjectRefs
            .FromSqlInterpolated($"""
                SELECT * FROM runs.project_refs
                WHERE "Id" = {schedule.ProjectId} AND "WorkspaceId" = {schedule.WorkspaceId}
                FOR UPDATE
                """)
            .AsTracking()
            .FirstOrDefaultAsync(ct);

        if (projectRef is null)
        {
            await ProjectRefSynchronizer.EnsureFromProjectsAsync(_db, schedule.WorkspaceId, schedule.ProjectId, ct);

            projectRef = await _db.ProjectRefs
                .FromSqlInterpolated($"""
                    SELECT * FROM runs.project_refs
                    WHERE "Id" = {schedule.ProjectId} AND "WorkspaceId" = {schedule.WorkspaceId}
                    FOR UPDATE
                    """)
                .AsTracking()
                .FirstOrDefaultAsync(ct);
        }

        if (projectRef is null)
            throw new InvalidOperationException("Project reference is missing for this schedule.");
        if (projectRef.IsArchived)
            throw new InvalidOperationException("Project is archived.");

        var seq = projectRef.AllocateRunSequence();
        var code = $"{projectRef.Code}-R-{seq:D3}";
        var name = $"{schedule.Name} - {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
        var run = TestRun.Create(
            schedule.WorkspaceId,
            schedule.ProjectId,
            null,
            code,
            seq,
            name,
            schedule.Description,
            schedule.CreatedByUserId,
            schedule.Environment);

        foreach (var c in cases)
        {
            run.Cases.Add(RunCase.Snapshot(
                run.Id,
                c.TestCaseId,
                c.TestCaseCode,
                c.TestCaseTitle,
                c.TestCaseVersion,
                null,
                c.AutomationScenarioPath,
                c.AutomationEngine,
                c.AutomationStatus,
                c.AutomationDefinitionVersion,
                c.AutomationRecordingJson));
        }

        _db.Runs.Add(run);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return run;
    }

    private async Task<ResultStatus> ExecuteCaseAsync(
        RunSchedule schedule,
        TestRun run,
        RunCase runCase,
        CancellationToken ct)
    {
        var outcome = await _caseExecutor.ExecuteAsync(
            run,
            runCase,
            schedule.CreatedByUserId,
            new AutomaticRunCaseExecutionOptions(
                Gateway: schedule.Gateway,
                Frontend: schedule.Frontend,
                CommentPrefix: "Scheduled automatic execution",
                RecordFailures: true),
            ct);

        if (outcome.IsFailure)
            throw new InvalidOperationException(outcome.Error.Message);

        return outcome.Value.Result.Status;
    }

    private DateTime? NextRun(RunSchedule schedule) =>
        RunScheduleCalculator.NextRunUtc(
            schedule.RecurrenceType,
            schedule.TimeOfDayMinutes,
            schedule.WeekDaysMask,
            schedule.TimeZoneId,
            schedule.StartAtUtc,
            schedule.EndAtUtc,
            DateTime.UtcNow.AddSeconds(1));
}
