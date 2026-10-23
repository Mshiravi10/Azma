namespace Azma.UserDirectory;

/// <summary>
/// Cross-service read model of a user. Hydrated from Identity events
/// (UserUpdated, UserAvatarChanged, UserDeactivated). Lives in each service's
/// own database/schema so joins and projections are local.
/// </summary>
public sealed class UserReadModel
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarObjectKey { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; }
}
