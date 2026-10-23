using Azma.Common.Results;

namespace Azma.Runs.Api.Domain;

public static class RunErrors
{
    public static readonly Error ProjectNotKnown = Error.Validation(
        "runs.project_not_known",
        "پروژه برای این سرویس شناخته‌شده نیست (هنوز event پروژه دریافت نشده).");

    public static readonly Error ProjectArchived = Error.Validation(
        "runs.project_archived",
        "این پروژه آرشیو شده است؛ امکان ساخت اجرای جدید وجود ندارد.");

    public static readonly Error TestCaseUnknown = Error.Validation(
        "runs.testcase_unknown",
        "حداقل یکی از تست‌کیس‌ها در این سرویس شناخته‌شده نیست.");

    public static readonly Error NoTestCases = Error.Validation(
        "runs.no_testcases",
        "حداقل یک تست‌کیس باید برای ساخت اجرا انتخاب شود.");

    public static readonly Error TestCaseProjectMismatch = Error.Validation(
        "runs.testcase_project_mismatch",
        "همه‌ی تست‌کیس‌های انتخاب شده باید به همان پروژه تعلق داشته باشند.");

    public static readonly Error RunNotFound = Error.NotFound(
        "runs.run_not_found",
        "اجرای مورد نظر یافت نشد.");

    public static readonly Error RunDeleted = Error.Validation(
        "runs.run_deleted",
        "این اجرا حذف شده است.");

    public static readonly Error RunClosed = Error.Validation(
        "runs.run_closed",
        "این اجرا بسته شده است؛ امکان تغییر نتیجه وجود ندارد.");

    public static readonly Error RunCaseNotFound = Error.NotFound(
        "runs.runcase_not_found",
        "تست‌کیس مورد نظر در این اجرا یافت نشد.");

    public static readonly Error AutomationNotConfigured = Error.Validation(
        "runs.automation_not_configured",
        "Automation is not configured for this test case.");

    public static readonly Error RunHasManualCases = Error.Validation(
        "runs.run_has_manual_cases",
        "Automatic run execution requires all cases to have published automation.");

    public static readonly Error RunAutomationAlreadyQueued = Error.Conflict(
        "runs.run_automation_already_queued",
        "An automatic execution is already queued or running for this run.");

    public static readonly Error InvalidStatus = Error.Validation(
        "runs.invalid_status",
        "وضعیت ارسالی معتبر نیست.");

    public static readonly Error AttachmentNotFound = Error.NotFound(
        "runs.attachment_not_found",
        "پیوست مورد نظر یافت نشد.");

    public static readonly Error AttachmentEmpty = Error.Validation(
        "runs.attachment_empty",
        "فایل پیوست خالی است.");

    public static readonly Error AttachmentTooLarge = Error.Validation(
        "runs.attachment_too_large",
        "حجم فایل بیش از حد مجاز است.");
    public static readonly Error ScheduleNotFound = Error.NotFound(
        "runs.schedule_not_found",
        "Schedule was not found.");

    public static readonly Error ScheduleHasNoAutomaticCases = Error.Validation(
        "runs.schedule_no_automatic_cases",
        "Schedule must contain at least one automated test case.");

    public static readonly Error ScheduleInvalidRecurrence = Error.Validation(
        "runs.schedule_invalid_recurrence",
        "Schedule recurrence is invalid.");

    public static readonly Error ProjectAccessDenied = Error.Forbidden(
        "runs.project_access_denied",
        "You do not have access to this project.");
}

public enum RunStatus
{
    Open = 0,
    Closed = 1,
}

public enum ResultStatus
{
    Untested = 0,
    Passed = 1,
    Failed = 2,
    Blocked = 3,
    Retest = 4,
    Skipped = 5,
}
