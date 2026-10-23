using Azma.Auth.ProjectAccess;
using Azma.Common.Pagination;
using Azma.Common.Results;
using Azma.Runs.Api.Application.Common;
using Azma.Runs.Api.Domain;
using Azma.Runs.Api.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Runs.Api.Application.Runs;

internal static class RunScheduleMapping
{
    public static RunScheduleDto Map(RunSchedule s) => new(
        s.Id, s.ProjectId, s.Name, s.Description, s.Environment,
        s.RecurrenceType, s.TimeOfDayMinutes, s.WeekDaysMask, s.TimeZoneId,
        s.StartAtUtc, s.EndAtUtc, s.IsActive, s.NextRunAtUtc,
        s.LastRunAtUtc, s.LastRunId, s.Cases.Count,
        s.Loops, s.Threads, s.Rampup, s.Skippable, s.Gateway, s.Frontend,
        s.CreatedByUserId, s.CreatedAtUtc, s.UpdatedAtUtc);

    public static RunScheduleCaseDto MapCase(RunScheduleCase c) => new(
        c.Id, c.TestCaseId, c.TestCaseCode, c.TestCaseTitle, c.TestCaseVersion,
        c.AutomationScenarioPath, c.AutomationEngine, c.AutomationStatus, c.AutomationDefinitionVersion);

    public static RunScheduleExecutionDto MapExecution(RunScheduleExecution e) => new(
        e.Id, e.RunScheduleId, e.RunId, e.Status, e.Trigger, e.StartedAtUtc,
        e.CompletedAtUtc, e.TotalCases, e.Passed, e.Failed, e.ErrorMessage);
}

internal static class RunScheduleRecurrencePolicy
{
    public static bool IsValid(ScheduleRecurrenceType recurrenceType, int weekDaysMask) =>
        recurrenceType == ScheduleRecurrenceType.Daily ||
        (recurrenceType == ScheduleRecurrenceType.Weekly && weekDaysMask != 0) ||
        RunScheduleCalculator.TryGetIntervalMinutes(recurrenceType, 0, out _);
}

public sealed record ListRunSchedulesQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid? ProjectId,
    bool IncludeInactive,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<RunScheduleDto>>>;

public sealed class ListRunSchedulesHandler : IRequestHandler<ListRunSchedulesQuery, Result<PagedResult<RunScheduleDto>>>
{
    private readonly RunsDbContext _db;
    public ListRunSchedulesHandler(RunsDbContext db) => _db = db;

    public async Task<Result<PagedResult<RunScheduleDto>>> Handle(ListRunSchedulesQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);
        var query = _db.RunSchedules.AsNoTracking()
            .Include(x => x.Cases)
            .Where(x => x.WorkspaceId == q.WorkspaceId && x.DeletedAtUtc == null);

        var visibility = await ProjectAccessPolicy.GetVisibilityAsync(
            _db.Database.GetDbConnection(), q.WorkspaceId, q.CurrentUserId, q.CurrentUserAccessScope, q.CurrentUserRole, ct);
        var allowed = visibility.AllowedArray();
        var denied = visibility.DeniedArray();
        if (allowed is not null)
            query = query.Where(x => allowed.Contains(x.ProjectId));
        if (denied.Length > 0)
            query = query.Where(x => !denied.Contains(x.ProjectId));

        if (q.ProjectId is { } projectId) query = query.Where(x => x.ProjectId == projectId);
        if (!q.IncludeInactive) query = query.Where(x => x.IsActive);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.NextRunAtUtc == null)
            .ThenBy(x => x.NextRunAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return new PagedResult<RunScheduleDto>(items.Select(RunScheduleMapping.Map).ToList(), page, size, total);
    }
}

public sealed record GetRunScheduleQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ScheduleId) : IRequest<Result<RunScheduleDetailDto>>;

public sealed class GetRunScheduleHandler : IRequestHandler<GetRunScheduleQuery, Result<RunScheduleDetailDto>>
{
    private readonly RunsDbContext _db;
    public GetRunScheduleHandler(RunsDbContext db) => _db = db;

    public async Task<Result<RunScheduleDetailDto>> Handle(GetRunScheduleQuery q, CancellationToken ct)
    {
        var schedule = await _db.RunSchedules.AsNoTracking()
            .Include(x => x.Cases)
            .FirstOrDefaultAsync(x => x.Id == q.ScheduleId && x.WorkspaceId == q.WorkspaceId && x.DeletedAtUtc == null, ct);
        if (schedule is null) return Result.Failure<RunScheduleDetailDto>(RunErrors.ScheduleNotFound);
        if (!await CanAccessAsync(q.WorkspaceId, q.CurrentUserId, q.CurrentUserAccessScope, schedule.ProjectId, ct))
            return Result.Failure<RunScheduleDetailDto>(RunErrors.ProjectAccessDenied);

        return new RunScheduleDetailDto(
            RunScheduleMapping.Map(schedule),
            schedule.Cases.OrderBy(x => x.TestCaseCode).Select(RunScheduleMapping.MapCase).ToList());
    }

    private Task<bool> CanAccessAsync(
        Guid workspaceId,
        Guid userId,
        string accessScope,
        Guid projectId,
        CancellationToken ct,
        string? workspaceRole = null,
        ProjectCapability required = ProjectCapability.View) =>
        ProjectAccessPolicy.CanAccessProjectAsync(
            _db.Database.GetDbConnection(), workspaceId, userId, accessScope, projectId, ct, workspaceRole, required);

}

public sealed record CreateRunScheduleCommand(
    Guid WorkspaceId,
    Guid CreatedByUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ProjectId,
    string Name,
    string? Description,
    string? Environment,
    ScheduleRecurrenceType RecurrenceType,
    int TimeOfDayMinutes,
    int WeekDaysMask,
    string? TimeZoneId,
    DateTime? StartAtUtc,
    DateTime? EndAtUtc,
    IReadOnlyList<Guid> TestCaseIds,
    int? Loops,
    int? Threads,
    int? Rampup,
    bool? Skippable,
    string? Gateway,
    string? Frontend) : IRequest<Result<RunScheduleDetailDto>>;

public sealed class CreateRunScheduleValidator : AbstractValidator<CreateRunScheduleCommand>
{
    public CreateRunScheduleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Environment).MaximumLength(300);
        RuleFor(x => x.TimeOfDayMinutes).InclusiveBetween(0, 1439);
        RuleFor(x => x.TestCaseIds).NotNull().Must(x => x.Count > 0);
        RuleFor(x => x.Gateway).MaximumLength(1000);
        RuleFor(x => x.Frontend).MaximumLength(1000);
    }
}

public sealed class CreateRunScheduleHandler : IRequestHandler<CreateRunScheduleCommand, Result<RunScheduleDetailDto>>
{
    private readonly RunsDbContext _db;
    public CreateRunScheduleHandler(RunsDbContext db) => _db = db;

    public async Task<Result<RunScheduleDetailDto>> Handle(CreateRunScheduleCommand cmd, CancellationToken ct)
    {
        if (!RunScheduleRecurrencePolicy.IsValid(cmd.RecurrenceType, cmd.WeekDaysMask))
            return Result.Failure<RunScheduleDetailDto>(RunErrors.ScheduleInvalidRecurrence);
        if (!await CanAccessAsync(cmd.WorkspaceId, cmd.CreatedByUserId, cmd.CurrentUserAccessScope, cmd.ProjectId, ct, cmd.CurrentUserRole, ProjectCapability.Contribute))
            return Result.Failure<RunScheduleDetailDto>(RunErrors.ProjectAccessDenied);

        var refs = await LoadAutomatedRefsAsync(cmd.WorkspaceId, cmd.ProjectId, cmd.TestCaseIds, ct);
        if (refs.Count != cmd.TestCaseIds.Distinct().Count())
            return Result.Failure<RunScheduleDetailDto>(RunErrors.ScheduleHasNoAutomaticCases);
        if (refs.Any(x => x.ProjectId != cmd.ProjectId))
            return Result.Failure<RunScheduleDetailDto>(RunErrors.TestCaseProjectMismatch);

        var now = DateTime.UtcNow;
        var schedule = RunSchedule.Create(
            cmd.WorkspaceId, cmd.ProjectId, cmd.Name, cmd.Description, cmd.Environment,
            cmd.RecurrenceType, cmd.TimeOfDayMinutes, cmd.WeekDaysMask, cmd.TimeZoneId,
            cmd.StartAtUtc, cmd.EndAtUtc, cmd.CreatedByUserId,
            cmd.Loops ?? 1, cmd.Threads ?? 1, cmd.Rampup ?? 0, cmd.Skippable ?? false,
            cmd.Gateway, cmd.Frontend);

        schedule.SetNextRun(RunScheduleCalculator.NextRunUtc(
            schedule.RecurrenceType, schedule.TimeOfDayMinutes, schedule.WeekDaysMask,
            schedule.TimeZoneId, schedule.StartAtUtc, schedule.EndAtUtc, now));

        var byId = refs.ToDictionary(x => x.Id);
        foreach (var id in cmd.TestCaseIds.Distinct())
        {
            var r = byId[id];
            schedule.Cases.Add(RunScheduleCase.Snapshot(
                schedule.Id, r.Id, r.Code, r.Title, r.Version, r.AutomationScenarioPath,
                r.AutomationEngine, r.AutomationStatus,
                r.AutomationDefinitionVersion, r.AutomationRecordingJson));
        }

        _db.RunSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);
        return new RunScheduleDetailDto(
            RunScheduleMapping.Map(schedule),
            schedule.Cases.Select(RunScheduleMapping.MapCase).ToList());
    }

    private async Task<List<TestCaseRef>> LoadAutomatedRefsAsync(
        Guid workspaceId,
        Guid projectId,
        IReadOnlyList<Guid> ids,
        CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        var distinctArray = distinct.ToArray();
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO runs."testcase_refs" ("Id", "WorkspaceId", "ProjectId", "Code", "Title", "Version", "AutomationScenarioPath", "AutomationEngine", "AutomationStatus", "AutomationDefinitionVersion", "AutomationRecordingJson", "IsDeleted")
            SELECT "Id", "WorkspaceId", "ProjectId", "Code", "Title", "Version", "AutomationScenarioPath", "AutomationEngine", "AutomationStatus", "AutomationDefinitionVersion", "AutomationRecordingJson", false
            FROM testcases."testcases"
            WHERE "Id" = ANY({distinctArray}) AND "WorkspaceId" = {workspaceId} AND "ProjectId" = {projectId} AND "DeletedAtUtc" IS NULL
            ON CONFLICT ("Id") DO UPDATE SET
                "WorkspaceId" = EXCLUDED."WorkspaceId",
                "ProjectId" = EXCLUDED."ProjectId",
                "Code" = EXCLUDED."Code",
                "Title" = EXCLUDED."Title",
                "Version" = EXCLUDED."Version",
                "AutomationScenarioPath" = EXCLUDED."AutomationScenarioPath",
                "AutomationEngine" = EXCLUDED."AutomationEngine",
                "AutomationStatus" = EXCLUDED."AutomationStatus",
                "AutomationDefinitionVersion" = EXCLUDED."AutomationDefinitionVersion",
                "AutomationRecordingJson" = EXCLUDED."AutomationRecordingJson",
                "IsDeleted" = false
            """, ct);

        var refs = await _db.TestCaseRefs.AsNoTracking()
            .Where(x => x.WorkspaceId == workspaceId && x.ProjectId == projectId && distinct.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(ct);

        return refs.Where(x => x.HasAutomation).ToList();
    }

    private Task<bool> CanAccessAsync(
        Guid workspaceId,
        Guid userId,
        string accessScope,
        Guid projectId,
        CancellationToken ct,
        string? workspaceRole = null,
        ProjectCapability required = ProjectCapability.View) =>
        ProjectAccessPolicy.CanAccessProjectAsync(
            _db.Database.GetDbConnection(), workspaceId, userId, accessScope, projectId, ct, workspaceRole, required);

}

public sealed record UpdateRunScheduleCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ScheduleId,
    string? Name,
    string? Description,
    string? Environment,
    ScheduleRecurrenceType? RecurrenceType,
    int? TimeOfDayMinutes,
    int? WeekDaysMask,
    string? TimeZoneId,
    DateTime? StartAtUtc,
    DateTime? EndAtUtc,
    IReadOnlyList<Guid>? TestCaseIds,
    int? Loops,
    int? Threads,
    int? Rampup,
    bool? Skippable,
    string? Gateway,
    string? Frontend) : IRequest<Result<RunScheduleDetailDto>>;

public sealed class UpdateRunScheduleHandler : IRequestHandler<UpdateRunScheduleCommand, Result<RunScheduleDetailDto>>
{
    private readonly RunsDbContext _db;
    public UpdateRunScheduleHandler(RunsDbContext db) => _db = db;

    public async Task<Result<RunScheduleDetailDto>> Handle(UpdateRunScheduleCommand cmd, CancellationToken ct)
    {
        var schedule = await _db.RunSchedules
            .Include(x => x.Cases)
            .FirstOrDefaultAsync(x => x.Id == cmd.ScheduleId && x.WorkspaceId == cmd.WorkspaceId && x.DeletedAtUtc == null, ct);
        if (schedule is null) return Result.Failure<RunScheduleDetailDto>(RunErrors.ScheduleNotFound);
        if (!await CanAccessAsync(cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, schedule.ProjectId, ct, cmd.CurrentUserRole, ProjectCapability.Contribute))
            return Result.Failure<RunScheduleDetailDto>(RunErrors.ProjectAccessDenied);

        var nextRecurrence = cmd.RecurrenceType ?? schedule.RecurrenceType;
        var nextMask = cmd.WeekDaysMask ?? schedule.WeekDaysMask;
        if (nextRecurrence == ScheduleRecurrenceType.Weekly && nextMask == 0)
            return Result.Failure<RunScheduleDetailDto>(RunErrors.ScheduleInvalidRecurrence);
        if (!RunScheduleRecurrencePolicy.IsValid(nextRecurrence, nextMask))
            return Result.Failure<RunScheduleDetailDto>(RunErrors.ScheduleInvalidRecurrence);

        if (cmd.TestCaseIds is not null)
        {
            if (cmd.TestCaseIds.Count == 0)
                return Result.Failure<RunScheduleDetailDto>(RunErrors.ScheduleHasNoAutomaticCases);

            var refs = await LoadAutomatedRefsAsync(cmd.WorkspaceId, schedule.ProjectId, cmd.TestCaseIds, ct);
            if (refs.Count != cmd.TestCaseIds.Distinct().Count())
                return Result.Failure<RunScheduleDetailDto>(RunErrors.ScheduleHasNoAutomaticCases);

            _db.RunScheduleCases.RemoveRange(schedule.Cases);
            schedule.Cases.Clear();
            var byId = refs.ToDictionary(x => x.Id);
            foreach (var id in cmd.TestCaseIds.Distinct())
            {
                var r = byId[id];
                schedule.Cases.Add(RunScheduleCase.Snapshot(
                    schedule.Id, r.Id, r.Code, r.Title, r.Version, r.AutomationScenarioPath,
                    r.AutomationEngine, r.AutomationStatus,
                    r.AutomationDefinitionVersion, r.AutomationRecordingJson));
            }
        }

        schedule.Update(
            cmd.Name, cmd.Description, cmd.Environment, cmd.RecurrenceType,
            cmd.TimeOfDayMinutes, cmd.WeekDaysMask, cmd.TimeZoneId,
            cmd.StartAtUtc, cmd.EndAtUtc, cmd.Loops, cmd.Threads,
            cmd.Rampup, cmd.Skippable, cmd.Gateway, cmd.Frontend);

        schedule.SetNextRun(schedule.IsActive
            ? RunScheduleCalculator.NextRunUtc(
                schedule.RecurrenceType, schedule.TimeOfDayMinutes, schedule.WeekDaysMask,
                schedule.TimeZoneId, schedule.StartAtUtc, schedule.EndAtUtc, DateTime.UtcNow)
            : schedule.NextRunAtUtc);

        await _db.SaveChangesAsync(ct);
        return new RunScheduleDetailDto(
            RunScheduleMapping.Map(schedule),
            schedule.Cases.Select(RunScheduleMapping.MapCase).ToList());
    }

    private async Task<List<TestCaseRef>> LoadAutomatedRefsAsync(
        Guid workspaceId,
        Guid projectId,
        IReadOnlyList<Guid> ids,
        CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        var distinctArray = distinct.ToArray();
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO runs."testcase_refs" ("Id", "WorkspaceId", "ProjectId", "Code", "Title", "Version", "AutomationScenarioPath", "AutomationEngine", "AutomationStatus", "AutomationDefinitionVersion", "AutomationRecordingJson", "IsDeleted")
            SELECT "Id", "WorkspaceId", "ProjectId", "Code", "Title", "Version", "AutomationScenarioPath", "AutomationEngine", "AutomationStatus", "AutomationDefinitionVersion", "AutomationRecordingJson", false
            FROM testcases."testcases"
            WHERE "Id" = ANY({distinctArray}) AND "WorkspaceId" = {workspaceId} AND "ProjectId" = {projectId} AND "DeletedAtUtc" IS NULL
            ON CONFLICT ("Id") DO UPDATE SET
                "WorkspaceId" = EXCLUDED."WorkspaceId",
                "ProjectId" = EXCLUDED."ProjectId",
                "Code" = EXCLUDED."Code",
                "Title" = EXCLUDED."Title",
                "Version" = EXCLUDED."Version",
                "AutomationScenarioPath" = EXCLUDED."AutomationScenarioPath",
                "AutomationEngine" = EXCLUDED."AutomationEngine",
                "AutomationStatus" = EXCLUDED."AutomationStatus",
                "AutomationDefinitionVersion" = EXCLUDED."AutomationDefinitionVersion",
                "AutomationRecordingJson" = EXCLUDED."AutomationRecordingJson",
                "IsDeleted" = false
            """, ct);

        var refs = await _db.TestCaseRefs.AsNoTracking()
            .Where(x => x.WorkspaceId == workspaceId && x.ProjectId == projectId && distinct.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(ct);
        return refs.Where(x => x.HasAutomation).ToList();
    }

    private Task<bool> CanAccessAsync(
        Guid workspaceId,
        Guid userId,
        string accessScope,
        Guid projectId,
        CancellationToken ct,
        string? workspaceRole = null,
        ProjectCapability required = ProjectCapability.View) =>
        ProjectAccessPolicy.CanAccessProjectAsync(
            _db.Database.GetDbConnection(), workspaceId, userId, accessScope, projectId, ct, workspaceRole, required);

}

public sealed record PauseRunScheduleCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ScheduleId) : IRequest<Result<RunScheduleDto>>;

public sealed record ResumeRunScheduleCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ScheduleId) : IRequest<Result<RunScheduleDto>>;

public sealed record RunScheduleNowCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ScheduleId) : IRequest<Result<RunScheduleDto>>;

public sealed record DeleteRunScheduleCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ScheduleId) : IRequest<Result>;

public sealed class RunScheduleStateHandler :
    IRequestHandler<PauseRunScheduleCommand, Result<RunScheduleDto>>,
    IRequestHandler<ResumeRunScheduleCommand, Result<RunScheduleDto>>,
    IRequestHandler<RunScheduleNowCommand, Result<RunScheduleDto>>,
    IRequestHandler<DeleteRunScheduleCommand, Result>
{
    private readonly RunsDbContext _db;
    private readonly RunScheduleSignal _signal;
    public RunScheduleStateHandler(RunsDbContext db, RunScheduleSignal signal)
    {
        _db = db;
        _signal = signal;
    }

    public async Task<Result<RunScheduleDto>> Handle(PauseRunScheduleCommand cmd, CancellationToken ct)
    {
        var schedule = await LoadAuthorizedAsync(
            cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.ScheduleId, ct,
            cmd.CurrentUserRole, ProjectCapability.Contribute);
        if (schedule is null) return Result.Failure<RunScheduleDto>(RunErrors.ScheduleNotFound);
        schedule.Pause();
        await _db.SaveChangesAsync(ct);
        return RunScheduleMapping.Map(schedule);
    }

    public async Task<Result<RunScheduleDto>> Handle(ResumeRunScheduleCommand cmd, CancellationToken ct)
    {
        var schedule = await LoadAuthorizedAsync(
            cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.ScheduleId, ct,
            cmd.CurrentUserRole, ProjectCapability.Contribute);
        if (schedule is null) return Result.Failure<RunScheduleDto>(RunErrors.ScheduleNotFound);
        schedule.Resume(RunScheduleCalculator.NextRunUtc(
            schedule.RecurrenceType, schedule.TimeOfDayMinutes, schedule.WeekDaysMask,
            schedule.TimeZoneId, schedule.StartAtUtc, schedule.EndAtUtc, DateTime.UtcNow));
        await _db.SaveChangesAsync(ct);
        return RunScheduleMapping.Map(schedule);
    }

    public async Task<Result<RunScheduleDto>> Handle(RunScheduleNowCommand cmd, CancellationToken ct)
    {
        var schedule = await LoadAuthorizedAsync(
            cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.ScheduleId, ct,
            cmd.CurrentUserRole, ProjectCapability.Contribute);
        if (schedule is null) return Result.Failure<RunScheduleDto>(RunErrors.ScheduleNotFound);
        schedule.QueueNow();
        await _db.SaveChangesAsync(ct);
        _signal.Pulse();
        return RunScheduleMapping.Map(schedule);
    }

    public async Task<Result> Handle(DeleteRunScheduleCommand cmd, CancellationToken ct)
    {
        var schedule = await LoadAuthorizedAsync(
            cmd.WorkspaceId, cmd.CurrentUserId, cmd.CurrentUserAccessScope, cmd.ScheduleId, ct,
            cmd.CurrentUserRole, ProjectCapability.Contribute);
        if (schedule is null) return Result.Failure(RunErrors.ScheduleNotFound);
        schedule.SoftDelete();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<RunSchedule?> LoadAuthorizedAsync(
        Guid workspaceId,
        Guid userId,
        string accessScope,
        Guid scheduleId,
        CancellationToken ct,
        string? workspaceRole = null,
        ProjectCapability required = ProjectCapability.Contribute)
    {
        var schedule = await _db.RunSchedules
            .Include(x => x.Cases)
            .FirstOrDefaultAsync(x => x.Id == scheduleId && x.WorkspaceId == workspaceId && x.DeletedAtUtc == null, ct);
        if (schedule is null) return null;
        var canAccess = await ProjectAccessPolicy.CanAccessProjectAsync(
            _db.Database.GetDbConnection(),
            workspaceId,
            userId,
            accessScope,
            schedule.ProjectId,
            ct,
            workspaceRole,
            required);
        return canAccess ? schedule : null;
    }
}

public sealed record ListRunScheduleExecutionsQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ScheduleId,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<RunScheduleExecutionDto>>>;

public sealed class ListRunScheduleExecutionsHandler
    : IRequestHandler<ListRunScheduleExecutionsQuery, Result<PagedResult<RunScheduleExecutionDto>>>
{
    private readonly RunsDbContext _db;
    public ListRunScheduleExecutionsHandler(RunsDbContext db) => _db = db;

    public async Task<Result<PagedResult<RunScheduleExecutionDto>>> Handle(ListRunScheduleExecutionsQuery q, CancellationToken ct)
    {
        var schedule = await _db.RunSchedules.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == q.ScheduleId && x.WorkspaceId == q.WorkspaceId && x.DeletedAtUtc == null, ct);
        if (schedule is null)
            return Result.Failure<PagedResult<RunScheduleExecutionDto>>(RunErrors.ScheduleNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                q.WorkspaceId,
                q.CurrentUserId,
                q.CurrentUserAccessScope,
                schedule.ProjectId,
                ct,
                q.CurrentUserRole))
            return Result.Failure<PagedResult<RunScheduleExecutionDto>>(RunErrors.ProjectAccessDenied);

        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 100);
        var query = _db.RunScheduleExecutions.AsNoTracking().Where(x => x.RunScheduleId == q.ScheduleId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.StartedAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return new PagedResult<RunScheduleExecutionDto>(
            items.Select(RunScheduleMapping.MapExecution).ToList(), page, size, total);
    }
}
