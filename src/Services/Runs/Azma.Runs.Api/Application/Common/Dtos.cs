using Azma.Runs.Api.Domain;
using Azma.Automation;

namespace Azma.Runs.Api.Application.Common;

public sealed record RunSummaryDto(
    Guid Id,
    Guid ProjectId,
    Guid? MilestoneId,
    string Code,
    string Name,
    RunStatus Status,
    int CaseCount,
    RunStatusCounts Counts,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    string? Environment,
    DateTime? DueDateUtc);

public sealed record RunStatusCounts(
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

public sealed record RunDto(
    Guid Id,
    Guid ProjectId,
    Guid? MilestoneId,
    string Code,
    string Name,
    string? Description,
    RunStatus Status,
    RunStatusCounts Counts,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    Guid? ClosedByUserId,
    string? Environment,
    DateTime? DueDateUtc);

public sealed record RunCaseDto(
    Guid Id,
    Guid TestCaseId,
    string TestCaseCode,
    string TestCaseTitle,
    int TestCaseVersion,
    bool HasAutomation,
    AutomationEngine AutomationEngine,
    AutomationStatus AutomationStatus,
    int? AutomationDefinitionVersion,
    string? AutomationScenarioPath,
    Guid? AssignedToUserId,
    ResultStatus CurrentStatus,
    DateTime? LastExecutedAtUtc,
    Guid? LastExecutedByUserId);

public sealed record RunResultDto(
    Guid Id,
    Guid RunCaseId,
    ResultStatus Status,
    string? Comment,
    int? ElapsedSeconds,
    AutomationEngine? AutomationEngine,
    string? AutomationDetailJson,
    Guid ExecutedByUserId,
    DateTime ExecutedAtUtc);

public sealed record AutomaticRunResultDto(
    RunResultDto Result,
    string ScenarioName,
    AutomationEngine AutomationEngine,
    bool Succeeded,
    string? ErrorMessage);

public sealed record RunCommentDto(
    Guid Id,
    Guid? RunCaseId,
    Guid AuthorUserId,
    string Body,
    DateTime CreatedAtUtc);

public sealed record RunAutomationExecutionDto(
    Guid Id,
    Guid RunId,
    RunAutomationExecutionStatus Status,
    string Trigger,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int TotalCases,
    int Passed,
    int Failed,
    string? ErrorMessage);

public sealed record RunAttachmentDto(
    Guid Id,
    Guid RunId,
    Guid? RunCaseId,
    Guid? RunResultId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid UploadedByUserId,
    DateTime UploadedAtUtc);

public sealed record RunScheduleDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    string? Environment,
    ScheduleRecurrenceType RecurrenceType,
    int TimeOfDayMinutes,
    int WeekDaysMask,
    string TimeZoneId,
    DateTime? StartAtUtc,
    DateTime? EndAtUtc,
    bool IsActive,
    DateTime? NextRunAtUtc,
    DateTime? LastRunAtUtc,
    Guid? LastRunId,
    int CaseCount,
    int Loops,
    int Threads,
    int Rampup,
    bool Skippable,
    string? Gateway,
    string? Frontend,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record RunScheduleCaseDto(
    Guid Id,
    Guid TestCaseId,
    string TestCaseCode,
    string TestCaseTitle,
    int TestCaseVersion,
    string? AutomationScenarioPath,
    AutomationEngine AutomationEngine,
    AutomationStatus AutomationStatus,
    int? AutomationDefinitionVersion);

public sealed record RunScheduleDetailDto(
    RunScheduleDto Schedule,
    IReadOnlyList<RunScheduleCaseDto> Cases);

public sealed record RunScheduleExecutionDto(
    Guid Id,
    Guid RunScheduleId,
    Guid? RunId,
    RunScheduleExecutionStatus Status,
    string Trigger,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int TotalCases,
    int Passed,
    int Failed,
    string? ErrorMessage);
