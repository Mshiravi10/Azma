namespace Azma.EventBus.Contracts.Identity;

public sealed record UserRegistered(
    Guid UserId,
    Guid WorkspaceId,
    string Email,
    string DisplayName) : IntegrationEvent;

public sealed record WorkspaceCreated(
    Guid WorkspaceId,
    Guid OwnerUserId,
    string Name,
    string Slug) : IntegrationEvent;

public sealed record MemberAdded(
    Guid WorkspaceId,
    Guid UserId,
    string Email,
    string Role) : IntegrationEvent;

public sealed record MemberRemoved(
    Guid WorkspaceId,
    Guid UserId) : IntegrationEvent;

/// <summary>
/// Broadcast whenever a user's profile (email, displayName, active flag) changes,
/// including the initial creation. Cross-service read models hydrate from this.
/// </summary>
public sealed record UserUpdated(
    Guid UserId,
    string Email,
    string DisplayName,
    string? AvatarObjectKey,
    bool IsActive) : IntegrationEvent;

/// <summary>Broadcast when a user's avatar is uploaded, replaced, or removed.</summary>
public sealed record UserAvatarChanged(
    Guid UserId,
    string? AvatarObjectKey) : IntegrationEvent;

/// <summary>Broadcast when a user is hard-deactivated. Also fired when removed from last workspace.</summary>
public sealed record UserDeactivated(Guid UserId) : IntegrationEvent;

/// <summary>Broadcast when a member's role inside a workspace changes.</summary>
public sealed record MemberRoleChanged(
    Guid WorkspaceId,
    Guid UserId,
    string Role) : IntegrationEvent;

/// <summary>
/// Requests Identity to ensure an existing user can enter a workspace for project-scoped access.
/// Existing workspace memberships must be preserved as-is.
/// </summary>
public sealed record EnsureWorkspaceMemberRequested(
    Guid WorkspaceId,
    Guid UserId,
    string Role,
    string AccessScope,
    Guid RequestedByUserId) : IntegrationEvent;
