namespace Azma.Projects.Api.Domain;

/// <summary>
/// Per-project role override. When a row exists for (ProjectId, UserId), it determines the user's
/// effective role in that project — overriding their workspace role. The special role
/// <see cref="ProjectMembershipRole.Excluded"/> means the user has no access at all (project hidden).
/// When no row exists the user inherits their workspace-level role.
/// </summary>
public sealed class ProjectMembership
{
    private ProjectMembership() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public ProjectMembershipRole Role { get; private set; }
    public Guid AddedByUserId { get; private set; }
    public DateTime AddedAtUtc { get; private set; } = DateTime.UtcNow;

    public Project? Project { get; private set; }

    public static ProjectMembership Create(
        Guid workspaceId, Guid projectId, Guid userId, ProjectMembershipRole role, Guid addedByUserId) => new()
    {
        WorkspaceId = workspaceId,
        ProjectId = projectId,
        UserId = userId,
        Role = role,
        AddedByUserId = addedByUserId,
    };

    public void ChangeRole(ProjectMembershipRole role) => Role = role;
}

public enum ProjectMembershipRole
{
    Owner = 0,
    Admin = 1,
    Tester = 2,
    Viewer = 3,
    Excluded = 99,
}
