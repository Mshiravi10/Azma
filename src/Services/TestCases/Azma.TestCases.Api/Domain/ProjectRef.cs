namespace Azma.TestCases.Api.Domain;

/// <summary>
/// Local read-side projection of a Project from the Projects service.
/// Populated by ProjectCreated / ProjectArchived consumers.
/// We need it locally so we can validate FK ownership and atomically allocate
/// per-project sequence numbers for test case codes (PROJCODE-T-NNN).
/// </summary>
public sealed class ProjectRef
{
    private ProjectRef() { }

    public Guid Id { get; private set; }            // = ProjectId
    public Guid WorkspaceId { get; private set; }
    public string Code { get; private set; } = default!;
    public bool IsArchived { get; private set; }
    public int NextSequence { get; private set; } = 1;

    public static ProjectRef Create(Guid projectId, Guid workspaceId, string code) => new()
    {
        Id = projectId,
        WorkspaceId = workspaceId,
        Code = code.ToUpperInvariant(),
        IsArchived = false,
        NextSequence = 1,
    };

    public void Archive() => IsArchived = true;
    public void Restore() => IsArchived = false;

    /// <summary>Reserve and return the next sequence number; caller must save.</summary>
    public int AllocateSequence()
    {
        var seq = NextSequence;
        NextSequence = seq + 1;
        return seq;
    }
}
