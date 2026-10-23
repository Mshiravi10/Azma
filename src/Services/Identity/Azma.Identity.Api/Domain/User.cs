namespace Azma.Identity.Api.Domain;

public sealed class User
{
    private User() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public string? AvatarObjectKey { get; private set; }
    public bool MustChangePassword { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; private set; }

    public ICollection<Membership> Memberships { get; private set; } = new List<Membership>();
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();
    public ICollection<McpToken> McpTokens { get; private set; } = new List<McpToken>();

    public static User Create(string email, string passwordHash, string displayName, bool mustChangePassword = false) => new()
    {
        Email = email.Trim().ToLowerInvariant(),
        PasswordHash = passwordHash,
        DisplayName = displayName.Trim(),
        MustChangePassword = mustChangePassword,
    };

    public void UpdateLastLogin() => LastLoginAtUtc = DateTime.UtcNow;

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ChangePassword(string newHash)
    {
        PasswordHash = newHash;
        MustChangePassword = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Rename(string displayName)
    {
        DisplayName = displayName.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetAvatar(string? objectKey)
    {
        AvatarObjectKey = objectKey;
        UpdatedAtUtc = DateTime.UtcNow;
    }

}
