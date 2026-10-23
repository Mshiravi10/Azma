using Azma.Projects.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Azma.Projects.Api.Infrastructure.Persistence.Configurations;

public sealed class ProjectMembershipConfiguration : IEntityTypeConfiguration<ProjectMembership>
{
    public void Configure(EntityTypeBuilder<ProjectMembership> b)
    {
        b.ToTable("project_memberships");
        b.HasKey(x => x.Id);
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.Role).HasConversion<int>().IsRequired();
        b.Property(x => x.AddedByUserId).IsRequired();
        b.Property(x => x.AddedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");

        b.HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique();
        b.HasIndex(x => x.ProjectId)
            .IsUnique()
            .HasFilter("\"Role\" = 0");
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.WorkspaceId);

        b.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
