using Azma.Runs.Api.Domain;
using Azma.Automation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Azma.Runs.Api.Infrastructure.Persistence.Configurations;

public sealed class ProjectRefConfig : IEntityTypeConfiguration<ProjectRef>
{
    public void Configure(EntityTypeBuilder<ProjectRef> b)
    {
        b.ToTable("project_refs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.Code).HasMaxLength(16).IsRequired();
        b.Property(x => x.IsArchived).HasDefaultValue(false);
        b.Property(x => x.NextRunSequence).HasDefaultValue(1);
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
        b.Property(x => x.AutomationScenarioPath).HasMaxLength(1000);
        b.Property(x => x.AutomationEngine).HasConversion<int>().HasDefaultValue(AutomationEngine.None);
        b.Property(x => x.AutomationStatus).HasConversion<int>().HasDefaultValue(AutomationStatus.None);
        b.Property(x => x.AutomationRecordingJson).HasColumnType("jsonb");
        b.Property(x => x.IsDeleted).HasDefaultValue(false);
        b.HasIndex(x => new { x.WorkspaceId, x.ProjectId });
    }
}

public sealed class TestRunConfig : IEntityTypeConfiguration<TestRun>
{
    public void Configure(EntityTypeBuilder<TestRun> b)
    {
        b.ToTable("runs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");

        b.HasIndex(x => new { x.WorkspaceId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.WorkspaceId, x.ProjectId, x.Status, x.DeletedAtUtc });

        b.HasMany(x => x.Cases)
            .WithOne()
            .HasForeignKey(c => c.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Comments)
            .WithOne()
            .HasForeignKey(c => c.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.AutomationExecutions)
            .WithOne()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RunCaseConfig : IEntityTypeConfiguration<RunCase>
{
    public void Configure(EntityTypeBuilder<RunCase> b)
    {
        b.ToTable("run_cases");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.TestCaseCode).HasMaxLength(32).IsRequired();
        b.Property(x => x.TestCaseTitle).HasMaxLength(300).IsRequired();
        b.Property(x => x.AutomationScenarioPath).HasMaxLength(1000);
        b.Property(x => x.AutomationEngine).HasConversion<int>().HasDefaultValue(AutomationEngine.None);
        b.Property(x => x.AutomationStatus).HasConversion<int>().HasDefaultValue(AutomationStatus.None);
        b.Property(x => x.AutomationRecordingJson).HasColumnType("jsonb");
        b.Property(x => x.CurrentStatus).HasConversion<int>();
        b.HasIndex(x => new { x.RunId, x.TestCaseId }).IsUnique();
        b.HasIndex(x => new { x.RunId, x.CurrentStatus });

        b.HasMany(x => x.Results)
            .WithOne()
            .HasForeignKey(r => r.RunCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RunResultConfig : IEntityTypeConfiguration<RunResult>
{
    public void Configure(EntityTypeBuilder<RunResult> b)
    {
        b.ToTable("run_results");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Comment).HasMaxLength(4000);
        b.Property(x => x.AutomationEngine).HasConversion<int>();
        b.Property(x => x.AutomationDetailJson).HasColumnType("jsonb");
        b.Property(x => x.ExecutedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
        b.HasIndex(x => new { x.RunCaseId, x.ExecutedAtUtc });
    }
}

public sealed class RunCommentConfig : IEntityTypeConfiguration<RunComment>
{
    public void Configure(EntityTypeBuilder<RunComment> b)
    {
        b.ToTable("run_comments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
        b.HasIndex(x => new { x.RunId, x.CreatedAtUtc });
        b.HasIndex(x => x.RunCaseId);
    }
}

public sealed class RunAutomationExecutionConfig : IEntityTypeConfiguration<RunAutomationExecution>
{
    public void Configure(EntityTypeBuilder<RunAutomationExecution> b)
    {
        b.ToTable("run_automation_executions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Trigger).HasMaxLength(32).IsRequired();
        b.Property(x => x.ErrorMessage).HasMaxLength(4000);
        b.Property(x => x.StartedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
        b.HasIndex(x => new { x.Status, x.StartedAtUtc });
        b.HasIndex(x => new { x.RunId, x.StartedAtUtc });
    }
}

public sealed class RunAttachmentConfig : IEntityTypeConfiguration<RunAttachment>
{
    public void Configure(EntityTypeBuilder<RunAttachment> b)
    {
        b.ToTable("run_attachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.RunId).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        b.Property(x => x.SizeBytes).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(512).IsRequired();
        b.Property(x => x.UploadedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");

        b.HasIndex(x => new { x.WorkspaceId, x.RunId, x.UploadedAtUtc });
        b.HasIndex(x => x.RunCaseId);
        b.HasIndex(x => x.RunResultId);

        b.HasOne<TestRun>()
            .WithMany()
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RunScheduleConfig : IEntityTypeConfiguration<RunSchedule>
{
    public void Configure(EntityTypeBuilder<RunSchedule> b)
    {
        b.ToTable("run_schedules");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Environment).HasMaxLength(300);
        b.Property(x => x.RecurrenceType).HasConversion<int>();
        b.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        b.Property(x => x.Gateway).HasMaxLength(1000);
        b.Property(x => x.Frontend).HasMaxLength(1000);
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");

        b.HasIndex(x => new { x.WorkspaceId, x.ProjectId, x.IsActive, x.DeletedAtUtc });
        b.HasIndex(x => new { x.IsActive, x.NextRunAtUtc, x.LockedUntilUtc });

        b.HasMany(x => x.Cases)
            .WithOne()
            .HasForeignKey(x => x.RunScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Executions)
            .WithOne()
            .HasForeignKey(x => x.RunScheduleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RunScheduleCaseConfig : IEntityTypeConfiguration<RunScheduleCase>
{
    public void Configure(EntityTypeBuilder<RunScheduleCase> b)
    {
        b.ToTable("run_schedule_cases");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.TestCaseCode).HasMaxLength(32).IsRequired();
        b.Property(x => x.TestCaseTitle).HasMaxLength(300).IsRequired();
        b.Property(x => x.AutomationScenarioPath).HasMaxLength(1000);
        b.Property(x => x.AutomationEngine).HasConversion<int>().HasDefaultValue(AutomationEngine.None);
        b.Property(x => x.AutomationStatus).HasConversion<int>().HasDefaultValue(AutomationStatus.None);
        b.Property(x => x.AutomationRecordingJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.RunScheduleId, x.TestCaseId }).IsUnique();
    }
}

public sealed class RunScheduleExecutionConfig : IEntityTypeConfiguration<RunScheduleExecution>
{
    public void Configure(EntityTypeBuilder<RunScheduleExecution> b)
    {
        b.ToTable("run_schedule_executions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Trigger).HasMaxLength(32).IsRequired();
        b.Property(x => x.ErrorMessage).HasMaxLength(4000);
        b.Property(x => x.StartedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
        b.HasIndex(x => new { x.RunScheduleId, x.StartedAtUtc });
        b.HasIndex(x => x.RunId);
    }
}
