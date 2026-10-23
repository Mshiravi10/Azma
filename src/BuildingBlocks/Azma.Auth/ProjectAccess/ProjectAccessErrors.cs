using Azma.Common.Results;

namespace Azma.Auth.ProjectAccess;

public static class ProjectAccessErrors
{
    public static readonly Error Denied = Error.Forbidden(
        "access.project_denied",
        "به این پروژه دسترسی ندارید.");

    public static readonly Error InsufficientRole = Error.Forbidden(
        "access.insufficient_role",
        "نقش شما برای این عملیات کافی نیست.");

    public static readonly Error WorkspaceAdminOnly = Error.Forbidden(
        "access.workspace_admin_only",
        "این عملیات فقط برای مالک یا مدیر فضای کاری مجاز است.");
}
