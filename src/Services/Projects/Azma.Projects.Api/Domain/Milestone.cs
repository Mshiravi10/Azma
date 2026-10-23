namespace Azma.Projects.Api.Domain;

public enum MilestoneStatus
{
    Open = 0,
    Closed = 1,
}

public sealed class Milestone
{
    private Milestone() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public DateTime? DueOnUtc { get; private set; }
    public MilestoneStatus Status { get; private set; } = MilestoneStatus.Open;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; private set; }

    public Project? Project { get; private set; }

    public static Milestone Create(Guid workspaceId, Guid projectId, string name, string? description, DateTime? dueOnUtc) => new()
    {
        WorkspaceId = workspaceId,
        ProjectId = projectId,
        Name = name.Trim(),
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        DueOnUtc = dueOnUtc,
    };

    public void Update(string name, string? description, DateTime? dueOnUtc)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DueOnUtc = dueOnUtc;
    }

    public void Close()
    {
        if (Status == MilestoneStatus.Closed) return;
        Status = MilestoneStatus.Closed;
        ClosedAtUtc = DateTime.UtcNow;
    }

    public void Reopen()
    {
        Status = MilestoneStatus.Open;
        ClosedAtUtc = null;
    }
}
