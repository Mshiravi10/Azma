using Azma.Reporting.Api.Domain;

namespace Azma.Reporting.Api.Application.Common;

public sealed record StatusBreakdownDto(
    int Untested,
    int Passed,
    int Failed,
    int Blocked,
    int Retest,
    int Skipped)
{
    public int Total => Untested + Passed + Failed + Blocked + Retest + Skipped;
    public int Executed => Total - Untested;
    public double PassRate => Executed == 0 ? 0 : Math.Round((double)Passed * 100 / Executed, 1);
}

public sealed record RecentRunDto(
    Guid RunId,
    Guid ProjectId,
    string ProjectCode,
    string Name,
    ReportRunStatus Status,
    int CaseCount,
    int Passed,
    int Failed,
    int Blocked,
    int Skipped,
    int Retest,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc)
{
    public int Executed => Passed + Failed + Blocked + Skipped + Retest;
    public double PassRate => Executed == 0 ? 0 : Math.Round((double)Passed * 100 / Executed, 1);
}

public sealed record ProjectDashboardDto(
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    bool IsArchived,
    int TotalRuns,
    int OpenRuns,
    int CompletedRuns,
    int TotalTestCases,
    int RecordedResults,
    StatusBreakdownDto Counts,
    IReadOnlyList<RecentRunDto> RecentRuns);

public sealed record WorkspaceDashboardDto(
    Guid WorkspaceId,
    int TotalProjects,
    int ArchivedProjects,
    int TotalRuns,
    int OpenRuns,
    int TotalTestCases,
    int RecordedResults,
    StatusBreakdownDto Counts,
    IReadOnlyList<ProjectSummaryDto> Projects,
    IReadOnlyList<RecentRunDto> RecentRuns);

public sealed record ProjectSummaryDto(
    Guid ProjectId,
    string Code,
    string Name,
    bool IsArchived,
    int TotalRuns,
    int OpenRuns,
    int TestCases);

public sealed record TrendBucketDto(
    DateTime BucketStartUtc,
    int Passed,
    int Failed,
    int Blocked,
    int Skipped,
    int Retest)
{
    public int Total => Passed + Failed + Blocked + Skipped + Retest;
    public double PassRate => Total == 0 ? 0 : Math.Round((double)Passed * 100 / Total, 1);
}

public sealed record TopFailingTestDto(
    Guid TestCaseId,
    string Code,
    string Title,
    int Failures,
    int Executions)
{
    public double FailureRate => Executions == 0 ? 0 : Math.Round((double)Failures * 100 / Executions, 1);
}

public sealed record UserActivityDto(
    Guid UserId,
    int Executions,
    int Passed,
    int Failed,
    int Blocked,
    int Skipped,
    int Retest)
{
    public double PassRate => Executions == 0 ? 0 : Math.Round((double)Passed * 100 / Executions, 1);
}
