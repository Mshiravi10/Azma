using Azma.Common.Results;

namespace Azma.Identity.Api.Domain;

public static class IdentityErrors
{
    public static readonly Error EmailAlreadyTaken =
        Error.Conflict("identity.email_taken", "این ایمیل قبلاً ثبت شده است.");

    public static readonly Error WorkspaceSlugTaken =
        Error.Conflict("identity.workspace_slug_taken", "این آدرس کارگاه قبلاً انتخاب شده است.");

    public static readonly Error InvalidCredentials =
        Error.Unauthorized("identity.invalid_credentials", "ایمیل یا گذرواژه اشتباه است.");

    public static readonly Error UserDeactivated =
        Error.Unauthorized("identity.user_deactivated", "حساب کاربری غیرفعال است.");

    public static readonly Error NoWorkspaceMembership =
        Error.Forbidden("identity.no_workspace", "شما عضو هیچ کارگاهی نیستید.");

    public static readonly Error NotMemberOfWorkspace =
        Error.Forbidden("identity.not_member", "عضو این کارگاه نیستید.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("identity.invalid_refresh_token", "توکن نوسازی نامعتبر است.");

    public static readonly Error InvalidMcpToken =
        Error.Unauthorized("identity.invalid_mcp_token", "MCP token is invalid, expired, or revoked.");

    public static readonly Error McpTokenNotFound =
        Error.NotFound("identity.mcp_token_not_found", "MCP token was not found.");

    public static readonly Error InvitationNotFound =
        Error.NotFound("identity.invitation_not_found", "دعوت‌نامه یافت نشد یا منقضی شده است.");

    public static readonly Error InvitationEmailMismatch =
        Error.Forbidden("identity.invitation_email_mismatch", "این دعوت‌نامه برای ایمیل دیگری صادر شده است.");

    public static readonly Error AlreadyMember =
        Error.Conflict("identity.already_member", "این کاربر در حال حاضر عضو کارگاه است.");

    public static readonly Error CannotChangeOwnerRole =
        Error.Forbidden("identity.cannot_change_owner_role", "نقش مالک کارگاه قابل تغییر نیست.");

    public static readonly Error CannotRemoveOwner =
        Error.Forbidden("identity.cannot_remove_owner", "مالک کارگاه را نمی‌توان حذف کرد. ابتدا مالکیت را منتقل کنید.");

    public static readonly Error CannotRemoveSelf =
        Error.Forbidden("identity.cannot_remove_self", "نمی‌توانید خودتان را از کارگاه حذف کنید.");

    public static readonly Error MemberNotFound =
        Error.NotFound("identity.member_not_found", "این کاربر در کارگاه یافت نشد.");

    public static readonly Error UserNotFound =
        Error.NotFound("identity.user_not_found", "کاربر یافت نشد.");

    public static readonly Error InvalidPassword =
        Error.Unauthorized("identity.invalid_password", "گذرواژه فعلی نادرست است.");

    public static readonly Error WeakPassword =
        Error.Validation("identity.weak_password", "گذرواژه باید حداقل ۸ کاراکتر باشد.");

    public static readonly Error AdminOnly =
        Error.Forbidden("identity.admin_only", "این عملیات فقط برای مدیران کارگاه مجاز است.");

    public static readonly Error OwnerOnly =
        Error.Forbidden("identity.owner_only", "این عملیات فقط برای مالک کارگاه مجاز است.");
}
