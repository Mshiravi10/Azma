namespace Azma.Identity.Api.Application.Common;

public sealed record AuthTokensDto(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserDto User,
    WorkspaceDto Workspace);

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    bool MustChangePassword);

public sealed record WorkspaceDto(Guid Id, string Name, string Slug, string Role, string AccessScope);

public sealed record McpTokenDto(
    Guid Id,
    string Name,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? LastUsedAtUtc,
    DateTime? RevokedAtUtc,
    string Status);

public sealed record CreateMcpTokenResponse(
    Guid Id,
    string Name,
    string Token,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);

public sealed record ValidateMcpTokenResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid WorkspaceId,
    string WorkspaceName,
    string WorkspaceSlug,
    string Role,
    string AccessScope);

public sealed record MembershipDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    string Role,
    string AccessScope,
    DateTime JoinedAtUtc,
    DateTime? LastLoginAtUtc,
    bool IsActive);

public sealed record InvitationDto(
    Guid Id,
    string Email,
    string Role,
    string Status,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    string? Token);

/// <summary>Builds the public avatar URL convention used by all clients.</summary>
public static class UserDtoFactory
{
    public static string? AvatarUrl(Guid userId, string? objectKey, DateTime updatedAtUtc) =>
        string.IsNullOrEmpty(objectKey)
            ? null
            : $"/api/users/{userId}/avatar?v={updatedAtUtc.Ticks}";
}
