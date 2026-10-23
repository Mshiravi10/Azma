namespace Azma.Identity.Api.Domain;

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Revoked = 2,
    Expired = 3,
}

public sealed class Invitation
{
    private Invitation() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; private set; }
    public string Email { get; private set; } = default!;
    public WorkspaceRole Role { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public Guid InvitedByUserId { get; private set; }
    public InvitationStatus Status { get; private set; } = InvitationStatus.Pending;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }

    public Workspace? Workspace { get; private set; }

    public static Invitation Create(
        Guid workspaceId,
        string email,
        WorkspaceRole role,
        string tokenHash,
        Guid invitedByUserId,
        TimeSpan lifetime) => new()
        {
            WorkspaceId = workspaceId,
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
            TokenHash = tokenHash,
            InvitedByUserId = invitedByUserId,
            ExpiresAtUtc = DateTime.UtcNow.Add(lifetime),
        };

    public void MarkAccepted()
    {
        Status = InvitationStatus.Accepted;
        AcceptedAtUtc = DateTime.UtcNow;
    }

    public void Revoke() => Status = InvitationStatus.Revoked;

    public bool IsUsable() => Status == InvitationStatus.Pending && DateTime.UtcNow < ExpiresAtUtc;
}
