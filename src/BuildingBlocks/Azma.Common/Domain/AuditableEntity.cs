namespace Azma.Common.Domain;

public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

public abstract class TenantEntity : AuditableEntity, ITenantEntity
{
    public Guid WorkspaceId { get; set; }
}

public interface ITenantEntity
{
    Guid WorkspaceId { get; set; }
}
