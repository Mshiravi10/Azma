namespace Azma.Identity.Api.Domain;

public sealed class McpToken
{
    public const string RawTokenPrefix = "azma_mcp_";

    private McpToken() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = default!;
    public string TokenHash { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? LastUsedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public User? User { get; private set; }
    public Workspace? Workspace { get; private set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    public static McpToken Issue(
        Guid userId,
        Guid workspaceId,
        string name,
        string tokenHash,
        TimeSpan lifetime) => new()
    {
        UserId = userId,
        WorkspaceId = workspaceId,
        Name = name.Trim(),
        TokenHash = tokenHash,
        ExpiresAtUtc = DateTime.UtcNow.Add(lifetime)
    };

    public void MarkUsed() => LastUsedAtUtc = DateTime.UtcNow;

    public void Revoke() => RevokedAtUtc ??= DateTime.UtcNow;
}
