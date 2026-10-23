using Azma.TestCases.Api.Domain;
using Azma.Automation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace Azma.TestCases.Api.Infrastructure.Persistence.Configurations;

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
        b.Property(x => x.NextSequence).HasDefaultValue(1);
        b.HasIndex(x => x.WorkspaceId);
    }
}

public sealed class TestCaseConfig : IEntityTypeConfiguration<TestCase>
{
    public void Configure(EntityTypeBuilder<TestCase> b)
    {
        b.ToTable("testcases");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Preconditions).HasMaxLength(4000);
        b.Property(x => x.AutomationScenarioPath).HasMaxLength(1000);
        b.Property(x => x.AutomationEngine).HasConversion<int>().HasDefaultValue(AutomationEngine.None);
        b.Property(x => x.AutomationStatus).HasConversion<int>().HasDefaultValue(AutomationStatus.None);
        b.Property(x => x.AutomationRecordingJson).HasColumnType("jsonb");
        b.HasIndex(x => x.AutomationPrerequisiteTestCaseId);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.Priority).HasConversion<int>();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
        b.Property(x => x.LastExecutionStatus).HasMaxLength(32);

        var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var dictConverter = new ValueConverter<Dictionary<string, JsonElement>, string>(
            v => JsonSerializer.Serialize(v, jsonOpts),
            v => string.IsNullOrWhiteSpace(v)
                ? new Dictionary<string, JsonElement>()
                : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(v, jsonOpts) ?? new());
        var dictComparer = new ValueComparer<Dictionary<string, JsonElement>>(
            (a, c) => JsonSerializer.Serialize(a, jsonOpts) == JsonSerializer.Serialize(c, jsonOpts),
            v => v == null ? 0 : JsonSerializer.Serialize(v, jsonOpts).GetHashCode(),
            v => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(v, jsonOpts), jsonOpts)!);

        b.Property(x => x.CustomFields)
            .HasColumnName("custom_fields")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .HasConversion(dictConverter, dictComparer);

        b.HasIndex(x => new { x.WorkspaceId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.SuiteId, x.SectionId, x.DeletedAtUtc });
        b.HasIndex(x => new { x.WorkspaceId, x.DeletedAtUtc });
        b.HasIndex(x => new { x.WorkspaceId, x.ProjectId, x.LastExecutedAtUtc });

        b.HasMany(x => x.Steps)
            .WithOne()
            .HasForeignKey(s => s.TestCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Tags)
            .WithOne()
            .HasForeignKey(t => t.TestCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.History)
            .WithOne()
            .HasForeignKey(v => v.TestCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AutomationRecordingSessionConfig : IEntityTypeConfiguration<AutomationRecordingSession>
{
    public void Configure(EntityTypeBuilder<AutomationRecordingSession> b)
    {
        b.ToTable("automation_recording_sessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.TestCaseId).IsRequired();
        b.Property(x => x.SessionToken).HasMaxLength(128).IsRequired();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.EventsJson).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
        b.Property(x => x.StartedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
        b.HasIndex(x => new { x.WorkspaceId, x.TestCaseId, x.StartedAtUtc });
        b.HasIndex(x => x.SessionToken).IsUnique();
    }
}

public sealed class TestStepConfig : IEntityTypeConfiguration<TestStep>
{
    public void Configure(EntityTypeBuilder<TestStep> b)
    {
        b.ToTable("test_steps");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Action).HasMaxLength(2000).IsRequired();
        b.Property(x => x.ExpectedResult).HasMaxLength(2000);
        b.HasIndex(x => new { x.TestCaseId, x.Order }).IsUnique();
    }
}

public sealed class TestCaseTagConfig : IEntityTypeConfiguration<TestCaseTag>
{
    public void Configure(EntityTypeBuilder<TestCaseTag> b)
    {
        b.ToTable("testcase_tags");
        b.HasKey(x => new { x.TestCaseId, x.Tag });
        b.Property(x => x.Tag).HasMaxLength(48).IsRequired();
    }
}

public sealed class TestCaseVersionConfig : IEntityTypeConfiguration<TestCaseVersion>
{
    public void Configure(EntityTypeBuilder<TestCaseVersion> b)
    {
        b.ToTable("testcase_versions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.SnapshotJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.ChangeReason).HasMaxLength(500);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
        b.HasIndex(x => new { x.TestCaseId, x.Version }).IsUnique();
    }
}

public sealed class CustomFieldDefinitionConfig : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> b)
    {
        b.ToTable("custom_field_definitions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.WorkspaceId).IsRequired();
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.Property(x => x.Label).HasMaxLength(120).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.Required).HasDefaultValue(false);
        b.Property(x => x.Position).HasDefaultValue(0);
        b.Property(x => x.IsArchived).HasDefaultValue(false);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");

        var optsConverter = new ValueConverter<List<string>?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null));
        var optsComparer = new ValueComparer<List<string>?>(
            (a, c) => (a == null && c == null) || (a != null && c != null && a.SequenceEqual(c)),
            v => v == null ? 0 : v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v == null ? null : v.ToList());

        b.Property(x => x.Options)
            .HasColumnName("options")
            .HasColumnType("jsonb")
            .HasConversion(optsConverter, optsComparer);

        b.HasIndex(x => new { x.WorkspaceId, x.ProjectId, x.Key }).IsUnique();
    }
}
