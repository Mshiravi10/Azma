namespace Azma.Projects.Api.Domain;

/// <summary>
/// Top-level container for test cases inside a Project. Each Suite owns a tree of Sections.
/// </summary>
public sealed class Suite
{
    private Suite() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public int Position { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public Project? Project { get; private set; }
    public ICollection<Section> Sections { get; private set; } = new List<Section>();

    public static Suite Create(Guid workspaceId, Guid projectId, string name, string? description, int position) => new()
    {
        WorkspaceId = workspaceId,
        ProjectId = projectId,
        Name = name.Trim(),
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        Position = position,
    };

    public void Update(string name, string? description)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void SetPosition(int position) => Position = position;
}
