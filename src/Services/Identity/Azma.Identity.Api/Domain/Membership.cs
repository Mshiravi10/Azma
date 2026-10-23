namespace Azma.Identity.Api.Domain;

public enum WorkspaceRole
{
    Owner = 0,
    Admin = 1,
    Tester = 2,
    Viewer = 3,
}

public enum MembershipAccessScope
{
    Workspace = 0,
    ProjectScoped = 1,
}

public sealed class Membership
{
    private Membership() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public WorkspaceRole Role { get; private set; }
    public MembershipAccessScope AccessScope { get; private set; } = MembershipAccessScope.Workspace;
    public DateTime JoinedAtUtc { get; private set; } = DateTime.UtcNow;

    public User? User { get; private set; }
    public Workspace? Workspace { get; private set; }

    public static Membership Create(
        Guid userId,
        Guid workspaceId,
        WorkspaceRole role,
        MembershipAccessScope accessScope = MembershipAccessScope.Workspace) => new()
    {
        UserId = userId,
        WorkspaceId = workspaceId,
        Role = role,
        AccessScope = accessScope,
    };

    public void ChangeRole(WorkspaceRole role) => Role = role;
    public void ChangeAccessScope(MembershipAccessScope accessScope) => AccessScope = accessScope;
}
