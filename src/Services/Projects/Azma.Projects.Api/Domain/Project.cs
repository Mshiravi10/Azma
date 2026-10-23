namespace Azma.Projects.Api.Domain;

public sealed class Project
{
    private Project() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ArchivedAtUtc { get; private set; }
    public bool IsArchived => ArchivedAtUtc is not null;

    public ICollection<Milestone> Milestones { get; private set; } = new List<Milestone>();
    public ICollection<Suite> Suites { get; private set; } = new List<Suite>();

    public static Project Create(Guid workspaceId, string name, string code, string? description, Guid createdByUserId) => new()
    {
        WorkspaceId = workspaceId,
        Name = name.Trim(),
        Code = code.Trim().ToUpperInvariant(),
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        CreatedByUserId = createdByUserId,
    };

    public void Update(string name, string? description)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void Archive()
    {
        if (ArchivedAtUtc is null) ArchivedAtUtc = DateTime.UtcNow;
    }

    public void Restore() => ArchivedAtUtc = null;
}
