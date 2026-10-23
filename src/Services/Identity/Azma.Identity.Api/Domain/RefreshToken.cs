namespace Azma.Identity.Api.Domain;

public sealed class RefreshToken
{
    private RefreshToken() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; private set; }
    public string? ReplacedByHash { get; private set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    public static RefreshToken Issue(Guid userId, Guid workspaceId, string tokenHash, TimeSpan lifetime) => new()
    {
        UserId = userId,
        WorkspaceId = workspaceId,
        TokenHash = tokenHash,
        ExpiresAtUtc = DateTime.UtcNow.Add(lifetime),
    };

    public void Revoke(string? replacedByHash = null)
    {
        RevokedAtUtc = DateTime.UtcNow;
        ReplacedByHash = replacedByHash;
    }
}
