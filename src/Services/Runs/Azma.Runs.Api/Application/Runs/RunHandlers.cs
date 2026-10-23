using Azma.Auth.ProjectAccess;
using Azma.Common.Pagination;
using Azma.Common.Results;
using Azma.EventBus.Contracts.Runs;
using Azma.Runs.Api.Application.Common;
using Azma.Runs.Api.Domain;
using Azma.Runs.Api.Infrastructure.Persistence;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Runs.Api.Application.Runs;

internal static class RunMapping
{
    public static RunCaseDto Map(RunCase c) => new(
        c.Id, c.TestCaseId, c.TestCaseCode, c.TestCaseTitle, c.TestCaseVersion,
        c.HasAutomation, c.AutomationEngine, c.AutomationStatus, c.AutomationDefinitionVersion,
        c.AutomationScenarioPath,
        c.AssignedToUserId, c.CurrentStatus, c.LastExecutedAtUtc, c.LastExecutedByUserId);

    public static RunResultDto Map(RunResult r) => new(
        r.Id, r.RunCaseId, r.Status, r.Comment, r.ElapsedSeconds,
        r.AutomationEngine, r.AutomationDetailJson,
        r.ExecutedByUserId, r.ExecutedAtUtc);

    public static RunCommentDto Map(RunComment c) => new(
        c.Id, c.RunCaseId, c.AuthorUserId, c.Body, c.CreatedAtUtc);

    public static RunAutomationExecutionDto Map(RunAutomationExecution e) => new(
        e.Id, e.RunId, e.Status, e.Trigger, e.StartedAtUtc, e.CompletedAtUtc,
        e.TotalCases, e.Passed, e.Failed, e.ErrorMessage);

    public static RunStatusCounts CountOf(IEnumerable<RunCase> cases)
    {
        int u = 0, p = 0, f = 0, b = 0, r = 0, s = 0;
        foreach (var c in cases)
        {
            switch (c.CurrentStatus)
            {
                case ResultStatus.Untested: u++; break;
                case ResultStatus.Passed: p++; break;
                case ResultStatus.Failed: f++; break;
                case ResultStatus.Blocked: b++; break;
                case ResultStatus.Retest: r++; break;
                case ResultStatus.Skipped: s++; break;
            }
        }
        return new(u, p, f, b, r, s);
    }
}

// ─── Create Run ──────────────────────────────────────────────────────────
public sealed record CreateRunCommand(
    Guid WorkspaceId,
    Guid CreatedByUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ProjectId,
    Guid? MilestoneId,
    string Name,
    string? Description,
    IReadOnlyList<Guid> TestCaseIds,
    string? Environment,
    DateTime? DueDateUtc,
    bool AutoExecute) : IRequest<Result<RunDto>>;

public sealed class CreateRunValidator : AbstractValidator<CreateRunCommand>
{
    public CreateRunValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.TestCaseIds).NotNull().Must(x => x.Count > 0).WithMessage("حداقل یک تست‌کیس لازم است.");
    }
}

public sealed class CreateRunHandler : IRequestHandler<CreateRunCommand, Result<RunDto>>
{
    private readonly RunsDbContext _db;
    private readonly IPublishEndpoint _bus;
    private readonly RunAutomationSignal _automationSignal;
    public CreateRunHandler(RunsDbContext db, IPublishEndpoint bus, RunAutomationSignal automationSignal)
    {
        _db = db;
        _bus = bus;
        _automationSignal = automationSignal;
    }

    public async Task<Result<RunDto>> Handle(CreateRunCommand cmd, CancellationToken ct)
    {
        if (cmd.TestCaseIds.Count == 0)
            return Result.Failure<RunDto>(RunErrors.NoTestCases);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CreatedByUserId,
                cmd.CurrentUserAccessScope,
                cmd.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<RunDto>(RunErrors.ProjectAccessDenied);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var projectRef = await _db.ProjectRefs
            .FromSqlInterpolated($"""
                SELECT * FROM runs.project_refs
                WHERE "Id" = {cmd.ProjectId} AND "WorkspaceId" = {cmd.WorkspaceId}
                FOR UPDATE
                """)
            .AsTracking()
            .FirstOrDefaultAsync(ct);

        // Fallback: if project ref is missing (event bus delay), sync from projects schema
        if (projectRef is null)
        {
            await ProjectRefSynchronizer.EnsureFromProjectsAsync(_db, cmd.WorkspaceId, cmd.ProjectId, ct);

            projectRef = await _db.ProjectRefs
                .FromSqlInterpolated($"""
                    SELECT * FROM runs.project_refs
                    WHERE "Id" = {cmd.ProjectId} AND "WorkspaceId" = {cmd.WorkspaceId}
                    FOR UPDATE
                    """)
                .AsTracking()
                .FirstOrDefaultAsync(ct);
        }

        if (projectRef is null) return Result.Failure<RunDto>(RunErrors.ProjectNotKnown);
        if (projectRef.IsArchived) return Result.Failure<RunDto>(RunErrors.ProjectArchived);

        var distinctIds = cmd.TestCaseIds.Distinct().ToList();
        var refs = await _db.TestCaseRefs.AsNoTracking()
            .Where(x => x.WorkspaceId == cmd.WorkspaceId && distinctIds.Contains(x.Id))
            .ToListAsync(ct);

        // Fallback: if refs are missing (event bus delay), sync directly from testcases schema
        if (refs.Count != distinctIds.Count)
        {
            var existingIds = refs.Select(r => r.Id).ToHashSet();
            var missingIds = distinctIds.Where(id => !existingIds.Contains(id)).ToArray();

            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO runs."testcase_refs" ("Id", "WorkspaceId", "ProjectId", "Code", "Title", "Version", "AutomationScenarioPath", "AutomationEngine", "AutomationStatus", "AutomationDefinitionVersion", "AutomationRecordingJson", "IsDeleted")
                SELECT "Id", "WorkspaceId", "ProjectId", "Code", "Title", "Version", "AutomationScenarioPath", "AutomationEngine", "AutomationStatus", "AutomationDefinitionVersion", "AutomationRecordingJson", false
                FROM testcases."testcases"
                WHERE "Id" = ANY({missingIds}) AND "WorkspaceId" = {cmd.WorkspaceId} AND "DeletedAtUtc" IS NULL
                ON CONFLICT ("Id") DO NOTHING
                """, ct);

            refs = await _db.TestCaseRefs.AsNoTracking()
                .Where(x => x.WorkspaceId == cmd.WorkspaceId && distinctIds.Contains(x.Id))
                .ToListAsync(ct);
        }

        if (refs.Count != distinctIds.Count)
            return Result.Failure<RunDto>(RunErrors.TestCaseUnknown);
        if (refs.Any(r => r.ProjectId != cmd.ProjectId))
            return Result.Failure<RunDto>(RunErrors.TestCaseProjectMismatch);
        if (cmd.AutoExecute && refs.Any(r => !r.HasAutomation))
            return Result.Failure<RunDto>(RunErrors.RunHasManualCases);

        var seq = projectRef.AllocateRunSequence();
        var code = $"{projectRef.Code}-R-{seq:D3}";

        var run = TestRun.Create(cmd.WorkspaceId, cmd.ProjectId, cmd.MilestoneId,
            code, seq, cmd.Name, cmd.Description, cmd.CreatedByUserId,
            cmd.Environment, cmd.DueDateUtc);

        // Preserve order from request as much as possible
        var refById = refs.ToDictionary(r => r.Id);
        foreach (var id in distinctIds)
        {
            var r = refById[id];
            run.Cases.Add(RunCase.Snapshot(
                run.Id, r.Id, r.Code, r.Title, r.Version, null, r.AutomationScenarioPath,
                r.AutomationEngine, r.AutomationStatus, r.AutomationDefinitionVersion, r.AutomationRecordingJson));
        }

        _db.Runs.Add(run);
        if (cmd.AutoExecute)
            _db.RunAutomationExecutions.Add(RunAutomationExecution.Queue(run.Id, "create", run.Cases.Count));

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _bus.Publish(new TestRunCreated(run.WorkspaceId, run.ProjectId, run.Id, run.Name, run.Cases.Count), ct);
        if (cmd.AutoExecute)
            _automationSignal.Pulse();

        return new RunDto(run.Id, run.ProjectId, run.MilestoneId, run.Code, run.Name, run.Description,
            run.Status, RunMapping.CountOf(run.Cases),
            run.CreatedByUserId, run.CreatedAtUtc, run.ClosedAtUtc, run.ClosedByUserId,
            run.Environment, run.DueDateUtc);
    }
}

// ─── Get Run ─────────────────────────────────────────────────────────────
public sealed record GetRunQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunId) : IRequest<Result<RunDto>>;

public sealed class GetRunHandler : IRequestHandler<GetRunQuery, Result<RunDto>>
{
    private readonly RunsDbContext _db;
    public GetRunHandler(RunsDbContext db) => _db = db;

    public async Task<Result<RunDto>> Handle(GetRunQuery q, CancellationToken ct)
    {
        var run = await _db.Runs.AsNoTracking()
            .Include(x => x.Cases)
            .FirstOrDefaultAsync(x => x.Id == q.RunId && x.WorkspaceId == q.WorkspaceId, ct);
        if (run is null) return Result.Failure<RunDto>(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                q.WorkspaceId,
                q.CurrentUserId,
                q.CurrentUserAccessScope,
                run.ProjectId,
                ct,
                q.CurrentUserRole))
            return Result.Failure<RunDto>(RunErrors.ProjectAccessDenied);
        return new RunDto(run.Id, run.ProjectId, run.MilestoneId, run.Code, run.Name, run.Description,
            run.Status, RunMapping.CountOf(run.Cases),
            run.CreatedByUserId, run.CreatedAtUtc, run.ClosedAtUtc, run.ClosedByUserId,
            run.Environment, run.DueDateUtc);
    }
}

// ─── List Runs ───────────────────────────────────────────────────────────
public sealed record ListRunsQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid? ProjectId,
    Guid? MilestoneId,
    RunStatus? Status,
    string? Search,
    bool IncludeDeleted,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<RunSummaryDto>>>;

public sealed class ListRunsHandler : IRequestHandler<ListRunsQuery, Result<PagedResult<RunSummaryDto>>>
{
    private readonly RunsDbContext _db;
    public ListRunsHandler(RunsDbContext db) => _db = db;

    public async Task<Result<PagedResult<RunSummaryDto>>> Handle(ListRunsQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);

        var query = _db.Runs.AsNoTracking().Where(x => x.WorkspaceId == q.WorkspaceId);
        var visibility = await ProjectAccessPolicy.GetVisibilityAsync(
            _db.Database.GetDbConnection(),
            q.WorkspaceId,
            q.CurrentUserId,
            q.CurrentUserAccessScope,
            q.CurrentUserRole,
            ct);
        var allowed = visibility.AllowedArray();
        var denied = visibility.DeniedArray();
        if (allowed is not null)
            query = query.Where(x => allowed.Contains(x.ProjectId));
        if (denied.Length > 0)
            query = query.Where(x => !denied.Contains(x.ProjectId));
        if (!q.IncludeDeleted) query = query.Where(x => x.DeletedAtUtc == null);
        if (q.ProjectId is { } pid) query = query.Where(x => x.ProjectId == pid);
        if (q.MilestoneId is { } mid) query = query.Where(x => x.MilestoneId == mid);
        if (q.Status is { } st) query = query.Where(x => x.Status == st);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var like = $"%{q.Search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, like) || EF.Functions.ILike(x.Code, like));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(x => x.Cases)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);

        var dtos = items.Select(r => new RunSummaryDto(
            r.Id, r.ProjectId, r.MilestoneId, r.Code, r.Name, r.Status,
            r.Cases.Count, RunMapping.CountOf(r.Cases),
            r.CreatedByUserId, r.CreatedAtUtc, r.ClosedAtUtc,
            r.Environment, r.DueDateUtc)).ToList();
        return new PagedResult<RunSummaryDto>(dtos, page, size, total);
    }
}

// ─── Close / Reopen / Delete Run ─────────────────────────────────────────
public sealed record CloseRunCommand(
    Guid WorkspaceId,
    Guid UserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunId) : IRequest<Result<RunDto>>;

public sealed class CloseRunHandler : IRequestHandler<CloseRunCommand, Result<RunDto>>
{
    private readonly RunsDbContext _db;
    private readonly IPublishEndpoint _bus;
    public CloseRunHandler(RunsDbContext db, IPublishEndpoint bus) { _db = db; _bus = bus; }

    public async Task<Result<RunDto>> Handle(CloseRunCommand cmd, CancellationToken ct)
    {
        var run = await _db.Runs.Include(x => x.Cases)
            .FirstOrDefaultAsync(x => x.Id == cmd.RunId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (run is null) return Result.Failure<RunDto>(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.UserId,
                cmd.CurrentUserAccessScope,
                run.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<RunDto>(RunErrors.ProjectAccessDenied);
        if (run.IsDeleted) return Result.Failure<RunDto>(RunErrors.RunDeleted);

        run.Close(cmd.UserId);
        await _db.SaveChangesAsync(ct);

        var counts = RunMapping.CountOf(run.Cases);
        await _bus.Publish(new TestRunCompleted(run.WorkspaceId, run.ProjectId, run.Id,
            counts.Passed, counts.Failed, counts.Blocked, counts.Skipped), ct);

        return new RunDto(run.Id, run.ProjectId, run.MilestoneId, run.Code, run.Name, run.Description,
            run.Status, counts, run.CreatedByUserId, run.CreatedAtUtc, run.ClosedAtUtc, run.ClosedByUserId,
            run.Environment, run.DueDateUtc);
    }
}

public sealed record ReopenRunCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunId) : IRequest<Result<RunDto>>;

public sealed class ReopenRunHandler : IRequestHandler<ReopenRunCommand, Result<RunDto>>
{
    private readonly RunsDbContext _db;
    public ReopenRunHandler(RunsDbContext db) => _db = db;

    public async Task<Result<RunDto>> Handle(ReopenRunCommand cmd, CancellationToken ct)
    {
        var run = await _db.Runs.Include(x => x.Cases)
            .FirstOrDefaultAsync(x => x.Id == cmd.RunId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (run is null) return Result.Failure<RunDto>(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                run.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<RunDto>(RunErrors.ProjectAccessDenied);
        if (run.IsDeleted) return Result.Failure<RunDto>(RunErrors.RunDeleted);
        run.Reopen();
        await _db.SaveChangesAsync(ct);
        return new RunDto(run.Id, run.ProjectId, run.MilestoneId, run.Code, run.Name, run.Description,
            run.Status, RunMapping.CountOf(run.Cases),
            run.CreatedByUserId, run.CreatedAtUtc, run.ClosedAtUtc, run.ClosedByUserId,
            run.Environment, run.DueDateUtc);
    }
}

public sealed record DeleteRunCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunId) : IRequest<Result>;

public sealed class DeleteRunHandler : IRequestHandler<DeleteRunCommand, Result>
{
    private readonly RunsDbContext _db;
    public DeleteRunHandler(RunsDbContext db) => _db = db;

    public async Task<Result> Handle(DeleteRunCommand cmd, CancellationToken ct)
    {
        var run = await _db.Runs.FirstOrDefaultAsync(
            x => x.Id == cmd.RunId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (run is null) return Result.Failure(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                run.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure(RunErrors.ProjectAccessDenied);
        if (run.IsDeleted) return Result.Success();
        run.SoftDelete();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── List Run Cases ──────────────────────────────────────────────────────
public sealed record ListRunCasesQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunId,
    ResultStatus? Status,
    Guid? AssignedToUserId,
    string? Search,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<RunCaseDto>>>;

public sealed class ListRunCasesHandler : IRequestHandler<ListRunCasesQuery, Result<PagedResult<RunCaseDto>>>
{
    private readonly RunsDbContext _db;
    public ListRunCasesHandler(RunsDbContext db) => _db = db;

    public async Task<Result<PagedResult<RunCaseDto>>> Handle(ListRunCasesQuery q, CancellationToken ct)
    {
        var projectId = await _db.Runs.AsNoTracking()
            .Where(x => x.Id == q.RunId && x.WorkspaceId == q.WorkspaceId)
            .Select(x => (Guid?)x.ProjectId)
            .FirstOrDefaultAsync(ct);
        if (projectId is null) return Result.Failure<PagedResult<RunCaseDto>>(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                q.WorkspaceId,
                q.CurrentUserId,
                q.CurrentUserAccessScope,
                projectId.Value,
                ct,
                q.CurrentUserRole))
            return Result.Failure<PagedResult<RunCaseDto>>(RunErrors.ProjectAccessDenied);

        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 500);

        var query = _db.RunCases.AsNoTracking().Where(x => x.RunId == q.RunId);
        if (q.Status is { } st) query = query.Where(x => x.CurrentStatus == st);
        if (q.AssignedToUserId is { } u) query = query.Where(x => x.AssignedToUserId == u);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var like = $"%{q.Search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.TestCaseTitle, like) || EF.Functions.ILike(x.TestCaseCode, like));
        }

        var total = await query.CountAsync(ct);
        var entities = await query
            .OrderBy(x => x.TestCaseCode)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);
        var items = entities.Select(RunMapping.Map).ToList();

        return new PagedResult<RunCaseDto>(items, page, size, total);
    }
}

// ─── Assign Run Case ─────────────────────────────────────────────────────
public sealed record AssignRunCaseCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunCaseId,
    Guid? AssignedToUserId) : IRequest<Result<RunCaseDto>>;

public sealed class AssignRunCaseHandler : IRequestHandler<AssignRunCaseCommand, Result<RunCaseDto>>
{
    private readonly RunsDbContext _db;
    public AssignRunCaseHandler(RunsDbContext db) => _db = db;

    public async Task<Result<RunCaseDto>> Handle(AssignRunCaseCommand cmd, CancellationToken ct)
    {
        var rc = await _db.RunCases
            .Where(x => x.Id == cmd.RunCaseId)
            .Join(_db.Runs, c => c.RunId, r => r.Id, (c, r) => new { Case = c, Run = r })
            .FirstOrDefaultAsync(x => x.Run.WorkspaceId == cmd.WorkspaceId, ct);
        if (rc is null) return Result.Failure<RunCaseDto>(RunErrors.RunCaseNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                rc.Run.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<RunCaseDto>(RunErrors.ProjectAccessDenied);
        if (rc.Run.IsDeleted) return Result.Failure<RunCaseDto>(RunErrors.RunDeleted);

        rc.Case.AssignTo(cmd.AssignedToUserId);
        await _db.SaveChangesAsync(ct);
        return RunMapping.Map(rc.Case);
    }
}

// ─── Record Result ───────────────────────────────────────────────────────
public sealed record RecordResultCommand(
    Guid WorkspaceId,
    Guid ExecutedByUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunCaseId,
    ResultStatus Status,
    string? Comment,
    int? ElapsedSeconds) : IRequest<Result<RunResultDto>>;

public sealed class RecordResultValidator : AbstractValidator<RecordResultCommand>
{
    public RecordResultValidator()
    {
        RuleFor(x => x.Status).Must(s => s is ResultStatus.Passed or ResultStatus.Failed
                                         or ResultStatus.Blocked or ResultStatus.Retest
                                         or ResultStatus.Skipped or ResultStatus.Untested);
        RuleFor(x => x.Comment).MaximumLength(4000);
    }
}

public sealed class RecordResultHandler : IRequestHandler<RecordResultCommand, Result<RunResultDto>>
{
    private readonly RunsDbContext _db;
    private readonly IPublishEndpoint _bus;
    public RecordResultHandler(RunsDbContext db, IPublishEndpoint bus) { _db = db; _bus = bus; }

    public async Task<Result<RunResultDto>> Handle(RecordResultCommand cmd, CancellationToken ct)
    {
        var joined = await _db.RunCases
            .Where(x => x.Id == cmd.RunCaseId)
            .Join(_db.Runs, c => c.RunId, r => r.Id, (c, r) => new { Case = c, Run = r })
            .FirstOrDefaultAsync(x => x.Run.WorkspaceId == cmd.WorkspaceId, ct);
        if (joined is null) return Result.Failure<RunResultDto>(RunErrors.RunCaseNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.ExecutedByUserId,
                cmd.CurrentUserAccessScope,
                joined.Run.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<RunResultDto>(RunErrors.ProjectAccessDenied);
        if (joined.Run.IsDeleted) return Result.Failure<RunResultDto>(RunErrors.RunDeleted);
        if (joined.Run.Status == RunStatus.Closed) return Result.Failure<RunResultDto>(RunErrors.RunClosed);

        var result = joined.Case.RecordResult(cmd.Status, cmd.Comment, cmd.ElapsedSeconds, cmd.ExecutedByUserId);
        _db.RunResults.Add(result);
        await _db.SaveChangesAsync(ct);

        await _bus.Publish(new TestResultRecorded(
            joined.Run.WorkspaceId, joined.Run.ProjectId, joined.Run.Id,
            joined.Case.Id, result.Id, joined.Case.TestCaseId, cmd.Status.ToString(),
            cmd.ExecutedByUserId, result.ExecutedAtUtc), ct);

        return RunMapping.Map(result);
    }
}

// ─── List Results for a Run Case ─────────────────────────────────────────
// Automatic execution for a run case
public sealed record ExecuteAutomaticRunCaseCommand(
    Guid WorkspaceId,
    Guid ExecutedByUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunCaseId,
    string? Gateway,
    string? Frontend) : IRequest<Result<AutomaticRunResultDto>>;

public sealed class ExecuteAutomaticRunCaseValidator : AbstractValidator<ExecuteAutomaticRunCaseCommand>
{
    public ExecuteAutomaticRunCaseValidator()
    {
        RuleFor(x => x.Gateway).MaximumLength(1000);
        RuleFor(x => x.Frontend).MaximumLength(1000);
    }
}

public sealed class ExecuteAutomaticRunCaseHandler
    : IRequestHandler<ExecuteAutomaticRunCaseCommand, Result<AutomaticRunResultDto>>
{
    private readonly RunsDbContext _db;
    private readonly AutomaticRunCaseExecutor _caseExecutor;

    public ExecuteAutomaticRunCaseHandler(RunsDbContext db, AutomaticRunCaseExecutor caseExecutor)
    {
        _db = db;
        _caseExecutor = caseExecutor;
    }

    public async Task<Result<AutomaticRunResultDto>> Handle(ExecuteAutomaticRunCaseCommand cmd, CancellationToken ct)
    {
        var joined = await _db.RunCases
            .Where(x => x.Id == cmd.RunCaseId)
            .Join(_db.Runs, c => c.RunId, r => r.Id, (c, r) => new { Case = c, Run = r })
            .FirstOrDefaultAsync(x => x.Run.WorkspaceId == cmd.WorkspaceId, ct);

        if (joined is null) return Result.Failure<AutomaticRunResultDto>(RunErrors.RunCaseNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.ExecutedByUserId,
                cmd.CurrentUserAccessScope,
                joined.Run.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<AutomaticRunResultDto>(RunErrors.ProjectAccessDenied);
        if (joined.Run.IsDeleted) return Result.Failure<AutomaticRunResultDto>(RunErrors.RunDeleted);
        if (joined.Run.Status == RunStatus.Closed) return Result.Failure<AutomaticRunResultDto>(RunErrors.RunClosed);

        var outcome = await _caseExecutor.ExecuteAsync(
            joined.Run,
            joined.Case,
            cmd.ExecutedByUserId,
            new AutomaticRunCaseExecutionOptions(
                cmd.Gateway,
                cmd.Frontend),
            ct);

        if (outcome.IsFailure)
            return Result.Failure<AutomaticRunResultDto>(outcome.Error);

        var value = outcome.Value;

        return new AutomaticRunResultDto(
            RunMapping.Map(value.Result),
            value.ScenarioName,
            value.AutomationEngine,
            value.Succeeded,
            value.ErrorMessage);
    }

}

public sealed record QueueRunAutomationCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunId,
    string Trigger) : IRequest<Result<RunAutomationExecutionDto>>;

public sealed class QueueRunAutomationHandler
    : IRequestHandler<QueueRunAutomationCommand, Result<RunAutomationExecutionDto>>
{
    private readonly RunsDbContext _db;
    private readonly RunAutomationSignal _signal;

    public QueueRunAutomationHandler(RunsDbContext db, RunAutomationSignal signal)
    {
        _db = db;
        _signal = signal;
    }

    public async Task<Result<RunAutomationExecutionDto>> Handle(QueueRunAutomationCommand cmd, CancellationToken ct)
    {
        var run = await _db.Runs.Include(x => x.Cases)
            .FirstOrDefaultAsync(x => x.Id == cmd.RunId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (run is null) return Result.Failure<RunAutomationExecutionDto>(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                run.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<RunAutomationExecutionDto>(RunErrors.ProjectAccessDenied);
        if (run.IsDeleted) return Result.Failure<RunAutomationExecutionDto>(RunErrors.RunDeleted);
        if (run.Status == RunStatus.Closed) return Result.Failure<RunAutomationExecutionDto>(RunErrors.RunClosed);
        if (run.Cases.Count == 0) return Result.Failure<RunAutomationExecutionDto>(RunErrors.NoTestCases);
        if (run.Cases.Any(x => !x.HasAutomation))
            return Result.Failure<RunAutomationExecutionDto>(RunErrors.RunHasManualCases);

        var hasActiveExecution = await _db.RunAutomationExecutions.AsNoTracking()
            .AnyAsync(x => x.RunId == run.Id &&
                           (x.Status == RunAutomationExecutionStatus.Queued ||
                            x.Status == RunAutomationExecutionStatus.Running), ct);
        if (hasActiveExecution)
            return Result.Failure<RunAutomationExecutionDto>(RunErrors.RunAutomationAlreadyQueued);

        var execution = RunAutomationExecution.Queue(run.Id, cmd.Trigger, run.Cases.Count);
        _db.RunAutomationExecutions.Add(execution);
        await _db.SaveChangesAsync(ct);
        _signal.Pulse();
        return RunMapping.Map(execution);
    }
}

public sealed record ListRunAutomationExecutionsQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunId,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<RunAutomationExecutionDto>>>;

public sealed class ListRunAutomationExecutionsHandler
    : IRequestHandler<ListRunAutomationExecutionsQuery, Result<PagedResult<RunAutomationExecutionDto>>>
{
    private readonly RunsDbContext _db;
    public ListRunAutomationExecutionsHandler(RunsDbContext db) => _db = db;

    public async Task<Result<PagedResult<RunAutomationExecutionDto>>> Handle(
        ListRunAutomationExecutionsQuery q,
        CancellationToken ct)
    {
        var run = await _db.Runs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == q.RunId && x.WorkspaceId == q.WorkspaceId, ct);
        if (run is null)
            return Result.Failure<PagedResult<RunAutomationExecutionDto>>(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                q.WorkspaceId,
                q.CurrentUserId,
                q.CurrentUserAccessScope,
                run.ProjectId,
                ct,
                q.CurrentUserRole))
            return Result.Failure<PagedResult<RunAutomationExecutionDto>>(RunErrors.ProjectAccessDenied);

        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 100);
        var query = _db.RunAutomationExecutions.AsNoTracking()
            .Where(x => x.RunId == q.RunId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.StartedAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return new PagedResult<RunAutomationExecutionDto>(
            items.Select(RunMapping.Map).ToList(), page, size, total);
    }
}

public sealed record ListResultsQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunCaseId) : IRequest<Result<IReadOnlyList<RunResultDto>>>;

public sealed class ListResultsHandler : IRequestHandler<ListResultsQuery, Result<IReadOnlyList<RunResultDto>>>
{
    private readonly RunsDbContext _db;
    public ListResultsHandler(RunsDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<RunResultDto>>> Handle(ListResultsQuery q, CancellationToken ct)
    {
        var owned = await _db.RunCases.AsNoTracking()
            .Where(x => x.Id == q.RunCaseId)
            .Join(_db.Runs.AsNoTracking(), c => c.RunId, r => r.Id, (c, r) => new { r.WorkspaceId, r.ProjectId })
            .FirstOrDefaultAsync(ct);
        if (owned is null || owned.WorkspaceId != q.WorkspaceId)
            return Result.Failure<IReadOnlyList<RunResultDto>>(RunErrors.RunCaseNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                q.WorkspaceId,
                q.CurrentUserId,
                q.CurrentUserAccessScope,
                owned.ProjectId,
                ct,
                q.CurrentUserRole))
            return Result.Failure<IReadOnlyList<RunResultDto>>(RunErrors.ProjectAccessDenied);

        var list = await _db.RunResults.AsNoTracking()
            .Where(x => x.RunCaseId == q.RunCaseId)
            .OrderByDescending(x => x.ExecutedAtUtc)
            .Select(x => new RunResultDto(
                x.Id,
                x.RunCaseId,
                x.Status,
                x.Comment,
                x.ElapsedSeconds,
                x.AutomationEngine,
                x.AutomationDetailJson,
                x.ExecutedByUserId,
                x.ExecutedAtUtc))
            .ToListAsync(ct);
        IReadOnlyList<RunResultDto> result = list;
        return Result.Success(result);
    }
}

// ─── Add / List Comments ─────────────────────────────────────────────────
public sealed record AddCommentCommand(
    Guid WorkspaceId,
    Guid AuthorUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunId,
    Guid? RunCaseId,
    string Body) : IRequest<Result<RunCommentDto>>;

public sealed class AddCommentValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentValidator() { RuleFor(x => x.Body).NotEmpty().MaximumLength(4000); }
}

public sealed class AddCommentHandler : IRequestHandler<AddCommentCommand, Result<RunCommentDto>>
{
    private readonly RunsDbContext _db;
    public AddCommentHandler(RunsDbContext db) => _db = db;

    public async Task<Result<RunCommentDto>> Handle(AddCommentCommand cmd, CancellationToken ct)
    {
        var run = await _db.Runs.FirstOrDefaultAsync(
            x => x.Id == cmd.RunId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (run is null) return Result.Failure<RunCommentDto>(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.AuthorUserId,
                cmd.CurrentUserAccessScope,
                run.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<RunCommentDto>(RunErrors.ProjectAccessDenied);
        if (run.IsDeleted) return Result.Failure<RunCommentDto>(RunErrors.RunDeleted);

        if (cmd.RunCaseId is { } rcid)
        {
            var caseExists = await _db.RunCases.AsNoTracking().AnyAsync(x => x.Id == rcid && x.RunId == cmd.RunId, ct);
            if (!caseExists) return Result.Failure<RunCommentDto>(RunErrors.RunCaseNotFound);
        }

        var comment = RunComment.Create(cmd.RunId, cmd.RunCaseId, cmd.AuthorUserId, cmd.Body);
        _db.RunComments.Add(comment);
        await _db.SaveChangesAsync(ct);
        return RunMapping.Map(comment);
    }
}

public sealed record ListCommentsQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunId,
    Guid? RunCaseId) : IRequest<Result<IReadOnlyList<RunCommentDto>>>;

public sealed class ListCommentsHandler : IRequestHandler<ListCommentsQuery, Result<IReadOnlyList<RunCommentDto>>>
{
    private readonly RunsDbContext _db;
    public ListCommentsHandler(RunsDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<RunCommentDto>>> Handle(ListCommentsQuery q, CancellationToken ct)
    {
        var projectId = await _db.Runs.AsNoTracking()
            .Where(x => x.Id == q.RunId && x.WorkspaceId == q.WorkspaceId)
            .Select(x => (Guid?)x.ProjectId)
            .FirstOrDefaultAsync(ct);
        if (projectId is null) return Result.Failure<IReadOnlyList<RunCommentDto>>(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                q.WorkspaceId,
                q.CurrentUserId,
                q.CurrentUserAccessScope,
                projectId.Value,
                ct,
                q.CurrentUserRole))
            return Result.Failure<IReadOnlyList<RunCommentDto>>(RunErrors.ProjectAccessDenied);

        var query = _db.RunComments.AsNoTracking().Where(x => x.RunId == q.RunId);
        if (q.RunCaseId is { } rcid) query = query.Where(x => x.RunCaseId == rcid);

        var list = await query.OrderBy(x => x.CreatedAtUtc)
            .Select(x => new RunCommentDto(x.Id, x.RunCaseId, x.AuthorUserId, x.Body, x.CreatedAtUtc))
            .ToListAsync(ct);
        IReadOnlyList<RunCommentDto> result = list;
        return Result.Success(result);
    }
}

// ─── Update Run Metadata ──────────────────────────────────────────────────
public sealed record UpdateRunCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunId,
    string? Name,
    string? Description,
    string? Environment,
    DateTime? DueDateUtc) : IRequest<Result<RunDto>>;

public sealed class UpdateRunHandler : IRequestHandler<UpdateRunCommand, Result<RunDto>>
{
    private readonly RunsDbContext _db;
    public UpdateRunHandler(RunsDbContext db) => _db = db;

    public async Task<Result<RunDto>> Handle(UpdateRunCommand cmd, CancellationToken ct)
    {
        var run = await _db.Runs.Include(x => x.Cases)
            .FirstOrDefaultAsync(x => x.Id == cmd.RunId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (run is null) return Result.Failure<RunDto>(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                run.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<RunDto>(RunErrors.ProjectAccessDenied);
        if (run.IsDeleted) return Result.Failure<RunDto>(RunErrors.RunDeleted);

        run.UpdateMetadata(cmd.Name, cmd.Description, cmd.Environment, cmd.DueDateUtc);
        await _db.SaveChangesAsync(ct);
        return new RunDto(run.Id, run.ProjectId, run.MilestoneId, run.Code, run.Name, run.Description,
            run.Status, RunMapping.CountOf(run.Cases),
            run.CreatedByUserId, run.CreatedAtUtc, run.ClosedAtUtc, run.ClosedByUserId,
            run.Environment, run.DueDateUtc);
    }
}

// ─── Duplicate Run ────────────────────────────────────────────────────────
public sealed record DuplicateRunCommand(
    Guid WorkspaceId,
    Guid UserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid SourceRunId,
    string? Name) : IRequest<Result<RunDto>>;

public sealed class DuplicateRunHandler : IRequestHandler<DuplicateRunCommand, Result<RunDto>>
{
    private readonly RunsDbContext _db;
    private readonly IPublishEndpoint _bus;
    public DuplicateRunHandler(RunsDbContext db, IPublishEndpoint bus) { _db = db; _bus = bus; }

    public async Task<Result<RunDto>> Handle(DuplicateRunCommand cmd, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var source = await _db.Runs.AsNoTracking().Include(x => x.Cases)
            .FirstOrDefaultAsync(x => x.Id == cmd.SourceRunId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (source is null) return Result.Failure<RunDto>(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.UserId,
                cmd.CurrentUserAccessScope,
                source.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<RunDto>(RunErrors.ProjectAccessDenied);

        var projectRef = await _db.ProjectRefs
            .FromSqlInterpolated($"""
                SELECT * FROM runs.project_refs
                WHERE "Id" = {source.ProjectId} AND "WorkspaceId" = {cmd.WorkspaceId}
                FOR UPDATE
                """)
            .AsTracking()
            .FirstOrDefaultAsync(ct);

        if (projectRef is null)
        {
            await ProjectRefSynchronizer.EnsureFromProjectsAsync(_db, cmd.WorkspaceId, source.ProjectId, ct);

            projectRef = await _db.ProjectRefs
                .FromSqlInterpolated($"""
                    SELECT * FROM runs.project_refs
                    WHERE "Id" = {source.ProjectId} AND "WorkspaceId" = {cmd.WorkspaceId}
                    FOR UPDATE
                    """)
                .AsTracking()
                .FirstOrDefaultAsync(ct);
        }

        if (projectRef is null) return Result.Failure<RunDto>(RunErrors.ProjectNotKnown);
        if (projectRef.IsArchived) return Result.Failure<RunDto>(RunErrors.ProjectArchived);

        var seq = projectRef.AllocateRunSequence();
        var code = $"{projectRef.Code}-R-{seq:D3}";
        var name = cmd.Name?.Trim() ?? $"کپی {source.Name}";

        var run = TestRun.Create(cmd.WorkspaceId, source.ProjectId, source.MilestoneId,
            code, seq, name, source.Description, cmd.UserId);

        foreach (var sc in source.Cases)
            run.Cases.Add(RunCase.Snapshot(
                run.Id, sc.TestCaseId, sc.TestCaseCode, sc.TestCaseTitle,
                sc.TestCaseVersion, null, sc.AutomationScenarioPath,
                sc.AutomationEngine, sc.AutomationStatus,
                sc.AutomationDefinitionVersion, sc.AutomationRecordingJson));

        _db.Runs.Add(run);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _bus.Publish(new TestRunCreated(run.WorkspaceId, run.ProjectId, run.Id, run.Name, run.Cases.Count), ct);

        return new RunDto(run.Id, run.ProjectId, run.MilestoneId, run.Code, run.Name, run.Description,
            run.Status, RunMapping.CountOf(run.Cases),
            run.CreatedByUserId, run.CreatedAtUtc, run.ClosedAtUtc, run.ClosedByUserId,
            run.Environment, run.DueDateUtc);
    }
}

// ─── Add Cases to Run ─────────────────────────────────────────────────────
public sealed record AddRunCasesCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunId,
    IReadOnlyList<Guid> TestCaseIds) : IRequest<Result<RunDto>>;

public sealed class AddRunCasesHandler : IRequestHandler<AddRunCasesCommand, Result<RunDto>>
{
    private readonly RunsDbContext _db;
    public AddRunCasesHandler(RunsDbContext db) => _db = db;

    public async Task<Result<RunDto>> Handle(AddRunCasesCommand cmd, CancellationToken ct)
    {
        var run = await _db.Runs.Include(x => x.Cases)
            .FirstOrDefaultAsync(x => x.Id == cmd.RunId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (run is null) return Result.Failure<RunDto>(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                run.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<RunDto>(RunErrors.ProjectAccessDenied);
        if (run.IsDeleted) return Result.Failure<RunDto>(RunErrors.RunDeleted);
        if (run.Status == RunStatus.Closed) return Result.Failure<RunDto>(RunErrors.RunClosed);

        var existingIds = run.Cases.Select(c => c.TestCaseId).ToHashSet();
        var newIds = cmd.TestCaseIds.Where(id => !existingIds.Contains(id)).Distinct().ToList();

        if (newIds.Count == 0)
            return new RunDto(run.Id, run.ProjectId, run.MilestoneId, run.Code, run.Name, run.Description,
                run.Status, RunMapping.CountOf(run.Cases),
                run.CreatedByUserId, run.CreatedAtUtc, run.ClosedAtUtc, run.ClosedByUserId,
                run.Environment, run.DueDateUtc);

        var refs = await _db.TestCaseRefs.AsNoTracking()
            .Where(x => x.WorkspaceId == cmd.WorkspaceId && newIds.Contains(x.Id))
            .ToListAsync(ct);

        foreach (var r in refs)
            run.Cases.Add(RunCase.Snapshot(
                run.Id, r.Id, r.Code, r.Title, r.Version, null, r.AutomationScenarioPath,
                r.AutomationEngine, r.AutomationStatus, r.AutomationDefinitionVersion, r.AutomationRecordingJson));

        await _db.SaveChangesAsync(ct);
        return new RunDto(run.Id, run.ProjectId, run.MilestoneId, run.Code, run.Name, run.Description,
            run.Status, RunMapping.CountOf(run.Cases),
            run.CreatedByUserId, run.CreatedAtUtc, run.ClosedAtUtc, run.ClosedByUserId,
            run.Environment, run.DueDateUtc);
    }
}

// ─── Remove Case from Run ─────────────────────────────────────────────────
public sealed record RemoveRunCaseCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid RunId,
    Guid RunCaseId) : IRequest<Result>;

public sealed class RemoveRunCaseHandler : IRequestHandler<RemoveRunCaseCommand, Result>
{
    private readonly RunsDbContext _db;
    public RemoveRunCaseHandler(RunsDbContext db) => _db = db;

    public async Task<Result> Handle(RemoveRunCaseCommand cmd, CancellationToken ct)
    {
        var run = await _db.Runs.Include(x => x.Cases)
            .FirstOrDefaultAsync(x => x.Id == cmd.RunId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (run is null) return Result.Failure(RunErrors.RunNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                run.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure(RunErrors.ProjectAccessDenied);
        if (run.IsDeleted) return Result.Failure(RunErrors.RunDeleted);
        if (run.Status == RunStatus.Closed) return Result.Failure(RunErrors.RunClosed);

        var rc = run.Cases.FirstOrDefault(c => c.Id == cmd.RunCaseId);
        if (rc is null) return Result.Failure(RunErrors.RunCaseNotFound);

        run.Cases.Remove(rc);
        _db.RunCases.Remove(rc);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ─── Bulk Record Results ──────────────────────────────────────────────────
public sealed record BulkRecordResultCommand(
    Guid WorkspaceId,
    Guid ExecutedByUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    IReadOnlyList<Guid> RunCaseIds,
    ResultStatus Status,
    string? Comment) : IRequest<Result<BulkRecordResultResponse>>;

public sealed record BulkRecordResultResponse(int Succeeded, int Failed);

public sealed class BulkRecordResultHandler : IRequestHandler<BulkRecordResultCommand, Result<BulkRecordResultResponse>>
{
    private readonly RunsDbContext _db;
    private readonly IPublishEndpoint _bus;
    public BulkRecordResultHandler(RunsDbContext db, IPublishEndpoint bus) { _db = db; _bus = bus; }

    public async Task<Result<BulkRecordResultResponse>> Handle(BulkRecordResultCommand cmd, CancellationToken ct)
    {
        var ids = cmd.RunCaseIds.Distinct().ToList();
        var visibility = await ProjectAccessPolicy.GetVisibilityAsync(
            _db.Database.GetDbConnection(),
            cmd.WorkspaceId,
            cmd.ExecutedByUserId,
            cmd.CurrentUserAccessScope,
            cmd.CurrentUserRole,
            ct);
        var runCases = await _db.RunCases
            .Where(x => ids.Contains(x.Id))
            .Join(_db.Runs, c => c.RunId, r => r.Id, (c, r) => new { Case = c, Run = r })
            .Where(x => x.Run.WorkspaceId == cmd.WorkspaceId && x.Run.Status == RunStatus.Open && x.Run.DeletedAtUtc == null)
            .ToListAsync(ct);
        runCases = runCases.Where(x => visibility.Contains(x.Run.ProjectId)).ToList();

        foreach (var projectId in runCases.Select(x => x.Run.ProjectId).Distinct())
        {
            if (!await ProjectAccessPolicy.CanMutateProjectAsync(
                    _db.Database.GetDbConnection(),
                    cmd.WorkspaceId,
                    cmd.ExecutedByUserId,
                    cmd.CurrentUserAccessScope,
                    cmd.CurrentUserRole,
                    projectId,
                    ct))
                return Result.Failure<BulkRecordResultResponse>(RunErrors.ProjectAccessDenied);
        }

        int succeeded = 0, failed = 0;
        foreach (var rc in runCases)
        {
            try
            {
                var result = rc.Case.RecordResult(cmd.Status, cmd.Comment, null, cmd.ExecutedByUserId);
                _db.RunResults.Add(result);
                succeeded++;

                await _bus.Publish(new TestResultRecorded(
                    rc.Run.WorkspaceId, rc.Run.ProjectId, rc.Run.Id,
                    rc.Case.Id, result.Id, rc.Case.TestCaseId, cmd.Status.ToString(),
                    cmd.ExecutedByUserId, result.ExecutedAtUtc), ct);
            }
            catch { failed++; }
        }

        await _db.SaveChangesAsync(ct);
        return new BulkRecordResultResponse(succeeded, failed);
    }
}
