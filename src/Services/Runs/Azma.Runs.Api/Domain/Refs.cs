namespace Azma.Runs.Api.Domain;

using Azma.Automation;

/// <summary>Local read-side projection of a Project from the Projects service.</summary>
public sealed class ProjectRef
{
    private ProjectRef() { }

    public Guid Id { get; private set; }            // = ProjectId
    public Guid WorkspaceId { get; private set; }
    public string Code { get; private set; } = default!;
    public bool IsArchived { get; private set; }
    public int NextRunSequence { get; private set; } = 1;

    public static ProjectRef Create(Guid projectId, Guid workspaceId, string code) => new()
    {
        Id = projectId,
        WorkspaceId = workspaceId,
        Code = code.ToUpperInvariant(),
        IsArchived = false,
        NextRunSequence = 1,
    };

    public void Archive() => IsArchived = true;
    public void Restore() => IsArchived = false;

    /// <summary>Reserve and return the next run sequence; caller must save.</summary>
    public int AllocateRunSequence()
    {
        var seq = NextRunSequence;
        NextRunSequence = seq + 1;
        return seq;
    }
}

/// <summary>Local read-side projection of a TestCase. Updated by TestCase events.</summary>
public sealed class TestCaseRef
{
    private TestCaseRef() { }

    public Guid Id { get; private set; }            // = TestCaseId
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public int Version { get; private set; } = 1;
    public string? AutomationScenarioPath { get; private set; }
    public AutomationEngine AutomationEngine { get; private set; } = AutomationEngine.None;
    public AutomationStatus AutomationStatus { get; private set; } = AutomationStatus.None;
    public int? AutomationDefinitionVersion { get; private set; }
    public string? AutomationRecordingJson { get; private set; }
    public bool IsDeleted { get; private set; }

    public static TestCaseRef Create(
        Guid id,
        Guid workspaceId,
        Guid projectId,
        string code,
        string title,
        string? automationScenarioPath = null,
        AutomationEngine automationEngine = AutomationEngine.None,
        AutomationStatus automationStatus = AutomationStatus.None,
        int? automationDefinitionVersion = null,
        string? automationRecordingJson = null) => new()
    {
        Id = id,
        WorkspaceId = workspaceId,
        ProjectId = projectId,
        Code = code,
        Title = title,
        Version = 1,
        AutomationScenarioPath = NormalizeAutomationScenarioPath(automationScenarioPath),
        AutomationEngine = automationEngine,
        AutomationStatus = automationStatus,
        AutomationDefinitionVersion = automationDefinitionVersion,
        AutomationRecordingJson = NormalizeAutomationRecordingJson(automationRecordingJson),
        IsDeleted = false,
    };

    public void Apply(
        string code,
        string title,
        int version,
        string? automationScenarioPath,
        AutomationEngine automationEngine,
        AutomationStatus automationStatus,
        int? automationDefinitionVersion,
        string? automationRecordingJson)
    {
        Code = code;
        Title = title;
        if (version > Version) Version = version;
        AutomationScenarioPath = NormalizeAutomationScenarioPath(automationScenarioPath);
        AutomationEngine = automationEngine;
        AutomationStatus = automationStatus;
        AutomationDefinitionVersion = automationDefinitionVersion;
        AutomationRecordingJson = NormalizeAutomationRecordingJson(automationRecordingJson);
    }

    public bool HasAutomation =>
        !string.IsNullOrWhiteSpace(AutomationScenarioPath) ||
        (AutomationEngine == AutomationEngine.ScenarioRecording &&
         AutomationStatus == AutomationStatus.Published &&
         !string.IsNullOrWhiteSpace(AutomationRecordingJson));

    public void MarkDeleted() => IsDeleted = true;

    private static string? NormalizeAutomationScenarioPath(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeAutomationRecordingJson(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
