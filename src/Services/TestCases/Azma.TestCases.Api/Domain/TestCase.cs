namespace Azma.TestCases.Api.Domain;

using Azma.Automation;
using System.Text.Json;

public sealed class TestCase
{
    private TestCase() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? SuiteId { get; private set; }
    public Guid? SectionId { get; private set; }

    /// <summary>Human-friendly code: e.g. "WEB-T-001". Unique per workspace.</summary>
    public string Code { get; private set; } = default!;

    /// <summary>The numeric portion of <see cref="Code"/>, allocated per project.</summary>
    public int Sequence { get; private set; }

    public string Title { get; private set; } = default!;
    public string? Preconditions { get; private set; }
    public string? AutomationScenarioPath { get; private set; }
    public AutomationEngine AutomationEngine { get; private set; } = AutomationEngine.None;
    public AutomationStatus AutomationStatus { get; private set; } = AutomationStatus.None;
    public int? AutomationDefinitionVersion { get; private set; }
    public string? AutomationRecordingJson { get; private set; }
    public Guid? AutomationPrerequisiteTestCaseId { get; private set; }
    public TestCaseType Type { get; private set; } = TestCaseType.Functional;
    public TestCasePriority Priority { get; private set; } = TestCasePriority.Medium;
    public TestCaseStatus Status { get; private set; } = TestCaseStatus.Draft;

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public Guid? UpdatedByUserId { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public bool IsDeleted => DeletedAtUtc is not null;

    public DateTime? LastExecutedAtUtc { get; private set; }
    public string? LastExecutionStatus { get; private set; }
    public Guid? LastExecutionRunId { get; private set; }
    public Guid? LastExecutionRunCaseId { get; private set; }
    public Guid? LastExecutedByUserId { get; private set; }

    /// <summary>Monotonically increasing per-testcase version, bumped on each update.</summary>
    public int Version { get; private set; } = 1;

    public ICollection<TestStep> Steps { get; private set; } = new List<TestStep>();
    public ICollection<TestCaseTag> Tags { get; private set; } = new List<TestCaseTag>();
    public ICollection<TestCaseVersion> History { get; private set; } = new List<TestCaseVersion>();

    /// <summary>Per-project custom fields. Keys correspond to <see cref="CustomFieldDefinition.Key"/>.</summary>
    public Dictionary<string, JsonElement> CustomFields { get; private set; } = new();

    public void SetCustomFields(Dictionary<string, JsonElement> values)
    {
        CustomFields = values ?? new Dictionary<string, JsonElement>();
    }

    public static TestCase Create(
        Guid workspaceId, Guid projectId, Guid? suiteId, Guid? sectionId,
        string code, int sequence, string title, string? preconditions,
        TestCaseType type, TestCasePriority priority, Guid createdByUserId,
        TestCaseStatus status = TestCaseStatus.Active,
        string? automationScenarioPath = null)
    {
        return new TestCase
        {
            WorkspaceId = workspaceId,
            ProjectId = projectId,
            SuiteId = suiteId,
            SectionId = sectionId,
            Code = code,
            Sequence = sequence,
            Title = title.Trim(),
            Preconditions = string.IsNullOrWhiteSpace(preconditions) ? null : preconditions.Trim(),
            AutomationScenarioPath = NormalizeAutomationScenarioPath(automationScenarioPath),
            Type = type,
            Priority = priority,
            Status = status,
            CreatedByUserId = createdByUserId,
        };
    }

    public void Update(
        string title, string? preconditions,
        TestCaseType type, TestCasePriority priority, TestCaseStatus status,
        Guid? suiteId, Guid? sectionId, Guid updatedByUserId,
        string? automationScenarioPath)
    {
        Title = title.Trim();
        Preconditions = string.IsNullOrWhiteSpace(preconditions) ? null : preconditions.Trim();
        AutomationScenarioPath = NormalizeAutomationScenarioPath(automationScenarioPath);
        Type = type;
        Priority = priority;
        Status = status;
        SuiteId = suiteId;
        SectionId = sectionId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
        Version += 1;
    }

    public void SetAutomationScenarioPath(string? automationScenarioPath) =>
        AutomationScenarioPath = NormalizeAutomationScenarioPath(automationScenarioPath);

    public void SetAutomationPrerequisite(Guid? prerequisiteTestCaseId, Guid updatedByUserId)
    {
        AutomationPrerequisiteTestCaseId = prerequisiteTestCaseId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
        Version += 1;
    }

    public void SaveRecordingDraft(string recordingJson, Guid updatedByUserId)
    {
        AutomationEngine = AutomationEngine.ScenarioRecording;
        AutomationStatus = AutomationStatus.Draft;
        AutomationDefinitionVersion = (AutomationDefinitionVersion ?? 0) + 1;
        AutomationRecordingJson = NormalizeRecordingJson(recordingJson);
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
        Version += 1;
    }

    public void MarkRecordingValidated(Guid updatedByUserId)
    {
        if (AutomationEngine != AutomationEngine.ScenarioRecording ||
            string.IsNullOrWhiteSpace(AutomationRecordingJson))
            return;

        AutomationStatus = AutomationStatus.Validated;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void PublishRecording(Guid updatedByUserId)
    {
        if (AutomationEngine != AutomationEngine.ScenarioRecording ||
            string.IsNullOrWhiteSpace(AutomationRecordingJson))
            return;

        AutomationStatus = AutomationStatus.Published;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkRecordingBroken(Guid updatedByUserId)
    {
        if (AutomationEngine != AutomationEngine.ScenarioRecording)
            return;

        AutomationStatus = AutomationStatus.Broken;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool RecordExecution(
        string status,
        Guid runId,
        Guid runCaseId,
        Guid executedByUserId,
        DateTime executedAtUtc)
    {
        if (LastExecutedAtUtc is not null && executedAtUtc < LastExecutedAtUtc.Value)
            return false;

        LastExecutedAtUtc = executedAtUtc;
        LastExecutionStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        LastExecutionRunId = runId;
        LastExecutionRunCaseId = runCaseId;
        LastExecutedByUserId = executedByUserId;
        return true;
    }

    public void SoftDelete()
    {
        if (DeletedAtUtc is null) DeletedAtUtc = DateTime.UtcNow;
    }

    public void Restore() => DeletedAtUtc = null;

    private static string? NormalizeAutomationScenarioPath(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeRecordingJson(string value)
    {
        using var document = JsonDocument.Parse(value);
        return JsonSerializer.Serialize(document.RootElement);
    }
}

public sealed class AutomationRecordingSession
{
    private const int MaxStoredEvents = 10_000;
    private AutomationRecordingSession() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid TestCaseId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string SessionToken { get; private set; } = default!;
    public AutomationRecordingSessionStatus Status { get; private set; } = AutomationRecordingSessionStatus.Open;
    public string EventsJson { get; private set; } = "[]";
    public DateTime StartedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }

    public static AutomationRecordingSession Start(
        Guid workspaceId,
        Guid projectId,
        Guid testCaseId,
        Guid createdByUserId,
        string sessionToken,
        DateTime expiresAtUtc) => new()
        {
            WorkspaceId = workspaceId,
            ProjectId = projectId,
            TestCaseId = testCaseId,
            CreatedByUserId = createdByUserId,
            SessionToken = sessionToken,
            ExpiresAtUtc = expiresAtUtc,
        };

    public bool TokenMatches(string? token) =>
        !string.IsNullOrWhiteSpace(token) &&
        string.Equals(SessionToken, token.Trim(), StringComparison.Ordinal);

    public bool IsExpired(DateTime nowUtc) => nowUtc > ExpiresAtUtc;

    public void AppendEvents(IEnumerable<JsonElement> events)
    {
        var existing = string.IsNullOrWhiteSpace(EventsJson)
            ? new List<JsonElement>()
            : JsonSerializer.Deserialize<List<JsonElement>>(EventsJson) ?? new List<JsonElement>();

        var eventIds = existing
            .Select(EventId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var item in events)
        {
            var eventId = EventId(item);
            if (!string.IsNullOrWhiteSpace(eventId) && !eventIds.Add(eventId))
                continue;

            if (IsScenarioSnapshot(item))
                existing.RemoveAll(IsScenarioSnapshot);

            existing.Add(item.Clone());
        }

        if (existing.Count > MaxStoredEvents)
        {
            var latestSnapshot = existing.LastOrDefault(IsScenarioSnapshot);
            existing = existing
                .Where(item => !IsScenarioSnapshot(item))
                .TakeLast(MaxStoredEvents - (latestSnapshot.ValueKind == JsonValueKind.Undefined ? 0 : 1))
                .ToList();
            if (latestSnapshot.ValueKind != JsonValueKind.Undefined)
                existing.Add(latestSnapshot.Clone());
        }

        EventsJson = JsonSerializer.Serialize(existing);
    }

    private static string? EventId(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object &&
        item.TryGetProperty("eventId", out var id) &&
        id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;

    private static bool IsScenarioSnapshot(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object &&
        item.TryGetProperty("type", out var type) &&
        type.ValueKind == JsonValueKind.String &&
        string.Equals(type.GetString(), "scenarioSnapshot", StringComparison.OrdinalIgnoreCase);

    public void Finish()
    {
        Status = AutomationRecordingSessionStatus.Finished;
        FinishedAtUtc = DateTime.UtcNow;
    }
}

public enum AutomationRecordingSessionStatus
{
    Open = 0,
    Finished = 1,
    Expired = 2,
}

public sealed class TestStep
{
    private TestStep() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TestCaseId { get; private set; }
    public int Order { get; private set; }
    public string Action { get; private set; } = default!;
    public string? ExpectedResult { get; private set; }

    public static TestStep Create(Guid testCaseId, int order, string action, string? expected) => new()
    {
        TestCaseId = testCaseId,
        Order = order,
        Action = action.Trim(),
        ExpectedResult = string.IsNullOrWhiteSpace(expected) ? null : expected.Trim(),
    };
}

public sealed class TestCaseTag
{
    private TestCaseTag() { }

    public Guid TestCaseId { get; private set; }
    public string Tag { get; private set; } = default!;

    public static TestCaseTag Create(Guid testCaseId, string tag) => new()
    {
        TestCaseId = testCaseId,
        Tag = tag.Trim().ToLowerInvariant(),
    };
}

public sealed class TestCaseVersion
{
    private TestCaseVersion() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TestCaseId { get; private set; }
    public int Version { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public string? ChangeReason { get; private set; }

    /// <summary>JSON snapshot of the test case + its steps + tags at this version.</summary>
    public string SnapshotJson { get; private set; } = "{}";

    public static TestCaseVersion Create(Guid testCaseId, int version, Guid createdByUserId, string snapshotJson, string? reason) => new()
    {
        TestCaseId = testCaseId,
        Version = version,
        CreatedByUserId = createdByUserId,
        SnapshotJson = snapshotJson,
        ChangeReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
    };
}
