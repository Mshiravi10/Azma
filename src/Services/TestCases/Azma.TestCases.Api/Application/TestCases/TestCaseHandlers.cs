using System.Text.Json;
using Azma.Auth.ProjectAccess;
using Azma.Automation;
using Azma.Common.Pagination;
using Azma.Common.Results;
using Azma.EventBus.Contracts.TestCases;
using Azma.TestCases.Api.Application.Common;
using Azma.TestCases.Api.Domain;
using Azma.TestCases.Api.Infrastructure.Persistence;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.TestCases.Api.Application.TestCases;

internal static class TestCaseMapping
{
    public static TestCaseDto Map(TestCase tc) => new(
        tc.Id, tc.ProjectId, tc.SuiteId, tc.SectionId,
        tc.Code, tc.Title, tc.Preconditions,
        HasAutomation(tc), tc.AutomationEngine, tc.AutomationStatus,
        tc.AutomationDefinitionVersion, tc.AutomationScenarioPath,
        RecordedScenarioValidator.MaskForDisplay(tc.AutomationRecordingJson),
        tc.Type, tc.Priority, tc.Status, tc.Version,
        tc.Tags.Select(x => x.Tag).OrderBy(x => x).ToList(),
        tc.Steps.OrderBy(x => x.Order).Select(s => new TestStepDto(s.Order, s.Action, s.ExpectedResult)).ToList(),
        tc.CreatedByUserId, tc.CreatedAtUtc, tc.UpdatedAtUtc,
        tc.LastExecutedAtUtc, tc.LastExecutionStatus, tc.LastExecutionRunId,
        tc.LastExecutionRunCaseId, tc.LastExecutedByUserId,
        tc.CustomFields ?? new Dictionary<string, JsonElement>(),
        tc.AutomationPrerequisiteTestCaseId);

    public static TestCaseSummaryDto MapSummary(TestCase tc) => new(
        tc.Id, tc.ProjectId, tc.SuiteId, tc.SectionId,
        tc.Code, tc.Title,
        HasAutomation(tc), tc.AutomationEngine, tc.AutomationStatus,
        tc.AutomationDefinitionVersion, tc.AutomationScenarioPath,
        tc.Type, tc.Priority, tc.Status, tc.Version,
        tc.Tags.Select(x => x.Tag).OrderBy(x => x).ToList(),
        tc.CreatedByUserId, tc.CreatedAtUtc, tc.UpdatedAtUtc,
        tc.LastExecutedAtUtc, tc.LastExecutionStatus, tc.LastExecutionRunId,
        tc.LastExecutionRunCaseId, tc.LastExecutedByUserId);

    public static string SnapshotOf(TestCase tc) => JsonSerializer.Serialize(Map(tc));

    private static bool HasAutomation(TestCase tc) =>
        !string.IsNullOrWhiteSpace(tc.AutomationScenarioPath) ||
        (tc.AutomationEngine == AutomationEngine.ScenarioRecording &&
         tc.AutomationStatus == AutomationStatus.Published &&
         !string.IsNullOrWhiteSpace(tc.AutomationRecordingJson));
}

public enum TestCaseExecutionState
{
    Never = 0,
    Stale = 1,
    Fresh = 2,
    NeedsRun = 3,
}

// ─── Create ──────────────────────────────────────────────────────────────
public sealed record CreateTestCaseStepInput(string Action, string? ExpectedResult);

public sealed record CreateTestCaseCommand(
    Guid WorkspaceId,
    Guid CreatedByUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ProjectId,
    Guid? SuiteId,
    Guid? SectionId,
    string Title,
    string? Preconditions,
    TestCaseType Type,
    TestCasePriority Priority,
    TestCaseStatus? Status,
    IReadOnlyList<string>? Tags,
    IReadOnlyList<CreateTestCaseStepInput>? Steps,
    string? AutomationScenarioPath,
    Dictionary<string, JsonElement>? CustomFields) : IRequest<Result<TestCaseDto>>;

public sealed class CreateTestCaseValidator : AbstractValidator<CreateTestCaseCommand>
{
    public CreateTestCaseValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Preconditions).MaximumLength(4000);
        RuleFor(x => x.AutomationScenarioPath).MaximumLength(1000);
        RuleForEach(x => x.Tags!).MaximumLength(48).When(x => x.Tags is not null);
        RuleForEach(x => x.Steps!).ChildRules(s =>
        {
            s.RuleFor(x => x.Action).NotEmpty().MaximumLength(2000);
            s.RuleFor(x => x.ExpectedResult).MaximumLength(2000);
        }).When(x => x.Steps is not null);
    }
}

public sealed class CreateTestCaseHandler : IRequestHandler<CreateTestCaseCommand, Result<TestCaseDto>>
{
    private readonly TestCasesDbContext _db;
    private readonly IPublishEndpoint _bus;
    public CreateTestCaseHandler(TestCasesDbContext db, IPublishEndpoint bus) { _db = db; _bus = bus; }

    public async Task<Result<TestCaseDto>> Handle(CreateTestCaseCommand cmd, CancellationToken ct)
    {
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CreatedByUserId,
                cmd.CurrentUserAccessScope,
                cmd.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<TestCaseDto>(TestCaseErrors.ProjectAccessDenied);

        // Lock the project_ref row to atomically allocate a sequence number.
        // (EF doesn't expose UPDLOCK directly; raw SQL FOR UPDATE here.)
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var projectRef = await _db.ProjectRefs
            .FromSqlInterpolated($"""
                SELECT * FROM testcases.project_refs
                WHERE "Id" = {cmd.ProjectId} AND "WorkspaceId" = {cmd.WorkspaceId}
                FOR UPDATE
                """)
            .AsTracking()
            .FirstOrDefaultAsync(ct);

        if (projectRef is null)
        {
            await ProjectRefSynchronizer.EnsureFromProjectsAsync(_db, cmd.WorkspaceId, cmd.ProjectId, ct);

            projectRef = await _db.ProjectRefs
                .FromSqlInterpolated($"""
                    SELECT * FROM testcases.project_refs
                    WHERE "Id" = {cmd.ProjectId} AND "WorkspaceId" = {cmd.WorkspaceId}
                    FOR UPDATE
                    """)
                .AsTracking()
                .FirstOrDefaultAsync(ct);
        }

        if (projectRef is null) return Result.Failure<TestCaseDto>(TestCaseErrors.ProjectNotKnown);
        if (projectRef.IsArchived) return Result.Failure<TestCaseDto>(TestCaseErrors.ProjectArchived);

        var seq = projectRef.AllocateSequence();
        var code = $"{projectRef.Code}-T-{seq:D3}";

        var tc = TestCase.Create(
            cmd.WorkspaceId, cmd.ProjectId, cmd.SuiteId, cmd.SectionId,
            code, seq, cmd.Title, cmd.Preconditions, cmd.Type, cmd.Priority, cmd.CreatedByUserId,
            cmd.Status ?? TestCaseStatus.Active,
            cmd.AutomationScenarioPath);

        if (cmd.Tags is { Count: > 0 })
        {
            foreach (var t in cmd.Tags
                         .Select(x => x?.Trim().ToLowerInvariant())
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Distinct()!)
            {
                tc.Tags.Add(TestCaseTag.Create(tc.Id, t!));
            }
        }

        if (cmd.Steps is { Count: > 0 })
        {
            var order = 1;
            foreach (var s in cmd.Steps)
                tc.Steps.Add(TestStep.Create(tc.Id, order++, s.Action, s.ExpectedResult));
        }

        _db.TestCases.Add(tc);

        if (cmd.CustomFields is { Count: > 0 })
        {
            var (ok, error, sanitized) = await CustomFieldValidator.ValidateAsync(
                _db, cmd.WorkspaceId, cmd.ProjectId, cmd.CustomFields, ct);
            if (!ok) return Result.Failure<TestCaseDto>(TestCaseErrors.CustomFieldValidation(error!));
            tc.SetCustomFields(sanitized);
        }

        // Initial version snapshot (added in same transaction)
        var initialVersion = TestCaseVersion.Create(tc.Id, tc.Version, cmd.CreatedByUserId,
            TestCaseMapping.SnapshotOf(tc), "created");
        _db.TestCaseVersions.Add(initialVersion);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _bus.Publish(new TestCaseCreated(
            tc.WorkspaceId, tc.ProjectId, tc.Id, tc.Code, tc.Title, tc.AutomationScenarioPath,
            tc.AutomationEngine.ToString(), tc.AutomationStatus.ToString(),
            tc.AutomationDefinitionVersion, tc.AutomationRecordingJson), ct);

        return TestCaseMapping.Map(tc);
    }
}

// ─── Update ──────────────────────────────────────────────────────────────
public sealed record UpdateTestCaseCommand(
    Guid WorkspaceId,
    Guid UpdatedByUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid TestCaseId,
    Guid? SuiteId,
    Guid? SectionId,
    string Title,
    string? Preconditions,
    TestCaseType Type,
    TestCasePriority Priority,
    TestCaseStatus Status,
    string? ChangeReason,
    string? AutomationScenarioPath,
    Dictionary<string, JsonElement>? CustomFields) : IRequest<Result<TestCaseDto>>;

public sealed class UpdateTestCaseValidator : AbstractValidator<UpdateTestCaseCommand>
{
    public UpdateTestCaseValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Preconditions).MaximumLength(4000);
        RuleFor(x => x.AutomationScenarioPath).MaximumLength(1000);
        RuleFor(x => x.ChangeReason).MaximumLength(500);
    }
}

public sealed class UpdateTestCaseHandler : IRequestHandler<UpdateTestCaseCommand, Result<TestCaseDto>>
{
    private readonly TestCasesDbContext _db;
    private readonly IPublishEndpoint _bus;
    public UpdateTestCaseHandler(TestCasesDbContext db, IPublishEndpoint bus) { _db = db; _bus = bus; }

    public async Task<Result<TestCaseDto>> Handle(UpdateTestCaseCommand cmd, CancellationToken ct)
    {
        var tc = await _db.TestCases
            .Include(x => x.Steps)
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == cmd.TestCaseId && x.WorkspaceId == cmd.WorkspaceId, ct);

        if (tc is null) return Result.Failure<TestCaseDto>(TestCaseErrors.TestCaseNotFound);
        if (tc.IsDeleted) return Result.Failure<TestCaseDto>(TestCaseErrors.TestCaseDeleted);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.UpdatedByUserId,
                cmd.CurrentUserAccessScope,
                tc.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<TestCaseDto>(TestCaseErrors.ProjectAccessDenied);

        tc.Update(cmd.Title, cmd.Preconditions, cmd.Type, cmd.Priority, cmd.Status,
                  cmd.SuiteId, cmd.SectionId, cmd.UpdatedByUserId, cmd.AutomationScenarioPath);

        if (cmd.CustomFields is not null)
        {
            var (ok, error, sanitized) = await CustomFieldValidator.ValidateAsync(
                _db, cmd.WorkspaceId, tc.ProjectId, cmd.CustomFields, ct);
            if (!ok) return Result.Failure<TestCaseDto>(TestCaseErrors.CustomFieldValidation(error!));
            tc.SetCustomFields(sanitized);
        }

        _db.TestCaseVersions.Add(TestCaseVersion.Create(tc.Id, tc.Version, cmd.UpdatedByUserId,
            TestCaseMapping.SnapshotOf(tc), cmd.ChangeReason));

        await _db.SaveChangesAsync(ct);

        await _bus.Publish(new TestCaseUpdated(
            tc.WorkspaceId, tc.ProjectId, tc.Id, tc.Code, tc.Title, tc.Version, tc.AutomationScenarioPath,
            tc.AutomationEngine.ToString(), tc.AutomationStatus.ToString(),
            tc.AutomationDefinitionVersion, tc.AutomationRecordingJson), ct);
        return TestCaseMapping.Map(tc);
    }
}

// ─── Delete (soft) ───────────────────────────────────────────────────────
public sealed record DeleteTestCaseCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid TestCaseId) : IRequest<Result>;

public sealed class DeleteTestCaseHandler : IRequestHandler<DeleteTestCaseCommand, Result>
{
    private readonly TestCasesDbContext _db;
    private readonly IPublishEndpoint _bus;
    public DeleteTestCaseHandler(TestCasesDbContext db, IPublishEndpoint bus) { _db = db; _bus = bus; }

    public async Task<Result> Handle(DeleteTestCaseCommand cmd, CancellationToken ct)
    {
        var tc = await _db.TestCases.FirstOrDefaultAsync(
            x => x.Id == cmd.TestCaseId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (tc is null) return Result.Failure(TestCaseErrors.TestCaseNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                tc.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure(TestCaseErrors.ProjectAccessDenied);
        if (tc.IsDeleted) return Result.Success();

        tc.SoftDelete();
        await _db.SaveChangesAsync(ct);
        await _bus.Publish(new TestCaseDeleted(tc.WorkspaceId, tc.ProjectId, tc.Id), ct);
        return Result.Success();
    }
}

// ─── Get ─────────────────────────────────────────────────────────────────
public sealed record GetTestCaseQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid TestCaseId) : IRequest<Result<TestCaseDto>>;

public sealed class GetTestCaseHandler : IRequestHandler<GetTestCaseQuery, Result<TestCaseDto>>
{
    private readonly TestCasesDbContext _db;
    public GetTestCaseHandler(TestCasesDbContext db) => _db = db;

    public async Task<Result<TestCaseDto>> Handle(GetTestCaseQuery q, CancellationToken ct)
    {
        var tc = await _db.TestCases.AsNoTracking()
            .Include(x => x.Steps)
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == q.TestCaseId && x.WorkspaceId == q.WorkspaceId, ct);
        if (tc is null) return Result.Failure<TestCaseDto>(TestCaseErrors.TestCaseNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                q.WorkspaceId,
                q.CurrentUserId,
                q.CurrentUserAccessScope,
                tc.ProjectId,
                ct,
                q.CurrentUserRole))
            return Result.Failure<TestCaseDto>(TestCaseErrors.ProjectAccessDenied);
        return TestCaseMapping.Map(tc);
    }
}

// ─── List (paged + filters) ──────────────────────────────────────────────
public sealed record ListTestCasesQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid? ProjectId,
    Guid? SuiteId,
    Guid? SectionId,
    string? Search,
    string? Tag,
    TestCaseStatus? Status,
    TestCasePriority? Priority,
    bool? HasAutomation,
    AutomationEngine? AutomationEngine,
    TestCaseExecutionState? ExecutionState,
    int StaleDays,
    bool IncludeDeleted,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<TestCaseSummaryDto>>>;

public sealed class ListTestCasesHandler : IRequestHandler<ListTestCasesQuery, Result<PagedResult<TestCaseSummaryDto>>>
{
    private readonly TestCasesDbContext _db;
    public ListTestCasesHandler(TestCasesDbContext db) => _db = db;

    public async Task<Result<PagedResult<TestCaseSummaryDto>>> Handle(ListTestCasesQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);

        var query = _db.TestCases.AsNoTracking()
            .Where(x => x.WorkspaceId == q.WorkspaceId);

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
        if (q.SuiteId is { } sid) query = query.Where(x => x.SuiteId == sid);
        if (q.SectionId is { } secId) query = query.Where(x => x.SectionId == secId);
        if (q.Status is { } st) query = query.Where(x => x.Status == st);
        if (q.Priority is { } pr) query = query.Where(x => x.Priority == pr);
        if (q.AutomationEngine is { } engine) query = query.Where(x => x.AutomationEngine == engine);
        if (q.HasAutomation is { } hasAutomation)
        {
            query = hasAutomation
                ? query.Where(x =>
                    (x.AutomationScenarioPath != null && x.AutomationScenarioPath.Trim() != "") ||
                    (x.AutomationEngine == AutomationEngine.ScenarioRecording &&
                     x.AutomationStatus == AutomationStatus.Published &&
                     x.AutomationRecordingJson != null))
                : query.Where(x =>
                    (x.AutomationScenarioPath == null || x.AutomationScenarioPath.Trim() == "") &&
                    !(x.AutomationEngine == AutomationEngine.ScenarioRecording &&
                      x.AutomationStatus == AutomationStatus.Published &&
                      x.AutomationRecordingJson != null));
        }
        if (q.ExecutionState is { } executionState)
        {
            var staleDays = Math.Clamp(q.StaleDays <= 0 ? 14 : q.StaleDays, 1, 365);
            var cutoff = DateTime.UtcNow.AddDays(-staleDays);
            query = executionState switch
            {
                TestCaseExecutionState.Never => query.Where(x => x.LastExecutedAtUtc == null),
                TestCaseExecutionState.Stale => query.Where(x => x.LastExecutedAtUtc != null && x.LastExecutedAtUtc < cutoff),
                TestCaseExecutionState.Fresh => query.Where(x => x.LastExecutedAtUtc != null && x.LastExecutedAtUtc >= cutoff),
                TestCaseExecutionState.NeedsRun => query.Where(x => x.LastExecutedAtUtc == null || x.LastExecutedAtUtc < cutoff),
                _ => query
            };
        }
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var like = $"%{q.Search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Title, like) ||
                EF.Functions.ILike(x.Code, like));
        }
        if (!string.IsNullOrWhiteSpace(q.Tag))
        {
            var tag = q.Tag.Trim().ToLowerInvariant();
            query = query.Where(x => x.Tags.Any(t => t.Tag == tag));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .Include(x => x.Tags)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);

        var dtos = items.Select(TestCaseMapping.MapSummary).ToList();
        return new PagedResult<TestCaseSummaryDto>(dtos, page, size, total);
    }
}

// ─── Replace Steps ───────────────────────────────────────────────────────
public sealed record ReplaceStepsCommand(
    Guid WorkspaceId,
    Guid UpdatedByUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid TestCaseId,
    IReadOnlyList<CreateTestCaseStepInput> Steps) : IRequest<Result<TestCaseDto>>;

public sealed class ReplaceStepsValidator : AbstractValidator<ReplaceStepsCommand>
{
    public ReplaceStepsValidator()
    {
        RuleForEach(x => x.Steps).ChildRules(s =>
        {
            s.RuleFor(x => x.Action).NotEmpty().MaximumLength(2000);
            s.RuleFor(x => x.ExpectedResult).MaximumLength(2000);
        });
    }
}

public sealed class ReplaceStepsHandler : IRequestHandler<ReplaceStepsCommand, Result<TestCaseDto>>
{
    private readonly TestCasesDbContext _db;
    private readonly IPublishEndpoint _bus;
    public ReplaceStepsHandler(TestCasesDbContext db, IPublishEndpoint bus) { _db = db; _bus = bus; }

    public async Task<Result<TestCaseDto>> Handle(ReplaceStepsCommand cmd, CancellationToken ct)
    {
        var tc = await _db.TestCases
            .Include(x => x.Steps)
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == cmd.TestCaseId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (tc is null) return Result.Failure<TestCaseDto>(TestCaseErrors.TestCaseNotFound);
        if (tc.IsDeleted) return Result.Failure<TestCaseDto>(TestCaseErrors.TestCaseDeleted);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.UpdatedByUserId,
                cmd.CurrentUserAccessScope,
                tc.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<TestCaseDto>(TestCaseErrors.ProjectAccessDenied);

        var oldSteps = tc.Steps.ToList();
        foreach (var s in oldSteps) tc.Steps.Remove(s);
        _db.TestSteps.RemoveRange(oldSteps);

        var order = 1;
        foreach (var s in cmd.Steps)
            tc.Steps.Add(TestStep.Create(tc.Id, order++, s.Action, s.ExpectedResult));

        // bump version + snapshot
        tc.Update(tc.Title, tc.Preconditions, tc.Type, tc.Priority, tc.Status,
                  tc.SuiteId, tc.SectionId, cmd.UpdatedByUserId, tc.AutomationScenarioPath);

        _db.TestCaseVersions.Add(TestCaseVersion.Create(tc.Id, tc.Version, cmd.UpdatedByUserId,
            TestCaseMapping.SnapshotOf(tc), "steps-replaced"));

        await _db.SaveChangesAsync(ct);

        await _bus.Publish(new TestCaseUpdated(
            tc.WorkspaceId, tc.ProjectId, tc.Id, tc.Code, tc.Title, tc.Version, tc.AutomationScenarioPath,
            tc.AutomationEngine.ToString(), tc.AutomationStatus.ToString(),
            tc.AutomationDefinitionVersion, tc.AutomationRecordingJson), ct);
        return TestCaseMapping.Map(tc);
    }
}

// --- Scenario-level automation recording ---
public sealed record StartAutomationRecordingCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid TestCaseId) : IRequest<Result<AutomationRecordingSessionDto>>;

public sealed class StartAutomationRecordingHandler
    : IRequestHandler<StartAutomationRecordingCommand, Result<AutomationRecordingSessionDto>>
{
    private readonly TestCasesDbContext _db;
    public StartAutomationRecordingHandler(TestCasesDbContext db) => _db = db;

    public async Task<Result<AutomationRecordingSessionDto>> Handle(StartAutomationRecordingCommand cmd, CancellationToken ct)
    {
        var tc = await _db.TestCases.FirstOrDefaultAsync(
            x => x.Id == cmd.TestCaseId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (tc is null) return Result.Failure<AutomationRecordingSessionDto>(TestCaseErrors.TestCaseNotFound);
        if (tc.IsDeleted) return Result.Failure<AutomationRecordingSessionDto>(TestCaseErrors.TestCaseDeleted);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                tc.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<AutomationRecordingSessionDto>(TestCaseErrors.ProjectAccessDenied);

        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "", StringComparison.Ordinal)
            .Replace("/", "", StringComparison.Ordinal)
            .Replace("=", "", StringComparison.Ordinal);
        var expiresAt = DateTime.UtcNow.AddHours(2);
        var session = AutomationRecordingSession.Start(
            cmd.WorkspaceId, tc.ProjectId, tc.Id, cmd.CurrentUserId, token, expiresAt);
        _db.AutomationRecordingSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return new AutomationRecordingSessionDto(session.Id, token, expiresAt);
    }
}

public sealed record AppendAutomationRecordingEventsCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid TestCaseId,
    Guid SessionId,
    string SessionToken,
    IReadOnlyList<JsonElement> Events) : IRequest<Result>;

internal static class AutomationRecordingEventGuard
{
    private const int MaxBatchSize = 100;
    // A V2 scenario snapshot contains the editable human layer and its ordered
    // executable layer. Keep it bounded, but allow a realistic studio session.
    private const int MaxEventChars = 2 * 1024 * 1024;
    private const int MaxBatchChars = 4 * 1024 * 1024;
    private const int MaxSessionChars = 12 * 1024 * 1024;

    public static Result ValidateAppend(AutomationRecordingSession session, string sessionToken, IReadOnlyList<JsonElement> events)
    {
        if (!session.TokenMatches(sessionToken) ||
            session.Status != AutomationRecordingSessionStatus.Open ||
            session.IsExpired(DateTime.UtcNow))
            return Result.Failure(TestCaseErrors.AutomationRecordingSessionInvalid);

        if (events.Count == 0)
            return Result.Success();

        if (events.Count > MaxBatchSize)
            return Result.Failure(TestCaseErrors.AutomationValidation(
                $"Recording event batch cannot contain more than {MaxBatchSize} events."));

        var totalChars = 0;
        foreach (var item in events)
        {
            var raw = item.GetRawText();
            if (raw.Length > MaxEventChars)
                return Result.Failure(TestCaseErrors.AutomationValidation(
                    $"Recording event cannot be larger than {MaxEventChars} characters."));

            totalChars += raw.Length;
            if (totalChars > MaxBatchChars)
                return Result.Failure(TestCaseErrors.AutomationValidation(
                    $"Recording event batch cannot be larger than {MaxBatchChars} characters."));
        }

        if ((session.EventsJson?.Length ?? 0) + totalChars > MaxSessionChars)
            return Result.Failure(TestCaseErrors.AutomationValidation(
                $"Recording session cannot be larger than {MaxSessionChars} characters."));

        return Result.Success();
    }
}

public sealed class AppendAutomationRecordingEventsHandler
    : IRequestHandler<AppendAutomationRecordingEventsCommand, Result>
{
    private readonly TestCasesDbContext _db;
    public AppendAutomationRecordingEventsHandler(TestCasesDbContext db) => _db = db;

    public async Task<Result> Handle(AppendAutomationRecordingEventsCommand cmd, CancellationToken ct)
    {
        var session = await _db.AutomationRecordingSessions.FirstOrDefaultAsync(
            x => x.Id == cmd.SessionId &&
                 x.TestCaseId == cmd.TestCaseId &&
                 x.WorkspaceId == cmd.WorkspaceId, ct);
        if (session is null) return Result.Failure(TestCaseErrors.AutomationRecordingSessionNotFound);

        var appendValidation = AutomationRecordingEventGuard.ValidateAppend(session, cmd.SessionToken, cmd.Events);
        if (appendValidation.IsFailure)
            return appendValidation;

        var projectId = await _db.TestCases.AsNoTracking()
            .Where(x => x.Id == cmd.TestCaseId && x.WorkspaceId == cmd.WorkspaceId)
            .Select(x => (Guid?)x.ProjectId)
            .FirstOrDefaultAsync(ct);
        if (projectId is null) return Result.Failure(TestCaseErrors.TestCaseNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                projectId.Value,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure(TestCaseErrors.ProjectAccessDenied);

        session.AppendEvents(cmd.Events);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record AppendAutomationRecordingEventsByTokenCommand(
    Guid SessionId,
    string SessionToken,
    IReadOnlyList<JsonElement> Events) : IRequest<Result>;

public sealed class AppendAutomationRecordingEventsByTokenHandler
    : IRequestHandler<AppendAutomationRecordingEventsByTokenCommand, Result>
{
    private readonly TestCasesDbContext _db;
    public AppendAutomationRecordingEventsByTokenHandler(TestCasesDbContext db) => _db = db;

    public async Task<Result> Handle(AppendAutomationRecordingEventsByTokenCommand cmd, CancellationToken ct)
    {
        var session = await _db.AutomationRecordingSessions.FirstOrDefaultAsync(
            x => x.Id == cmd.SessionId, ct);
        if (session is null) return Result.Failure(TestCaseErrors.AutomationRecordingSessionNotFound);

        var appendValidation = AutomationRecordingEventGuard.ValidateAppend(session, cmd.SessionToken, cmd.Events);
        if (appendValidation.IsFailure)
            return appendValidation;

        session.AppendEvents(cmd.Events);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record FinishAutomationRecordingCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid TestCaseId,
    Guid SessionId,
    string SessionToken,
    RecordedScenarioDefinition? Definition) : IRequest<Result<TestCaseAutomationDto>>;

public sealed class FinishAutomationRecordingHandler
    : IRequestHandler<FinishAutomationRecordingCommand, Result<TestCaseAutomationDto>>
{
    private readonly TestCasesDbContext _db;
    private readonly IPublishEndpoint _bus;
    public FinishAutomationRecordingHandler(TestCasesDbContext db, IPublishEndpoint bus)
    {
        _db = db;
        _bus = bus;
    }

    public async Task<Result<TestCaseAutomationDto>> Handle(FinishAutomationRecordingCommand cmd, CancellationToken ct)
    {
        var tc = await _db.TestCases
            .Include(x => x.Steps)
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == cmd.TestCaseId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (tc is null) return Result.Failure<TestCaseAutomationDto>(TestCaseErrors.TestCaseNotFound);
        if (tc.IsDeleted) return Result.Failure<TestCaseAutomationDto>(TestCaseErrors.TestCaseDeleted);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                tc.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<TestCaseAutomationDto>(TestCaseErrors.ProjectAccessDenied);

        var session = await _db.AutomationRecordingSessions.FirstOrDefaultAsync(
            x => x.Id == cmd.SessionId &&
                 x.TestCaseId == tc.Id &&
                 x.WorkspaceId == cmd.WorkspaceId, ct);
        if (session is null) return Result.Failure<TestCaseAutomationDto>(TestCaseErrors.AutomationRecordingSessionNotFound);
        if (!session.TokenMatches(cmd.SessionToken) ||
            session.Status != AutomationRecordingSessionStatus.Open ||
            session.IsExpired(DateTime.UtcNow))
            return Result.Failure<TestCaseAutomationDto>(TestCaseErrors.AutomationRecordingSessionInvalid);

        return await FinishAutomationRecordingWorkflow.FinishAsync(
            _db, _bus, tc, session, cmd.CurrentUserId, cmd.Definition, ct);
    }

    internal static bool HasMeaningfulDefinition(RecordedScenarioDefinition? definition) =>
        definition is not null &&
        ((definition.Actions?.Count ?? 0) > 0 ||
         (definition.NetworkCaptures?.Count ?? 0) > 0 ||
         (definition.Expectations?.Count ?? 0) > 0 ||
         (definition.Steps?.Count ?? 0) > 0);

    internal static RecordedScenarioDefinition BuildDefinitionFromEvents(string title, string eventsJson)
    {
        var actions = new List<RecordedScenarioAction>();
        var network = new List<RecordedNetworkCapture>();
        var expectations = new List<RecordedExpectation>();
        var steps = new List<RecordedScenarioStep>();
        string? startUrl = null;

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(eventsJson) ? "[]" : eventsJson);

        foreach (var snapshot in document.RootElement.EnumerateArray().Reverse())
        {
            var snapshotType = snapshot.TryGetProperty("type", out var snapshotTypeElement)
                ? snapshotTypeElement.GetString()
                : null;
            if (!string.Equals(snapshotType, "scenarioSnapshot", StringComparison.OrdinalIgnoreCase) ||
                !snapshot.TryGetProperty("definition", out var definitionElement))
                continue;

            try
            {
                var snapshotDefinition = definitionElement.Deserialize<RecordedScenarioDefinition>(
                    RecordedScenarioDefinition.JsonOptions);
                if (HasMeaningfulDefinition(snapshotDefinition))
                    return snapshotDefinition!;
            }
            catch (JsonException)
            {
                // Continue with the raw event stream when a partial snapshot is corrupt.
            }
        }

        foreach (var item in document.RootElement.EnumerateArray())
        {
            var type = item.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;

            if (string.Equals(type, "action", StringComparison.OrdinalIgnoreCase))
            {
                var actionType = ParseEnum(item, "actionType", RecordedActionType.Wait);
                var url = GetString(item, "url");
                if (actionType == RecordedActionType.Navigate && string.IsNullOrWhiteSpace(startUrl))
                    startUrl = url;

                var action = new RecordedScenarioAction(
                    actionType,
                    url,
                    GetString(item, "selector"),
                    GetString(item, "text"),
                    GetString(item, "value"),
                    GetInt(item, "delayMs"),
                    GetString(item, "label"),
                    GetStringArray(item, "selectorAlternates"),
                    GetBool(item, "sensitive"),
                    GetString(item, "selectorStrategy"),
                    GetInt(item, "selectorConfidence"));
                actions.Add(action);
                steps.Add(new RecordedScenarioStep(
                    GetString(item, "stepId") ?? GetString(item, "eventId") ?? Guid.NewGuid().ToString("N"),
                    action.Label,
                    HumanAction(action),
                    null,
                    [action],
                    [],
                    [],
                    new RecordedStepEvidence(url ?? startUrl, null, DateTime.UtcNow)));
            }
            else if (string.Equals(type, "network", StringComparison.OrdinalIgnoreCase))
            {
                var capture = new RecordedNetworkCapture(
                    GetString(item, "method") ?? "GET",
                    GetString(item, "urlPattern") ?? GetString(item, "url") ?? "*",
                    GetInt(item, "expectedStatus") ?? GetInt(item, "statusCode"),
                    GetLong(item, "durationMs"),
                    RecordedScenarioValidator.MaskSensitiveText(GetString(item, "requestSummary")),
                    RecordedScenarioValidator.MaskSensitiveText(GetString(item, "responseSummary")));
                network.Add(capture);
                if (steps.Count > 0)
                {
                    var latest = steps[^1];
                    steps[^1] = latest with
                    {
                        NetworkCaptures = (latest.NetworkCaptures ?? []).Append(capture).ToList(),
                    };
                }
            }
            else if (string.Equals(type, "expectation", StringComparison.OrdinalIgnoreCase))
            {
                var expectation = new RecordedExpectation(
                    ParseEnum(item, "expectationType", RecordedExpectationType.VisibleText),
                    GetString(item, "selector"),
                    GetString(item, "text"),
                    GetString(item, "urlPattern"),
                    GetString(item, "method"),
                    GetInt(item, "statusCode"),
                    GetString(item, "jsonPath"),
                    GetString(item, "expectedValue"),
                    GetString(item, "bodyContains"),
                    GetString(item, "label"),
                    GetStringArray(item, "selectorAlternates"),
                    GetString(item, "selectorStrategy"),
                    GetInt(item, "selectorConfidence"));
                expectations.Add(expectation);
                if (steps.Count == 0)
                {
                    steps.Add(new RecordedScenarioStep(
                        GetString(item, "stepId") ?? GetString(item, "eventId") ?? Guid.NewGuid().ToString("N"),
                        expectation.Label,
                        "بررسی نتیجه",
                        HumanExpectation(expectation),
                        [],
                        [expectation]));
                }
                else
                {
                    var latest = steps[^1];
                    steps[^1] = latest with
                    {
                        ExpectedResult = JoinText(latest.ExpectedResult, HumanExpectation(expectation)),
                        Assertions = (latest.Assertions ?? []).Append(expectation).ToList(),
                    };
                }
            }
            else if (string.Equals(type, "session", StringComparison.OrdinalIgnoreCase))
            {
                startUrl ??= GetString(item, "startUrl") ?? GetString(item, "targetUrl");
            }
        }

        return new RecordedScenarioDefinition(
            title, startUrl, actions, network, expectations,
            Steps: steps, SchemaVersion: 2);
    }

    private static string HumanAction(RecordedScenarioAction action) => action.Type switch
    {
        RecordedActionType.Navigate => $"باز کردن {action.Url}",
        RecordedActionType.Click => $"کلیک روی {action.Label ?? action.Selector}",
        RecordedActionType.Fill => $"وارد کردن مقدار در {action.Label ?? action.Selector}",
        RecordedActionType.Select => $"انتخاب مقدار در {action.Label ?? action.Selector}",
        RecordedActionType.Submit => $"ارسال {action.Label ?? "فرم"}",
        RecordedActionType.Wait => $"انتظار به مدت {action.DelayMs ?? 0} میلی‌ثانیه",
        _ => action.Label ?? action.Type.ToString(),
    };

    private static string HumanExpectation(RecordedExpectation expectation) => expectation.Type switch
    {
        RecordedExpectationType.VisibleText => $"متن «{expectation.Text}» نمایش داده شود",
        RecordedExpectationType.ElementVisible => $"عنصر {expectation.Label ?? expectation.Selector} نمایش داده شود",
        RecordedExpectationType.UrlMatches => $"آدرس صفحه با {expectation.UrlPattern} مطابقت داشته باشد",
        RecordedExpectationType.ApiStatus => $"پاسخ API {expectation.UrlPattern} دارای وضعیت {expectation.StatusCode} باشد",
        RecordedExpectationType.ApiJsonPathEquals => $"مقدار {expectation.JsonPath} در پاسخ API برابر {expectation.ExpectedValue} باشد",
        RecordedExpectationType.ApiBodyContains => $"بدنه پاسخ API شامل {expectation.BodyContains} باشد",
        _ => expectation.Label ?? expectation.Type.ToString(),
    };

    private static string JoinText(string? current, string next) =>
        string.IsNullOrWhiteSpace(current) ? next : $"{current}؛ {next}";

    private static string? GetString(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    private static bool GetBool(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean();

    private static int? GetInt(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.TryGetInt32(out var number)
            ? number
            : null;

    private static long? GetLong(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.TryGetInt64(out var number)
            ? number
            : null;

    private static IReadOnlyList<string>? GetStringArray(JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.Array)
            return null;

        var result = value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return result.Count == 0 ? null : result;
    }

    private static TEnum ParseEnum<TEnum>(JsonElement item, string property, TEnum fallback)
        where TEnum : struct
    {
        var value = GetString(item, property);
        return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;
    }
}

public sealed record FinishAutomationRecordingByTokenCommand(
    Guid SessionId,
    string SessionToken,
    RecordedScenarioDefinition? Definition) : IRequest<Result<TestCaseAutomationDto>>;

public sealed class FinishAutomationRecordingByTokenHandler
    : IRequestHandler<FinishAutomationRecordingByTokenCommand, Result<TestCaseAutomationDto>>
{
    private readonly TestCasesDbContext _db;
    private readonly IPublishEndpoint _bus;

    public FinishAutomationRecordingByTokenHandler(TestCasesDbContext db, IPublishEndpoint bus)
    {
        _db = db;
        _bus = bus;
    }

    public async Task<Result<TestCaseAutomationDto>> Handle(
        FinishAutomationRecordingByTokenCommand cmd,
        CancellationToken ct)
    {
        var session = await _db.AutomationRecordingSessions.FirstOrDefaultAsync(
            x => x.Id == cmd.SessionId, ct);
        if (session is null)
            return Result.Failure<TestCaseAutomationDto>(TestCaseErrors.AutomationRecordingSessionNotFound);
        if (!session.TokenMatches(cmd.SessionToken) ||
            session.Status != AutomationRecordingSessionStatus.Open ||
            session.IsExpired(DateTime.UtcNow))
            return Result.Failure<TestCaseAutomationDto>(TestCaseErrors.AutomationRecordingSessionInvalid);

        var tc = await _db.TestCases
            .Include(x => x.Steps)
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x =>
                x.Id == session.TestCaseId &&
                x.WorkspaceId == session.WorkspaceId, ct);
        if (tc is null)
            return Result.Failure<TestCaseAutomationDto>(TestCaseErrors.TestCaseNotFound);
        if (tc.IsDeleted)
            return Result.Failure<TestCaseAutomationDto>(TestCaseErrors.TestCaseDeleted);

        return await FinishAutomationRecordingWorkflow.FinishAsync(
            _db, _bus, tc, session, session.CreatedByUserId, cmd.Definition, ct);
    }
}

internal static class FinishAutomationRecordingWorkflow
{
    public static async Task<Result<TestCaseAutomationDto>> FinishAsync(
        TestCasesDbContext db,
        IPublishEndpoint bus,
        TestCase tc,
        AutomationRecordingSession session,
        Guid updatedByUserId,
        RecordedScenarioDefinition? requestedDefinition,
        CancellationToken ct)
    {
        var definition = FinishAutomationRecordingHandler.HasMeaningfulDefinition(requestedDefinition)
            ? requestedDefinition!
            : FinishAutomationRecordingHandler.BuildDefinitionFromEvents(tc.Title, session.EventsJson);
        var validation = RecordedScenarioValidator.Validate(definition);
        if (!validation.IsValid || validation.SanitizedDefinition is null)
            return Result.Failure<TestCaseAutomationDto>(
                TestCaseErrors.AutomationValidation(string.Join("; ", validation.Errors)));

        var sanitized = validation.SanitizedDefinition;
        if (sanitized.Steps is not null)
        {
            var oldSteps = tc.Steps.ToList();
            foreach (var oldStep in oldSteps)
                tc.Steps.Remove(oldStep);
            db.TestSteps.RemoveRange(oldSteps);

            var order = 1;
            foreach (var step in sanitized.Steps)
            {
                var action = HumanStepAction(step);
                if (string.IsNullOrWhiteSpace(action))
                    continue;
                tc.Steps.Add(TestStep.Create(tc.Id, order++, action, step.ExpectedResult));
            }
        }

        tc.SaveRecordingDraft(sanitized.ToJson(), updatedByUserId);
        session.Finish();
        db.TestCaseVersions.Add(TestCaseVersion.Create(
            tc.Id, tc.Version, updatedByUserId, TestCaseMapping.SnapshotOf(tc), "automation-recording-draft"));
        await db.SaveChangesAsync(ct);

        await bus.Publish(new TestCaseUpdated(
            tc.WorkspaceId, tc.ProjectId, tc.Id, tc.Code, tc.Title, tc.Version, tc.AutomationScenarioPath,
            tc.AutomationEngine.ToString(), tc.AutomationStatus.ToString(),
            tc.AutomationDefinitionVersion, tc.AutomationRecordingJson), ct);

        return AutomationMapping.Map(tc, validation);
    }

    private static string? HumanStepAction(RecordedScenarioStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.ActionText))
            return step.ActionText.Trim();
        if (!string.IsNullOrWhiteSpace(step.Title))
            return step.Title.Trim();

        return step.Actions?.FirstOrDefault()?.Label;
    }
}

public sealed record ValidateAutomationCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid TestCaseId) : IRequest<Result<TestCaseAutomationDto>>;

public sealed class ValidateAutomationHandler
    : IRequestHandler<ValidateAutomationCommand, Result<TestCaseAutomationDto>>
{
    private readonly TestCasesDbContext _db;
    public ValidateAutomationHandler(TestCasesDbContext db) => _db = db;

    public async Task<Result<TestCaseAutomationDto>> Handle(ValidateAutomationCommand cmd, CancellationToken ct)
    {
        var tc = await _db.TestCases.FirstOrDefaultAsync(
            x => x.Id == cmd.TestCaseId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (tc is null) return Result.Failure<TestCaseAutomationDto>(TestCaseErrors.TestCaseNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                tc.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<TestCaseAutomationDto>(TestCaseErrors.ProjectAccessDenied);

        var validation = RecordedScenarioValidator.Validate(
            RecordedScenarioDefinition.FromJson(tc.AutomationRecordingJson));
        if (!validation.IsValid)
            return Result.Failure<TestCaseAutomationDto>(
                TestCaseErrors.AutomationValidation(string.Join("; ", validation.Errors)));

        tc.MarkRecordingValidated(cmd.CurrentUserId);
        await _db.SaveChangesAsync(ct);
        return AutomationMapping.Map(tc, validation);
    }
}

public sealed record PublishAutomationCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid TestCaseId) : IRequest<Result<TestCaseAutomationDto>>;

public sealed class PublishAutomationHandler
    : IRequestHandler<PublishAutomationCommand, Result<TestCaseAutomationDto>>
{
    private readonly TestCasesDbContext _db;
    private readonly IPublishEndpoint _bus;
    public PublishAutomationHandler(TestCasesDbContext db, IPublishEndpoint bus)
    {
        _db = db;
        _bus = bus;
    }

    public async Task<Result<TestCaseAutomationDto>> Handle(PublishAutomationCommand cmd, CancellationToken ct)
    {
        var tc = await _db.TestCases.FirstOrDefaultAsync(
            x => x.Id == cmd.TestCaseId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (tc is null) return Result.Failure<TestCaseAutomationDto>(TestCaseErrors.TestCaseNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                tc.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<TestCaseAutomationDto>(TestCaseErrors.ProjectAccessDenied);

        var validation = RecordedScenarioValidator.Validate(
            RecordedScenarioDefinition.FromJson(tc.AutomationRecordingJson));
        if (!validation.IsValid)
            return Result.Failure<TestCaseAutomationDto>(
                TestCaseErrors.AutomationValidation(string.Join("; ", validation.Errors)));

        tc.PublishRecording(cmd.CurrentUserId);
        await _db.SaveChangesAsync(ct);
        await _bus.Publish(new TestCaseUpdated(
            tc.WorkspaceId, tc.ProjectId, tc.Id, tc.Code, tc.Title, tc.Version, tc.AutomationScenarioPath,
            tc.AutomationEngine.ToString(), tc.AutomationStatus.ToString(),
            tc.AutomationDefinitionVersion, tc.AutomationRecordingJson), ct);
        return AutomationMapping.Map(tc, validation);
    }
}

public sealed record DryRunAutomationCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid TestCaseId,
    string? Gateway,
    string? Frontend) : IRequest<Result<Azma.Automation.AutomationRunResponse>>;

public sealed class DryRunAutomationHandler
    : IRequestHandler<DryRunAutomationCommand, Result<Azma.Automation.AutomationRunResponse>>
{
    private readonly TestCasesDbContext _db;
    private readonly IAutomationApiClient _automation;
    public DryRunAutomationHandler(TestCasesDbContext db, IAutomationApiClient automation)
    {
        _db = db;
        _automation = automation;
    }

    public async Task<Result<Azma.Automation.AutomationRunResponse>> Handle(DryRunAutomationCommand cmd, CancellationToken ct)
    {
        var tc = await _db.TestCases.FirstOrDefaultAsync(
            x => x.Id == cmd.TestCaseId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (tc is null) return Result.Failure<Azma.Automation.AutomationRunResponse>(TestCaseErrors.TestCaseNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                tc.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<Azma.Automation.AutomationRunResponse>(TestCaseErrors.ProjectAccessDenied);

        var definition = RecordedScenarioDefinition.FromJson(tc.AutomationRecordingJson);
        var validation = RecordedScenarioValidator.Validate(definition);
        if (!validation.IsValid || validation.SanitizedDefinition is null)
            return Result.Failure<Azma.Automation.AutomationRunResponse>(
                TestCaseErrors.AutomationValidation(string.Join("; ", validation.Errors)));

        var prerequisites = await AutomationPrerequisiteLoader.LoadAsync(
            _db, cmd.WorkspaceId, tc.ProjectId, tc.Id, tc.AutomationPrerequisiteTestCaseId, ct);
        if (prerequisites.IsFailure)
            return Result.Failure<Azma.Automation.AutomationRunResponse>(prerequisites.Error);

        var response = await _automation.RunAsync(
            new AutomationRunRequest(
                cmd.WorkspaceId,
                tc.ProjectId,
                tc.Id,
                tc.Code,
                validation.SanitizedDefinition,
                new AutomationRunConfiguration(cmd.Gateway, cmd.Frontend),
                prerequisites.Value),
            ct);
        return response;
    }
}

public sealed record SetAutomationPrerequisiteCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid TestCaseId,
    Guid? PrerequisiteTestCaseId) : IRequest<Result<TestCaseDto>>;

public sealed class SetAutomationPrerequisiteHandler
    : IRequestHandler<SetAutomationPrerequisiteCommand, Result<TestCaseDto>>
{
    private readonly TestCasesDbContext _db;
    public SetAutomationPrerequisiteHandler(TestCasesDbContext db) => _db = db;

    public async Task<Result<TestCaseDto>> Handle(SetAutomationPrerequisiteCommand cmd, CancellationToken ct)
    {
        var tc = await _db.TestCases
            .Include(x => x.Steps)
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == cmd.TestCaseId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (tc is null) return Result.Failure<TestCaseDto>(TestCaseErrors.TestCaseNotFound);
        if (tc.IsDeleted) return Result.Failure<TestCaseDto>(TestCaseErrors.TestCaseDeleted);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                tc.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<TestCaseDto>(TestCaseErrors.ProjectAccessDenied);

        if (cmd.PrerequisiteTestCaseId is Guid prerequisiteId)
        {
            if (prerequisiteId == tc.Id)
                return Result.Failure<TestCaseDto>(TestCaseErrors.PrerequisiteSelf);

            var seen = new HashSet<Guid> { tc.Id };
            var cursor = (Guid?)prerequisiteId;
            var depth = 0;
            while (cursor is Guid id)
            {
                if (!seen.Add(id))
                    return Result.Failure<TestCaseDto>(TestCaseErrors.AutomationValidation(
                        "پیش‌نیازها حلقه ساخته‌اند. یک سناریو نمی‌تواند، مستقیم یا با واسطه، پیش‌نیاز خودش باشد."));
                if (++depth > ScenarioPrerequisitePlanner.MaxDepth)
                    return Result.Failure<TestCaseDto>(TestCaseErrors.AutomationValidation(
                        $"زنجیره پیش‌نیاز نمی‌تواند بیشتر از {ScenarioPrerequisitePlanner.MaxDepth} سناریو باشد."));

                var link = await _db.TestCases.AsNoTracking()
                    .Where(x => x.Id == id && x.WorkspaceId == cmd.WorkspaceId)
                    .Select(x => new
                    {
                        x.ProjectId,
                        x.DeletedAtUtc,
                        x.AutomationEngine,
                        x.AutomationRecordingJson,
                        x.AutomationPrerequisiteTestCaseId,
                    })
                    .FirstOrDefaultAsync(ct);
                if (link is null || link.DeletedAtUtc != null)
                    return Result.Failure<TestCaseDto>(TestCaseErrors.PrerequisiteNotFound);
                if (link.ProjectId != tc.ProjectId)
                    return Result.Failure<TestCaseDto>(TestCaseErrors.PrerequisiteProjectMismatch);
                if (link.AutomationEngine != AutomationEngine.ScenarioRecording ||
                    string.IsNullOrWhiteSpace(link.AutomationRecordingJson))
                    return Result.Failure<TestCaseDto>(TestCaseErrors.PrerequisiteNotRecorded);

                cursor = link.AutomationPrerequisiteTestCaseId;
            }
        }

        tc.SetAutomationPrerequisite(cmd.PrerequisiteTestCaseId, cmd.CurrentUserId);
        await _db.SaveChangesAsync(ct);
        return TestCaseMapping.Map(tc);
    }
}

internal static class AutomationPrerequisiteLoader
{
    public static async Task<Result<IReadOnlyList<RecordedScenarioDefinition>>> LoadAsync(
        TestCasesDbContext db,
        Guid workspaceId,
        Guid projectId,
        Guid scenarioId,
        Guid? prerequisiteTestCaseId,
        CancellationToken ct)
    {
        var links = new Dictionary<Guid, ScenarioPrerequisitePlanner.ScenarioLink>();
        var pending = new Queue<Guid>();
        if (prerequisiteTestCaseId is Guid first)
            pending.Enqueue(first);

        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            if (links.ContainsKey(id))
                continue;

            var row = await db.TestCases.AsNoTracking()
                .Where(x => x.Id == id && x.WorkspaceId == workspaceId)
                .Select(x => new
                {
                    x.Id,
                    x.ProjectId,
                    x.Title,
                    x.DeletedAtUtc,
                    x.AutomationRecordingJson,
                    x.AutomationPrerequisiteTestCaseId,
                })
                .FirstOrDefaultAsync(ct);

            if (row is null)
            {
                links[id] = new ScenarioPrerequisitePlanner.ScenarioLink(id, projectId, string.Empty, null, null, true);
                continue;
            }

            links[id] = new ScenarioPrerequisitePlanner.ScenarioLink(
                row.Id,
                row.ProjectId,
                row.Title,
                row.AutomationPrerequisiteTestCaseId,
                row.AutomationRecordingJson,
                row.DeletedAtUtc != null);

            if (row.AutomationPrerequisiteTestCaseId is Guid next && !links.ContainsKey(next))
                pending.Enqueue(next);
        }

        var plan = ScenarioPrerequisitePlanner.Create(
            scenarioId,
            projectId,
            prerequisiteTestCaseId,
            id => links.TryGetValue(id, out var link) ? link : null);
        if (!plan.Succeeded)
            return Result.Failure<IReadOnlyList<RecordedScenarioDefinition>>(
                TestCaseErrors.AutomationValidation(plan.Error ?? "پیش‌نیاز اجرا معتبر نیست."));

        return Result.Success<IReadOnlyList<RecordedScenarioDefinition>>(plan.Definitions);
    }
}

internal static class AutomationMapping
{
    public static TestCaseAutomationDto Map(
        TestCase tc,
        AutomationValidationResponse validation) => new(
            tc.Id,
            tc.AutomationEngine,
            tc.AutomationStatus,
            tc.AutomationDefinitionVersion,
            RecordedScenarioValidator.MaskForDisplay(tc.AutomationRecordingJson),
            validation.IsValid,
            validation.Errors);
}

// ─── Set Tags ────────────────────────────────────────────────────────────
public sealed record SetTagsCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid TestCaseId,
    IReadOnlyList<string> Tags) : IRequest<Result<IReadOnlyList<string>>>;

public sealed class SetTagsHandler : IRequestHandler<SetTagsCommand, Result<IReadOnlyList<string>>>
{
    private readonly TestCasesDbContext _db;
    public SetTagsHandler(TestCasesDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<string>>> Handle(SetTagsCommand cmd, CancellationToken ct)
    {
        var tc = await _db.TestCases
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == cmd.TestCaseId && x.WorkspaceId == cmd.WorkspaceId, ct);
        if (tc is null) return Result.Failure<IReadOnlyList<string>>(TestCaseErrors.TestCaseNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                cmd.WorkspaceId,
                cmd.CurrentUserId,
                cmd.CurrentUserAccessScope,
                tc.ProjectId,
                ct,
                cmd.CurrentUserRole,
                ProjectCapability.Contribute))
            return Result.Failure<IReadOnlyList<string>>(TestCaseErrors.ProjectAccessDenied);

        _db.TestCaseTags.RemoveRange(tc.Tags);
        tc.Tags.Clear();
        foreach (var t in cmd.Tags
                     .Select(x => x?.Trim().ToLowerInvariant())
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct()!)
        {
            tc.Tags.Add(TestCaseTag.Create(tc.Id, t!));
        }

        await _db.SaveChangesAsync(ct);
        IReadOnlyList<string> tags = tc.Tags.Select(x => x.Tag).OrderBy(x => x).ToList();
        return Result.Success(tags);
    }
}

// ─── List Versions ───────────────────────────────────────────────────────
public sealed record ListVersionsQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid TestCaseId) : IRequest<Result<IReadOnlyList<TestCaseVersionDto>>>;

public sealed class ListVersionsHandler : IRequestHandler<ListVersionsQuery, Result<IReadOnlyList<TestCaseVersionDto>>>
{
    private readonly TestCasesDbContext _db;
    public ListVersionsHandler(TestCasesDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<TestCaseVersionDto>>> Handle(ListVersionsQuery q, CancellationToken ct)
    {
        var projectId = await _db.TestCases.AsNoTracking()
            .Where(x => x.Id == q.TestCaseId && x.WorkspaceId == q.WorkspaceId)
            .Select(x => (Guid?)x.ProjectId)
            .FirstOrDefaultAsync(ct);
        if (projectId is null) return Result.Failure<IReadOnlyList<TestCaseVersionDto>>(TestCaseErrors.TestCaseNotFound);
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                q.WorkspaceId,
                q.CurrentUserId,
                q.CurrentUserAccessScope,
                projectId.Value,
                ct,
                q.CurrentUserRole))
            return Result.Failure<IReadOnlyList<TestCaseVersionDto>>(TestCaseErrors.ProjectAccessDenied);

        var list = await _db.TestCaseVersions.AsNoTracking()
            .Where(v => v.TestCaseId == q.TestCaseId)
            .OrderByDescending(v => v.Version)
            .Select(v => new TestCaseVersionDto(v.Version, v.CreatedByUserId, v.CreatedAtUtc, v.ChangeReason))
            .ToListAsync(ct);
        IReadOnlyList<TestCaseVersionDto> result = list;
        return Result.Success(result);
    }
}


// --- CustomField validation helper ---
public static class CustomFieldValidator
{
    public static async Task<(bool ok, string? error, Dictionary<string, JsonElement> sanitized)> ValidateAsync(
        TestCasesDbContext db, Guid workspaceId, Guid projectId,
        Dictionary<string, JsonElement> input, CancellationToken ct)
    {
        var defs = await db.CustomFieldDefinitions.AsNoTracking()
            .Where(x => x.WorkspaceId == workspaceId && x.ProjectId == projectId && !x.IsArchived)
            .ToListAsync(ct);
        var byKey = defs.ToDictionary(x => x.Key);
        var sanitized = new Dictionary<string, JsonElement>();
        // validate provided values
        foreach (var (key, value) in input)
        {
            if (!byKey.TryGetValue(key, out var def)) continue; // ignore unknown keys
            if (!def.TryCoerce(value, out var coerced, out var err)) return (false, err, sanitized);
            if (coerced.ValueKind != JsonValueKind.Undefined) sanitized[key] = coerced;
        }
        // ensure required fields are present
        foreach (var def in defs.Where(d => d.Required))
        {
            if (!sanitized.ContainsKey(def.Key))
                return (false, $"\u0641\u06cc\u0644\u062f '{def.Label}' \u0627\u0644\u0632\u0627\u0645\u06cc \u0627\u0633\u062a.", sanitized);
        }
        return (true, null, sanitized);
    }
}

