namespace Azma.Runs.Api.Domain;

using Azma.Automation;

public sealed class TestRun
{
    private TestRun() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? MilestoneId { get; private set; }

    /// <summary>Human-friendly code: e.g. "WEB-R-001".</summary>
    public string Code { get; private set; } = default!;
    public int Sequence { get; private set; }

    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? Environment { get; private set; }
    public DateTime? DueDateUtc { get; private set; }

    public RunStatus Status { get; private set; } = RunStatus.Open;

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public bool IsDeleted => DeletedAtUtc is not null;

    public ICollection<RunCase> Cases { get; private set; } = new List<RunCase>();
    public ICollection<RunComment> Comments { get; private set; } = new List<RunComment>();
    public ICollection<RunAutomationExecution> AutomationExecutions { get; private set; } = new List<RunAutomationExecution>();

    public static TestRun Create(
        Guid workspaceId, Guid projectId, Guid? milestoneId,
        string code, int sequence, string name, string? description, Guid createdByUserId,
        string? environment = null, DateTime? dueDateUtc = null) => new()
    {
        WorkspaceId = workspaceId,
        ProjectId = projectId,
        MilestoneId = milestoneId,
        Code = code,
        Sequence = sequence,
        Name = name.Trim(),
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        Environment = string.IsNullOrWhiteSpace(environment) ? null : environment.Trim(),
        DueDateUtc = dueDateUtc,
        Status = RunStatus.Open,
        CreatedByUserId = createdByUserId,
    };

    public void Close(Guid byUserId)
    {
        if (Status == RunStatus.Closed) return;
        Status = RunStatus.Closed;
        ClosedAtUtc = DateTime.UtcNow;
        ClosedByUserId = byUserId;
    }

    public void Reopen()
    {
        Status = RunStatus.Open;
        ClosedAtUtc = null;
        ClosedByUserId = null;
    }

    public void SoftDelete()
    {
        if (DeletedAtUtc is null) DeletedAtUtc = DateTime.UtcNow;
    }

    public void UpdateMetadata(string? name, string? description, string? environment = null, DateTime? dueDateUtc = null)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
        if (description is not null) Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (environment is not null) Environment = string.IsNullOrWhiteSpace(environment) ? null : environment.Trim();
        if (dueDateUtc is not null) DueDateUtc = dueDateUtc;
    }
}

/// <summary>A test case included in a run; carries an immutable snapshot from creation time.</summary>
public sealed class RunCase
{
    private RunCase() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RunId { get; private set; }

    /// <summary>Source TestCaseId at the time of run creation.</summary>
    public Guid TestCaseId { get; private set; }

    public string TestCaseCode { get; private set; } = default!;
    public string TestCaseTitle { get; private set; } = default!;
    public int TestCaseVersion { get; private set; }
    public string? AutomationScenarioPath { get; private set; }
    public AutomationEngine AutomationEngine { get; private set; } = AutomationEngine.None;
    public AutomationStatus AutomationStatus { get; private set; } = AutomationStatus.None;
    public int? AutomationDefinitionVersion { get; private set; }
    public string? AutomationRecordingJson { get; private set; }

    public Guid? AssignedToUserId { get; private set; }

    /// <summary>Latest result status; updated when a new RunResult is recorded.</summary>
    public ResultStatus CurrentStatus { get; private set; } = ResultStatus.Untested;
    public DateTime? LastExecutedAtUtc { get; private set; }
    public Guid? LastExecutedByUserId { get; private set; }

    public ICollection<RunResult> Results { get; private set; } = new List<RunResult>();

    public static RunCase Snapshot(
        Guid runId,
        Guid testCaseId,
        string code,
        string title,
        int version,
        Guid? assignedToUserId,
        string? automationScenarioPath = null,
        AutomationEngine automationEngine = AutomationEngine.None,
        AutomationStatus automationStatus = AutomationStatus.None,
        int? automationDefinitionVersion = null,
        string? automationRecordingJson = null) => new()
    {
        RunId = runId,
        TestCaseId = testCaseId,
        TestCaseCode = code,
        TestCaseTitle = title,
        TestCaseVersion = version,
        AssignedToUserId = assignedToUserId,
        AutomationScenarioPath = NormalizeAutomationScenarioPath(automationScenarioPath),
        AutomationEngine = automationEngine,
        AutomationStatus = automationStatus,
        AutomationDefinitionVersion = automationDefinitionVersion,
        AutomationRecordingJson = NormalizeAutomationRecordingJson(automationRecordingJson),
        CurrentStatus = ResultStatus.Untested,
    };

    public void AssignTo(Guid? userId) => AssignedToUserId = userId;

    public void UpdateAutomationScenarioPath(string? automationScenarioPath) =>
        AutomationScenarioPath = NormalizeAutomationScenarioPath(automationScenarioPath);

    public void UpdateAutomationSnapshot(
        string? automationScenarioPath,
        AutomationEngine automationEngine,
        AutomationStatus automationStatus,
        int? automationDefinitionVersion,
        string? automationRecordingJson)
    {
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

    public RunResult RecordResult(
        ResultStatus status,
        string? comment,
        int? elapsedSeconds,
        Guid byUserId,
        AutomationEngine? automationEngine = null,
        string? automationDetailJson = null)
    {
        var result = RunResult.Create(Id, status, comment, elapsedSeconds, byUserId, automationEngine, automationDetailJson);
        Results.Add(result);
        CurrentStatus = status;
        LastExecutedAtUtc = result.ExecutedAtUtc;
        LastExecutedByUserId = byUserId;
        return result;
    }

    private static string? NormalizeAutomationScenarioPath(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeAutomationRecordingJson(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class RunResult
{
    private RunResult() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RunCaseId { get; private set; }
    public ResultStatus Status { get; private set; }
    public string? Comment { get; private set; }
    public int? ElapsedSeconds { get; private set; }
    public Guid ExecutedByUserId { get; private set; }
    public DateTime ExecutedAtUtc { get; private set; } = DateTime.UtcNow;
    public AutomationEngine? AutomationEngine { get; private set; }
    public string? AutomationDetailJson { get; private set; }

    public static RunResult Create(
        Guid runCaseId,
        ResultStatus status,
        string? comment,
        int? elapsedSeconds,
        Guid byUserId,
        AutomationEngine? automationEngine = null,
        string? automationDetailJson = null) => new()
    {
        RunCaseId = runCaseId,
        Status = status,
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
        ElapsedSeconds = elapsedSeconds is > 0 ? elapsedSeconds : null,
        ExecutedByUserId = byUserId,
        AutomationEngine = automationEngine,
        AutomationDetailJson = string.IsNullOrWhiteSpace(automationDetailJson) ? null : automationDetailJson.Trim(),
    };
}

public sealed class RunComment
{
    private RunComment() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RunId { get; private set; }
    public Guid? RunCaseId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Body { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public static RunComment Create(Guid runId, Guid? runCaseId, Guid authorUserId, string body) => new()
    {
        RunId = runId,
        RunCaseId = runCaseId,
        AuthorUserId = authorUserId,
        Body = body.Trim(),
    };
}

public sealed class RunAutomationExecution
{
    private RunAutomationExecution() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RunId { get; private set; }
    public RunAutomationExecutionStatus Status { get; private set; } = RunAutomationExecutionStatus.Queued;
    public string Trigger { get; private set; } = "manual";
    public DateTime StartedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; private set; }
    public int TotalCases { get; private set; }
    public int Passed { get; private set; }
    public int Failed { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static RunAutomationExecution Queue(Guid runId, string trigger, int totalCases) => new()
    {
        RunId = runId,
        Trigger = string.IsNullOrWhiteSpace(trigger) ? "manual" : trigger.Trim(),
        TotalCases = totalCases,
        Status = RunAutomationExecutionStatus.Queued,
    };

    public void Start()
    {
        Status = RunAutomationExecutionStatus.Running;
        StartedAtUtc = DateTime.UtcNow;
        ErrorMessage = null;
    }

    public void Complete(int passed, int failed)
    {
        Status = RunAutomationExecutionStatus.Completed;
        Passed = passed;
        Failed = failed;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Fail(string message)
    {
        Status = RunAutomationExecutionStatus.Failed;
        ErrorMessage = string.IsNullOrWhiteSpace(message) ? "Automatic run execution failed." : message.Trim();
        CompletedAtUtc = DateTime.UtcNow;
    }
}

public enum RunAutomationExecutionStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
}
