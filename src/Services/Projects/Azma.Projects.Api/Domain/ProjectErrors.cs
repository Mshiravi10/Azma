using Azma.Common.Results;

namespace Azma.Projects.Api.Domain;

public static class ProjectErrors
{
    public static readonly Error ProjectNotFound =
        Error.NotFound("projects.project_not_found", "پروژه‌ی مورد نظر پیدا نشد.");

    public static readonly Error ProjectArchived =
        Error.Conflict("projects.project_archived", "این پروژه آرشیو شده است.");

    public static readonly Error CodeAlreadyTaken =
        Error.Conflict("projects.code_taken", "کد پروژه قبلاً در این فضای کاری ثبت شده است.");

    public static readonly Error MilestoneNotFound =
        Error.NotFound("projects.milestone_not_found", "Milestone مورد نظر پیدا نشد.");

    public static readonly Error MilestoneClosed =
        Error.Conflict("projects.milestone_closed", "این Milestone بسته شده است.");

    public static readonly Error SuiteNotFound =
        Error.NotFound("projects.suite_not_found", "Suite مورد نظر پیدا نشد.");

    public static readonly Error SectionNotFound =
        Error.NotFound("projects.section_not_found", "Section مورد نظر پیدا نشد.");

    public static readonly Error SectionParentMismatch =
        Error.Validation("projects.section_parent_mismatch", "Section والد متعلق به Suite دیگری است.");

    public static readonly Error SectionCycle =
        Error.Validation("projects.section_cycle", "نمی‌توانید یک Section را زیر فرزند خودش قرار دهید.");

    public static readonly Error WorkspaceMismatch =
        Error.Forbidden("projects.workspace_mismatch", "این منبع متعلق به فضای کاری شما نیست.");

    public static readonly Error InvalidPosition =
        Error.Validation("projects.invalid_position", "موقعیت نامعتبر است.");

    public static readonly Error MembershipNotFound =
        Error.NotFound("projects.membership_not_found", "این عضو در پروژه ثبت نشده است.");

    public static readonly Error UserNotInWorkspace =
        Error.Validation("projects.user_not_in_workspace", "این کاربر عضو فضای کاری نیست.");

    public static readonly Error CannotExcludeWorkspaceOwner =
        Error.Forbidden("projects.cannot_exclude_owner", "نمی‌توان مالک کارگاه را از پروژه حذف کرد.");

    public static readonly Error AdminOnly =
        Error.Forbidden("projects.admin_only", "این عملیات فقط برای مدیران مجاز است.");
    public static readonly Error ProjectOwnerOnly =
        Error.Forbidden("projects.project_owner_only", "Only the current project owner can perform this operation.");

    public static readonly Error CannotSetOwnerDirectly =
        Error.Validation("projects.cannot_set_owner_directly", "Use ownership transfer to assign the project owner.");

    public static readonly Error CannotModifyProjectOwner =
        Error.Forbidden("projects.cannot_modify_project_owner", "Project owner role cannot be changed from this operation.");

    public static readonly Error UserNotFound =
        Error.NotFound("projects.user_not_found", "User was not found.");

    public static readonly Error UserInactive =
        Error.Validation("projects.user_inactive", "Inactive users cannot be added to a project.");

    public static readonly Error ProjectAccessDenied =
        Error.Forbidden("projects.access_denied", "You do not have access to this project.");
}
