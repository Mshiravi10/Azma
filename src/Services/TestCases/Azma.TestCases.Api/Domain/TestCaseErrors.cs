using Azma.Common.Domain;
using Azma.Common.Results;

namespace Azma.TestCases.Api.Domain;

public static class TestCaseErrors
{
    public static readonly Error ProjectNotKnown = Error.Conflict(
        "testcases.project_not_known",
        "پروژه برای این سرویس شناخته‌شده نیست (هنوز event پروژه دریافت نشده).");

    public static readonly Error ProjectArchived = new(
        "testcases.project_archived",
        "این پروژه آرشیو شده است؛ امکان ساخت تست‌کیس جدید وجود ندارد.");

    public static readonly Error TestCaseNotFound = new(
        "testcases.testcase_not_found",
        "تست‌کیس مورد نظر یافت نشد.");

    public static readonly Error TestCaseDeleted = new(
        "testcases.testcase_deleted",
        "این تست‌کیس حذف شده است.");

    public static readonly Error WorkspaceMismatch = new(
        "testcases.workspace_mismatch",
        "این منبع به فضای کاری شما تعلق ندارد.");

    public static readonly Error InvalidStepOrder = new(
        "testcases.invalid_step_order",
        "ترتیب گام‌ها باید پیوسته و از 1 شروع شود.");

    public static readonly Error EmptyTitle = new(
        "testcases.empty_title",
        "عنوان تست‌کیس نمی‌تواند خالی باشد.");

    public static readonly Error CustomFieldNotFound = new(
        "testcases.custom_field_not_found",
        "فیلد سفارشی یافت نشد.");

    public static readonly Error CustomFieldKeyExists = new(
        "testcases.custom_field_key_exists",
        "فیلدی با این کلید از قبل در پروژه وجود دارد.");

    public static Error CustomFieldValidation(string message) => new(
        "testcases.custom_field_validation",
        message);

    public static Error AutomationValidation(string message) => new(
        "testcases.automation_validation",
        message);

    public static readonly Error AutomationRecordingSessionNotFound = new(
        "testcases.automation_recording_session_not_found",
        "Automation recording session was not found.");

    public static readonly Error AutomationRecordingSessionInvalid = new(
        "testcases.automation_recording_session_invalid",
        "Automation recording session is invalid or expired.");

    public static readonly Error PrerequisiteSelf = new(
        "testcases.prerequisite_self",
        "یک سناریو نمی‌تواند پیش‌نیاز خودش باشد.");

    public static readonly Error PrerequisiteNotFound = new(
        "testcases.prerequisite_not_found",
        "سناریوی پیش‌نیاز پیدا نشد.");

    public static readonly Error PrerequisiteProjectMismatch = new(
        "testcases.prerequisite_project_mismatch",
        "پیش‌نیاز باید سناریویی از همین پروژه باشد.");

    public static readonly Error PrerequisiteNotRecorded = new(
        "testcases.prerequisite_not_recorded",
        "سناریوی پیش‌نیاز باید یک ضبط معتبر داشته باشد.");

    public static readonly Error ProjectAccessDenied = new(
        "testcases.project_access_denied",
        "You do not have access to this project.");
}

public enum TestCaseType
{
    Functional = 0,
    Integration = 1,
    EndToEnd = 2,
    Performance = 3,
    Security = 4,
    Usability = 5,
    Other = 99
}

public enum TestCasePriority
{
    Critical = 0,
    High = 1,
    Medium = 2,
    Low = 3
}

public enum TestCaseStatus
{
    Draft = 0,
    Active = 1,
    Deprecated = 2
}
