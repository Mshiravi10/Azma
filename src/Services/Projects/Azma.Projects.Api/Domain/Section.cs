namespace Azma.Projects.Api.Domain;

/// <summary>
/// Recursive folder under a Suite. ParentId = null means it's a top-level Section of the Suite.
/// </summary>
public sealed class Section
{
    private Section() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid SuiteId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public int Position { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public Suite? Suite { get; private set; }
    public Section? Parent { get; private set; }
    public ICollection<Section> Children { get; private set; } = new List<Section>();

    public static Section Create(
        Guid workspaceId, Guid projectId, Guid suiteId, Guid? parentId,
        string name, string? description, int position) => new()
    {
        WorkspaceId = workspaceId,
        ProjectId = projectId,
        SuiteId = suiteId,
        ParentId = parentId,
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

    public void MoveTo(Guid? newParentId, int newPosition)
    {
        ParentId = newParentId;
        Position = newPosition;
    }
}
