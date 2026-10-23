namespace Azma.Identity.Api.Domain;

public sealed class Workspace
{
    private Workspace() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public Guid OwnerUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public ICollection<Membership> Memberships { get; private set; } = new List<Membership>();
    public ICollection<Invitation> Invitations { get; private set; } = new List<Invitation>();

    public static Workspace Create(string name, string slug, Guid ownerUserId) => new()
    {
        Name = name.Trim(),
        Slug = slug.Trim().ToLowerInvariant(),
        OwnerUserId = ownerUserId,
    };

    public void Rename(string name) => Name = name.Trim();
}
