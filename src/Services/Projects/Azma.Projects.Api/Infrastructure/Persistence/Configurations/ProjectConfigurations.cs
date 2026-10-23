using Azma.Projects.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Azma.Projects.Api.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("projects");
        b.HasKey(x => x.Id);
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.Code).HasMaxLength(16).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
        b.HasIndex(x => new { x.WorkspaceId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.WorkspaceId, x.ArchivedAtUtc });

        b.HasMany(x => x.Milestones)
            .WithOne(x => x.Project!)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Suites)
            .WithOne(x => x.Project!)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> b)
    {
        b.ToTable("milestones");
        b.HasKey(x => x.Id);
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
        b.HasIndex(x => new { x.ProjectId, x.Status });
        b.HasIndex(x => x.WorkspaceId);
    }
}

public sealed class SuiteConfiguration : IEntityTypeConfiguration<Suite>
{
    public void Configure(EntityTypeBuilder<Suite> b)
    {
        b.ToTable("suites");
        b.HasKey(x => x.Id);
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
        b.HasIndex(x => new { x.ProjectId, x.Position });
        b.HasIndex(x => x.WorkspaceId);

        b.HasMany(x => x.Sections)
            .WithOne(x => x.Suite!)
            .HasForeignKey(x => x.SuiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> b)
    {
        b.ToTable("sections");
        b.HasKey(x => x.Id);
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
        b.HasIndex(x => new { x.SuiteId, x.ParentId, x.Position });
        b.HasIndex(x => x.WorkspaceId);

        b.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
