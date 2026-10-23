using Azma.Reporting.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Azma.Reporting.Api.Infrastructure.Persistence.Configurations;

public sealed class ProjectRefConfig : IEntityTypeConfiguration<ProjectRef>
{
    public void Configure(EntityTypeBuilder<ProjectRef> b)
    {
        b.ToTable("project_refs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.Code).HasMaxLength(16).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.IsArchived).HasDefaultValue(false);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
        b.HasIndex(x => x.WorkspaceId);
    }
}

public sealed class TestCaseRefConfig : IEntityTypeConfiguration<TestCaseRef>
{
    public void Configure(EntityTypeBuilder<TestCaseRef> b)
    {
        b.ToTable("testcase_refs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.IsDeleted).HasDefaultValue(false);
        b.HasIndex(x => new { x.WorkspaceId, x.ProjectId });
    }
}

public sealed class RunRefConfig : IEntityTypeConfiguration<RunRef>
{
    public void Configure(EntityTypeBuilder<RunRef> b)
    {
        b.ToTable("runs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
        b.HasIndex(x => new { x.WorkspaceId, x.ProjectId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.WorkspaceId, x.Status });
    }
}

public sealed class ResultEventConfig : IEntityTypeConfiguration<ResultEvent>
{
    public void Configure(EntityTypeBuilder<ResultEvent> b)
    {
        b.ToTable("result_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.RunId).IsRequired();
        b.Property(x => x.RunCaseId).IsRequired();
        b.Property(x => x.TestCaseId).IsRequired();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.ExecutedByUserId).IsRequired();
        b.HasIndex(x => new { x.WorkspaceId, x.ProjectId, x.ExecutedAtUtc });
        b.HasIndex(x => new { x.WorkspaceId, x.TestCaseId, x.Status });
        b.HasIndex(x => new { x.WorkspaceId, x.ExecutedByUserId, x.ExecutedAtUtc });
        b.HasIndex(x => x.RunId);
    }
}
