namespace Azma.Runs.Api.Domain;

using Azma.Automation;

public sealed class RunSchedule
{
    private RunSchedule() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? Environment { get; private set; }
    public ScheduleRecurrenceType RecurrenceType { get; private set; }
    public int TimeOfDayMinutes { get; private set; }
    public int WeekDaysMask { get; private set; }
    public string TimeZoneId { get; private set; } = "Asia/Tehran";
    public DateTime? StartAtUtc { get; private set; }
    public DateTime? EndAtUtc { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? NextRunAtUtc { get; private set; }
    public DateTime? LastRunAtUtc { get; private set; }
    public Guid? LastRunId { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public bool IsDeleted => DeletedAtUtc is not null;

    public int Loops { get; private set; } = 1;
    public int Threads { get; private set; } = 1;
    public int Rampup { get; private set; }
    public bool Skippable { get; private set; }
    public string? Gateway { get; private set; }
    public string? Frontend { get; private set; }

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; private set; }

    public ICollection<RunScheduleCase> Cases { get; private set; } = new List<RunScheduleCase>();
    public ICollection<RunScheduleExecution> Executions { get; private set; } = new List<RunScheduleExecution>();

    public static RunSchedule Create(
        Guid workspaceId,
        Guid projectId,
        string name,
        string? description,
        string? environment,
        ScheduleRecurrenceType recurrenceType,
        int timeOfDayMinutes,
        int weekDaysMask,
        string? timeZoneId,
        DateTime? startAtUtc,
        DateTime? endAtUtc,
        Guid createdByUserId,
        int loops,
        int threads,
        int rampup,
        bool skippable,
        string? gateway,
        string? frontend) => new()
    {
        WorkspaceId = workspaceId,
        ProjectId = projectId,
        Name = name.Trim(),
        Description = Normalize(description),
        Environment = Normalize(environment),
        RecurrenceType = recurrenceType,
        TimeOfDayMinutes = Math.Clamp(timeOfDayMinutes, 0, 1439),
        WeekDaysMask = weekDaysMask,
        TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Tehran" : timeZoneId.Trim(),
        StartAtUtc = startAtUtc,
        EndAtUtc = endAtUtc,
        CreatedByUserId = createdByUserId,
        Loops = Math.Max(1, loops),
        Threads = Math.Max(1, threads),
        Rampup = Math.Max(0, rampup),
        Skippable = skippable,
        Gateway = Normalize(gateway),
        Frontend = Normalize(frontend),
        IsActive = true,
    };

    public void Update(
        string? name,
        string? description,
        string? environment,
        ScheduleRecurrenceType? recurrenceType,
        int? timeOfDayMinutes,
        int? weekDaysMask,
        string? timeZoneId,
        DateTime? startAtUtc,
        DateTime? endAtUtc,
        int? loops,
        int? threads,
        int? rampup,
        bool? skippable,
        string? gateway,
        string? frontend)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
        if (description is not null) Description = Normalize(description);
        if (environment is not null) Environment = Normalize(environment);
        if (recurrenceType is not null) RecurrenceType = recurrenceType.Value;
        if (timeOfDayMinutes is not null) TimeOfDayMinutes = Math.Clamp(timeOfDayMinutes.Value, 0, 1439);
        if (weekDaysMask is not null) WeekDaysMask = weekDaysMask.Value;
        if (!string.IsNullOrWhiteSpace(timeZoneId)) TimeZoneId = timeZoneId.Trim();
        if (startAtUtc is not null) StartAtUtc = startAtUtc;
        if (endAtUtc is not null) EndAtUtc = endAtUtc;
        if (loops is not null) Loops = Math.Max(1, loops.Value);
        if (threads is not null) Threads = Math.Max(1, threads.Value);
        if (rampup is not null) Rampup = Math.Max(0, rampup.Value);
        if (skippable is not null) Skippable = skippable.Value;
        if (gateway is not null) Gateway = Normalize(gateway);
        if (frontend is not null) Frontend = Normalize(frontend);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetNextRun(DateTime? nextRunAtUtc) => NextRunAtUtc = nextRunAtUtc;

    public void Pause()
    {
        IsActive = false;
        LockedUntilUtc = null;
    }

    public void Resume(DateTime? nextRunAtUtc)
    {
        IsActive = true;
        NextRunAtUtc = nextRunAtUtc;
        LockedUntilUtc = null;
    }

    public void QueueNow()
    {
        NextRunAtUtc = DateTime.UtcNow;
        IsActive = true;
    }

    public void ClaimUntil(DateTime lockedUntilUtc) => LockedUntilUtc = lockedUntilUtc;

    public void ReleaseClaim() => LockedUntilUtc = null;

    public void MarkExecuted(Guid runId, DateTime executedAtUtc)
    {
        LastRunId = runId;
        LastRunAtUtc = executedAtUtc;
    }

    public void SoftDelete()
    {
        if (DeletedAtUtc is null) DeletedAtUtc = DateTime.UtcNow;
        IsActive = false;
        LockedUntilUtc = null;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class RunScheduleCase
{
    private RunScheduleCase() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RunScheduleId { get; private set; }
    public Guid TestCaseId { get; private set; }
    public string TestCaseCode { get; private set; } = default!;
    public string TestCaseTitle { get; private set; } = default!;
    public int TestCaseVersion { get; private set; }
    public string? AutomationScenarioPath { get; private set; }
    public AutomationEngine AutomationEngine { get; private set; } = AutomationEngine.None;
    public AutomationStatus AutomationStatus { get; private set; } = AutomationStatus.None;
    public int? AutomationDefinitionVersion { get; private set; }
    public string? AutomationRecordingJson { get; private set; }

    public static RunScheduleCase Snapshot(
        Guid runScheduleId,
        Guid testCaseId,
        string code,
        string title,
        int version,
        string? automationScenarioPath,
        AutomationEngine automationEngine,
        AutomationStatus automationStatus,
        int? automationDefinitionVersion,
        string? automationRecordingJson) => new()
    {
        RunScheduleId = runScheduleId,
        TestCaseId = testCaseId,
        TestCaseCode = code,
        TestCaseTitle = title,
        TestCaseVersion = version,
        AutomationScenarioPath = string.IsNullOrWhiteSpace(automationScenarioPath) ? null : automationScenarioPath.Trim(),
        AutomationEngine = automationEngine,
        AutomationStatus = automationStatus,
        AutomationDefinitionVersion = automationDefinitionVersion,
        AutomationRecordingJson = string.IsNullOrWhiteSpace(automationRecordingJson) ? null : automationRecordingJson.Trim(),
    };
}

public sealed class RunScheduleExecution
{
    private RunScheduleExecution() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RunScheduleId { get; private set; }
    public Guid? RunId { get; private set; }
    public RunScheduleExecutionStatus Status { get; private set; } = RunScheduleExecutionStatus.Running;
    public string Trigger { get; private set; } = "scheduled";
    public DateTime StartedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; private set; }
    public int TotalCases { get; private set; }
    public int Passed { get; private set; }
    public int Failed { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static RunScheduleExecution Start(Guid scheduleId, string trigger, int totalCases) => new()
    {
        RunScheduleId = scheduleId,
        Trigger = string.IsNullOrWhiteSpace(trigger) ? "scheduled" : trigger.Trim(),
        TotalCases = totalCases,
    };

    public void AttachRun(Guid runId) => RunId = runId;

    public void Complete(int passed, int failed)
    {
        Status = RunScheduleExecutionStatus.Completed;
        Passed = passed;
        Failed = failed;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Fail(string message)
    {
        Status = RunScheduleExecutionStatus.Failed;
        ErrorMessage = string.IsNullOrWhiteSpace(message) ? "Schedule execution failed." : message.Trim();
        CompletedAtUtc = DateTime.UtcNow;
    }
}

public enum ScheduleRecurrenceType
{
    Daily = 0,
    Weekly = 1,
    Hourly = 2,
    Every30Minutes = 3,
    Every15Minutes = 4,
    Every10Minutes = 5,
    Every5Minutes = 6,
}

public enum RunScheduleExecutionStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2,
}
