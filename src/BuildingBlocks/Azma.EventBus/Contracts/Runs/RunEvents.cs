namespace Azma.EventBus.Contracts.Runs;

public sealed record TestRunCreated(
    Guid WorkspaceId,
    Guid ProjectId,
    Guid RunId,
    string Name,
    int CaseCount) : IntegrationEvent;

public sealed record TestRunCompleted(
    Guid WorkspaceId,
    Guid ProjectId,
    Guid RunId,
    int Passed,
    int Failed,
    int Blocked,
    int Skipped) : IntegrationEvent;

public sealed record TestResultRecorded(
    Guid WorkspaceId,
    Guid ProjectId,
    Guid RunId,
    Guid RunCaseId,
    Guid RunResultId,
    Guid TestCaseId,
    string Status,
    Guid ExecutedByUserId,
    DateTime ExecutedAtUtc) : IntegrationEvent;
