namespace Azma.Reporting.Api.Domain;

/// <summary>Project projection — built from ProjectCreated/ProjectArchived.</summary>
public sealed class ProjectRef
{
    private ProjectRef() { }

    public Guid Id { get; private set; }            // = ProjectId
    public Guid WorkspaceId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsArchived { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public static ProjectRef Create(Guid id, Guid workspaceId, string code, string name) => new()
    {
        Id = id,
        WorkspaceId = workspaceId,
        Code = code.ToUpperInvariant(),
        Name = name,
        IsArchived = false,
    };

    public void Archive() => IsArchived = true;
}

/// <summary>TestCase projection — built from TestCase* events.</summary>
public sealed class TestCaseRef
{
    private TestCaseRef() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public bool IsDeleted { get; private set; }

    public static TestCaseRef Create(Guid id, Guid workspaceId, Guid projectId, string code, string title) => new()
    {
        Id = id,
        WorkspaceId = workspaceId,
        ProjectId = projectId,
        Code = code,
        Title = title,
    };

    public void Apply(string code, string title)
    {
        Code = code;
        Title = title;
    }

    public void MarkDeleted() => IsDeleted = true;
}

/// <summary>Run projection — built from TestRunCreated/TestRunCompleted/TestResultRecorded.</summary>
public sealed class RunRef
{
    private RunRef() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = default!;
    public int CaseCount { get; private set; }
    public ReportRunStatus Status { get; private set; } = ReportRunStatus.Open;

    // Aggregated counters; updated on every TestResultRecorded for this run.
    public int Passed { get; private set; }
    public int Failed { get; private set; }
    public int Blocked { get; private set; }
    public int Skipped { get; private set; }
    public int Retest { get; private set; }
    /// <summary>Total recorded results so far (>= Passed+Failed+Blocked+Skipped+Retest).</summary>
    public int Recorded { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; private set; }

    public static RunRef Create(Guid id, Guid workspaceId, Guid projectId, string name, int caseCount) => new()
    {
        Id = id,
        WorkspaceId = workspaceId,
        ProjectId = projectId,
        Name = name,
        CaseCount = caseCount,
        Status = ReportRunStatus.Open,
    };

    public void RecordResult(ReportResultStatus status)
    {
        Recorded++;
        switch (status)
        {
            case ReportResultStatus.Passed:  Passed++; break;
            case ReportResultStatus.Failed:  Failed++; break;
            case ReportResultStatus.Blocked: Blocked++; break;
            case ReportResultStatus.Skipped: Skipped++; break;
            case ReportResultStatus.Retest:  Retest++; break;
        }
    }

    public void Complete(int passed, int failed, int blocked, int skipped)
    {
        Status = ReportRunStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        // Use the authoritative final counts published by Runs service.
        Passed = passed;
        Failed = failed;
        Blocked = blocked;
        Skipped = skipped;
    }
}

/// <summary>One row per recorded TestResultRecorded event — feeds trends, top-failures, user-activity.</summary>
public sealed class ResultEvent
{
    private ResultEvent() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid RunId { get; private set; }
    public Guid RunCaseId { get; private set; }
    public Guid TestCaseId { get; private set; }
    public ReportResultStatus Status { get; private set; }
    public Guid ExecutedByUserId { get; private set; }
    public DateTime ExecutedAtUtc { get; private set; } = DateTime.UtcNow;

    public static ResultEvent Create(
        Guid workspaceId, Guid projectId, Guid runId, Guid runCaseId, Guid testCaseId,
        ReportResultStatus status, Guid executedByUserId, DateTime executedAtUtc) => new()
    {
        WorkspaceId = workspaceId,
        ProjectId = projectId,
        RunId = runId,
        RunCaseId = runCaseId,
        TestCaseId = testCaseId,
        Status = status,
        ExecutedByUserId = executedByUserId,
        ExecutedAtUtc = executedAtUtc,
    };
}
