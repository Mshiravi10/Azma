using Azma.Auth.ProjectAccess;
using Azma.Common.Results;
using Azma.Reporting.Api.Application.Common;
using Azma.Reporting.Api.Domain;
using Azma.Reporting.Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Reporting.Api.Application.Reporting;

internal static class Mapping
{
    public static StatusBreakdownDto BuildBreakdown(int caseCount, int passed, int failed, int blocked, int retest, int skipped)
    {
        var executed = passed + failed + blocked + retest + skipped;
        var untested = Math.Max(0, caseCount - executed);
        return new StatusBreakdownDto(untested, passed, failed, blocked, retest, skipped);
    }

    public static RecentRunDto MapRun(RunRef r, string projectCode) => new(
        r.Id, r.ProjectId, projectCode, r.Name, r.Status, r.CaseCount,
        r.Passed, r.Failed, r.Blocked, r.Skipped, r.Retest, r.CreatedAtUtc, r.CompletedAtUtc);
}

// ───────────────────────── Project Dashboard ─────────────────────────────
public sealed record GetProjectDashboardQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid ProjectId) : IRequest<Result<ProjectDashboardDto>>;

public sealed class GetProjectDashboardHandler : IRequestHandler<GetProjectDashboardQuery, Result<ProjectDashboardDto>>
{
    private readonly ReportingDbContext _db;
    public GetProjectDashboardHandler(ReportingDbContext db) => _db = db;

    public async Task<Result<ProjectDashboardDto>> Handle(GetProjectDashboardQuery q, CancellationToken ct)
    {
        if (!await ProjectAccessPolicy.CanAccessProjectAsync(
                _db.Database.GetDbConnection(),
                q.WorkspaceId,
                q.CurrentUserId,
                q.CurrentUserAccessScope,
                q.ProjectId,
                ct,
                q.CurrentUserRole))
            return Result.Failure<ProjectDashboardDto>(ReportingErrors.ProjectAccessDenied);

        // Fallback: if the project projection is missing (e.g. event bus missed the event),
        // sync it from the source-of-truth (same pattern used by Runs service).
        var project = await _db.ProjectRefs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == q.ProjectId && p.WorkspaceId == q.WorkspaceId, ct);

        if (project is null)
        {
            await SyncProjectFromSourceAsync(q.WorkspaceId, q.ProjectId, ct);
            project = await _db.ProjectRefs.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == q.ProjectId && p.WorkspaceId == q.WorkspaceId, ct);
        }

        if (project is null) return Result.Failure<ProjectDashboardDto>(ReportingErrors.ProjectNotKnown);

        // Fallback: sync test case refs and runs if they're missing.
        var hasTestCases = await _db.TestCaseRefs.AsNoTracking()
            .AnyAsync(t => t.WorkspaceId == q.WorkspaceId && t.ProjectId == q.ProjectId, ct);
        if (!hasTestCases)
            await SyncTestCasesFromSourceAsync(q.WorkspaceId, q.ProjectId, ct);

        var hasRuns = await _db.Runs.AsNoTracking()
            .AnyAsync(r => r.WorkspaceId == q.WorkspaceId && r.ProjectId == q.ProjectId, ct);
        if (!hasRuns)
            await SyncRunsFromSourceAsync(q.WorkspaceId, q.ProjectId, ct);

        var runs = await _db.Runs.AsNoTracking()
            .Where(r => r.WorkspaceId == q.WorkspaceId && r.ProjectId == q.ProjectId)
            .ToListAsync(ct);

        var totalCases = await _db.TestCaseRefs.AsNoTracking()
            .CountAsync(t => t.WorkspaceId == q.WorkspaceId && t.ProjectId == q.ProjectId && !t.IsDeleted, ct);

        var totalRuns = runs.Count;
        var openRuns = runs.Count(r => r.Status == ReportRunStatus.Open);
        var completedRuns = totalRuns - openRuns;
        var caseCount = runs.Sum(r => r.CaseCount);
        var passed = runs.Sum(r => r.Passed);
        var failed = runs.Sum(r => r.Failed);
        var blocked = runs.Sum(r => r.Blocked);
        var skipped = runs.Sum(r => r.Skipped);
        var retest = runs.Sum(r => r.Retest);
        var recorded = runs.Sum(r => r.Recorded);

        var counts = Mapping.BuildBreakdown(caseCount, passed, failed, blocked, retest, skipped);

        var recent = runs
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(10)
            .Select(r => Mapping.MapRun(r, project.Code))
            .ToList();

        return new ProjectDashboardDto(
            project.Id, project.Code, project.Name, project.IsArchived,
            totalRuns, openRuns, completedRuns,
            totalCases, recorded, counts, recent);
    }

    private async Task SyncProjectFromSourceAsync(Guid workspaceId, Guid projectId, CancellationToken ct)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO reporting."project_refs" ("Id", "WorkspaceId", "Code", "Name", "IsArchived", "CreatedAtUtc")
            SELECT "Id", "WorkspaceId", "Code", COALESCE("Name", "Code"), ("ArchivedAtUtc" IS NOT NULL), "CreatedAtUtc"
            FROM projects."projects"
            WHERE "Id" = {projectId} AND "WorkspaceId" = {workspaceId}
            ON CONFLICT ("Id") DO NOTHING
            """, ct);
    }

    private async Task SyncTestCasesFromSourceAsync(Guid workspaceId, Guid projectId, CancellationToken ct)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO reporting."testcase_refs" ("Id", "WorkspaceId", "ProjectId", "Code", "Title", "IsDeleted")
            SELECT "Id", "WorkspaceId", "ProjectId", "Code", "Title", ("DeletedAtUtc" IS NOT NULL)
            FROM testcases."testcases"
            WHERE "ProjectId" = {projectId} AND "WorkspaceId" = {workspaceId}
            ON CONFLICT ("Id") DO NOTHING
            """, ct);
    }

    private async Task SyncRunsFromSourceAsync(Guid workspaceId, Guid projectId, CancellationToken ct)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO reporting."runs" (
                "Id", "WorkspaceId", "ProjectId", "Name", "CaseCount",
                "Status", "Passed", "Failed", "Blocked", "Skipped", "Retest", "Recorded",
                "CreatedAtUtc", "CompletedAtUtc"
            )
            SELECT
                r."Id", r."WorkspaceId", r."ProjectId", r."Name",
                COUNT(rc."Id") as "CaseCount",
                CASE WHEN MAX(CASE WHEN r."Status" = 1 THEN 1 ELSE 0 END) = 1 THEN 1 ELSE 0 END as "Status",
                COALESCE(COUNT(rc."Id") FILTER (WHERE rc."CurrentStatus" = 1), 0) as "Passed",
                COALESCE(COUNT(rc."Id") FILTER (WHERE rc."CurrentStatus" = 2), 0) as "Failed",
                COALESCE(COUNT(rc."Id") FILTER (WHERE rc."CurrentStatus" = 3), 0) as "Blocked",
                COALESCE(COUNT(rc."Id") FILTER (WHERE rc."CurrentStatus" = 5), 0) as "Skipped",
                COALESCE(COUNT(rc."Id") FILTER (WHERE rc."CurrentStatus" = 4), 0) as "Retest",
                COALESCE(COUNT(rc."Id") FILTER (WHERE rc."CurrentStatus" > 0), 0) as "Recorded",
                r."CreatedAtUtc",
                r."ClosedAtUtc" as "CompletedAtUtc"
            FROM runs."runs" r
            LEFT JOIN runs."run_cases" rc ON rc."RunId" = r."Id"
            WHERE r."ProjectId" = {projectId} AND r."WorkspaceId" = {workspaceId} AND r."DeletedAtUtc" IS NULL
            GROUP BY r."Id"
            ON CONFLICT ("Id") DO NOTHING
            """, ct);
    }
}

// ─────────────────────── Workspace Dashboard ─────────────────────────────
public sealed record GetWorkspaceDashboardQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole) : IRequest<Result<WorkspaceDashboardDto>>;

public sealed class GetWorkspaceDashboardHandler : IRequestHandler<GetWorkspaceDashboardQuery, Result<WorkspaceDashboardDto>>
{
    private readonly ReportingDbContext _db;
    public GetWorkspaceDashboardHandler(ReportingDbContext db) => _db = db;

    public async Task<Result<WorkspaceDashboardDto>> Handle(GetWorkspaceDashboardQuery q, CancellationToken ct)
    {
        // Fallback: if project projections are missing, sync from source-of-truth first.
        var hasProjects = await _db.ProjectRefs.AsNoTracking()
            .AnyAsync(p => p.WorkspaceId == q.WorkspaceId, ct);
        if (!hasProjects)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO reporting."project_refs" ("Id", "WorkspaceId", "Code", "Name", "IsArchived", "CreatedAtUtc")
                SELECT "Id", "WorkspaceId", "Code", COALESCE("Name", "Code"), ("ArchivedAtUtc" IS NOT NULL), "CreatedAtUtc"
                FROM projects."projects"
                WHERE "WorkspaceId" = {q.WorkspaceId}
                ON CONFLICT ("Id") DO NOTHING
                """, ct);
        }

        var projects = await _db.ProjectRefs.AsNoTracking()
            .Where(p => p.WorkspaceId == q.WorkspaceId)
            .ToListAsync(ct);
        var visibility = await ProjectAccessPolicy.GetVisibilityAsync(
            _db.Database.GetDbConnection(),
            q.WorkspaceId,
            q.CurrentUserId,
            q.CurrentUserAccessScope,
            q.CurrentUserRole,
            ct);
        projects = projects.Where(p => visibility.Contains(p.Id)).ToList();

        var runs = await _db.Runs.AsNoTracking()
            .Where(r => r.WorkspaceId == q.WorkspaceId)
            .ToListAsync(ct);
        runs = runs.Where(r => visibility.Contains(r.ProjectId)).ToList();

        var testCases = await _db.TestCaseRefs.AsNoTracking()
            .Where(t => t.WorkspaceId == q.WorkspaceId && !t.IsDeleted)
            .GroupBy(t => t.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        testCases = testCases.Where(t => visibility.Contains(t.ProjectId)).ToList();
        var tcByProject = testCases.ToDictionary(x => x.ProjectId, x => x.Count);

        var caseCount = runs.Sum(r => r.CaseCount);
        var passed = runs.Sum(r => r.Passed);
        var failed = runs.Sum(r => r.Failed);
        var blocked = runs.Sum(r => r.Blocked);
        var skipped = runs.Sum(r => r.Skipped);
        var retest = runs.Sum(r => r.Retest);

        var counts = Mapping.BuildBreakdown(caseCount, passed, failed, blocked, retest, skipped);

        var codeById = projects.ToDictionary(p => p.Id, p => p.Code);

        var summaries = projects
            .Where(p => !p.IsArchived)
            .Select(p =>
            {
                var prRuns = runs.Where(r => r.ProjectId == p.Id).ToList();
                return new ProjectSummaryDto(
                    p.Id, p.Code, p.Name, p.IsArchived,
                    prRuns.Count,
                    prRuns.Count(r => r.Status == ReportRunStatus.Open),
                    tcByProject.GetValueOrDefault(p.Id));
            })
            .OrderBy(s => s.Code)
            .ToList();

        var recent = runs
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(10)
            .Select(r => Mapping.MapRun(r, codeById.GetValueOrDefault(r.ProjectId, "?")))
            .ToList();

        return new WorkspaceDashboardDto(
            q.WorkspaceId,
            projects.Count,
            projects.Count(p => p.IsArchived),
            runs.Count,
            runs.Count(r => r.Status == ReportRunStatus.Open),
            tcByProject.Sum(x => x.Value),
            runs.Sum(r => r.Recorded),
            counts,
            summaries,
            recent);
    }
}

// ─────────────────────────── Trends ──────────────────────────────────────
public sealed record GetTrendsQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid? ProjectId,
    DateTime FromUtc,
    DateTime ToUtc,
    string Granularity) : IRequest<Result<IReadOnlyList<TrendBucketDto>>>;

public sealed class GetTrendsHandler : IRequestHandler<GetTrendsQuery, Result<IReadOnlyList<TrendBucketDto>>>
{
    private readonly ReportingDbContext _db;
    public GetTrendsHandler(ReportingDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<TrendBucketDto>>> Handle(GetTrendsQuery q, CancellationToken ct)
    {
        if (q.ToUtc <= q.FromUtc)
            return Result.Failure<IReadOnlyList<TrendBucketDto>>(ReportingErrors.InvalidRange);

        var trunc = q.Granularity?.ToLowerInvariant() switch
        {
            "day" or "daily" => "day",
            "week" or "weekly" => "week",
            "month" or "monthly" => "month",
            _ => null,
        };
        if (trunc is null)
            return Result.Failure<IReadOnlyList<TrendBucketDto>>(ReportingErrors.InvalidGranularity);

        var query = _db.ResultEvents.AsNoTracking()
            .Where(e => e.WorkspaceId == q.WorkspaceId
                        && e.ExecutedAtUtc >= q.FromUtc
                        && e.ExecutedAtUtc < q.ToUtc);
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
            query = query.Where(e => allowed.Contains(e.ProjectId));
        if (denied.Length > 0)
            query = query.Where(e => !denied.Contains(e.ProjectId));
        if (q.ProjectId is { } pid) query = query.Where(e => e.ProjectId == pid);

        // Pull rows then bucket client-side (works on any provider; volume bounded by date range).
        var raw = await query
            .Select(e => new { e.ExecutedAtUtc, e.Status })
            .ToListAsync(ct);

        DateTime Bucket(DateTime t) => trunc switch
        {
            "day" => new DateTime(t.Year, t.Month, t.Day, 0, 0, 0, DateTimeKind.Utc),
            "week" => StartOfIsoWeek(t),
            "month" => new DateTime(t.Year, t.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => t,
        };

        var grouped = raw
            .GroupBy(r => Bucket(DateTime.SpecifyKind(r.ExecutedAtUtc, DateTimeKind.Utc)))
            .OrderBy(g => g.Key)
            .Select(g => new TrendBucketDto(
                g.Key,
                g.Count(x => x.Status == ReportResultStatus.Passed),
                g.Count(x => x.Status == ReportResultStatus.Failed),
                g.Count(x => x.Status == ReportResultStatus.Blocked),
                g.Count(x => x.Status == ReportResultStatus.Skipped),
                g.Count(x => x.Status == ReportResultStatus.Retest)))
            .ToList();

        IReadOnlyList<TrendBucketDto> result = grouped;
        return Result.Success(result);
    }

    private static DateTime StartOfIsoWeek(DateTime t)
    {
        var utc = DateTime.SpecifyKind(t, DateTimeKind.Utc);
        var diff = ((int)utc.DayOfWeek + 6) % 7; // Monday=0
        var start = utc.AddDays(-diff).Date;
        return DateTime.SpecifyKind(start, DateTimeKind.Utc);
    }
}

// ────────────────────── Top failing test cases ───────────────────────────
public sealed record GetTopFailingTestsQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid? ProjectId,
    int Days,
    int Limit) : IRequest<Result<IReadOnlyList<TopFailingTestDto>>>;

public sealed class GetTopFailingTestsHandler : IRequestHandler<GetTopFailingTestsQuery, Result<IReadOnlyList<TopFailingTestDto>>>
{
    private readonly ReportingDbContext _db;
    public GetTopFailingTestsHandler(ReportingDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<TopFailingTestDto>>> Handle(GetTopFailingTestsQuery q, CancellationToken ct)
    {
        var days = Math.Clamp(q.Days, 1, 365);
        var limit = Math.Clamp(q.Limit, 1, 100);
        var since = DateTime.UtcNow.AddDays(-days);

        var query = _db.ResultEvents.AsNoTracking()
            .Where(e => e.WorkspaceId == q.WorkspaceId && e.ExecutedAtUtc >= since);
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
            query = query.Where(e => allowed.Contains(e.ProjectId));
        if (denied.Length > 0)
            query = query.Where(e => !denied.Contains(e.ProjectId));
        if (q.ProjectId is { } pid) query = query.Where(e => e.ProjectId == pid);

        var grouped = await query
            .GroupBy(e => e.TestCaseId)
            .Select(g => new
            {
                TestCaseId = g.Key,
                Failures = g.Count(x => x.Status == ReportResultStatus.Failed),
                Executions = g.Count(),
            })
            .Where(x => x.Failures > 0)
            .OrderByDescending(x => x.Failures)
            .ThenByDescending(x => x.Executions)
            .Take(limit)
            .ToListAsync(ct);

        var ids = grouped.Select(g => g.TestCaseId).ToList();
        var refs = await _db.TestCaseRefs.AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, ct);

        IReadOnlyList<TopFailingTestDto> result = grouped
            .Select(g =>
            {
                refs.TryGetValue(g.TestCaseId, out var tc);
                return new TopFailingTestDto(
                    g.TestCaseId,
                    tc?.Code ?? "?",
                    tc?.Title ?? "(unknown)",
                    g.Failures,
                    g.Executions);
            })
            .ToList();
        return Result.Success(result);
    }
}

// ─────────────────────── User activity (executors) ───────────────────────
public sealed record GetUserActivityQuery(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserAccessScope,
    string CurrentUserRole,
    Guid? ProjectId,
    int Days,
    int Limit) : IRequest<Result<IReadOnlyList<UserActivityDto>>>;

public sealed class GetUserActivityHandler : IRequestHandler<GetUserActivityQuery, Result<IReadOnlyList<UserActivityDto>>>
{
    private readonly ReportingDbContext _db;
    public GetUserActivityHandler(ReportingDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<UserActivityDto>>> Handle(GetUserActivityQuery q, CancellationToken ct)
    {
        var days = Math.Clamp(q.Days, 1, 365);
        var limit = Math.Clamp(q.Limit, 1, 100);
        var since = DateTime.UtcNow.AddDays(-days);

        var query = _db.ResultEvents.AsNoTracking()
            .Where(e => e.WorkspaceId == q.WorkspaceId && e.ExecutedAtUtc >= since);
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
            query = query.Where(e => allowed.Contains(e.ProjectId));
        if (denied.Length > 0)
            query = query.Where(e => !denied.Contains(e.ProjectId));
        if (q.ProjectId is { } pid) query = query.Where(e => e.ProjectId == pid);

        var rows = await query
            .GroupBy(e => e.ExecutedByUserId)
            .Select(g => new
            {
                UserId = g.Key,
                Executions = g.Count(),
                Passed = g.Count(x => x.Status == ReportResultStatus.Passed),
                Failed = g.Count(x => x.Status == ReportResultStatus.Failed),
                Blocked = g.Count(x => x.Status == ReportResultStatus.Blocked),
                Skipped = g.Count(x => x.Status == ReportResultStatus.Skipped),
                Retest = g.Count(x => x.Status == ReportResultStatus.Retest),
            })
            .OrderByDescending(x => x.Executions)
            .Take(limit)
            .ToListAsync(ct);

        IReadOnlyList<UserActivityDto> result = rows
            .Select(r => new UserActivityDto(r.UserId, r.Executions, r.Passed, r.Failed, r.Blocked, r.Skipped, r.Retest))
            .ToList();
        return Result.Success(result);
    }
}
