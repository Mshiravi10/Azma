using Azma.Common.Results;

namespace Azma.Reporting.Api.Domain;

public static class ReportingErrors
{
    public static readonly Error ProjectNotKnown = Error.NotFound(
        "reporting.project_not_known",
        "پروژه برای سرویس گزارش‌گیری شناخته‌شده نیست.");

    public static readonly Error InvalidRange = Error.Validation(
        "reporting.invalid_range",
        "بازه‌ی زمانی نامعتبر است (تاریخ شروع باید قبل از پایان باشد).");

    public static readonly Error InvalidGranularity = Error.Validation(
        "reporting.invalid_granularity",
        "تفکیک باید یکی از مقادیر day, week یا month باشد.");
    public static readonly Error ProjectAccessDenied = Error.Forbidden(
        "reporting.project_access_denied",
        "You do not have access to this project.");
}

public enum ReportRunStatus
{
    Open = 0,
    Completed = 1,
}

/// <summary>Mirrors Runs service ResultStatus for projections.</summary>
public enum ReportResultStatus
{
    Untested = 0,
    Passed = 1,
    Failed = 2,
    Blocked = 3,
    Retest = 4,
    Skipped = 5,
}
