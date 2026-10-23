namespace Azma.EventBus.Contracts.TestCases;

public sealed record TestCaseCreated(
    Guid WorkspaceId,
    Guid ProjectId,
    Guid TestCaseId,
    string Code,
    string Title,
    string? AutomationScenarioPath = null,
    string? AutomationEngine = null,
    string? AutomationStatus = null,
    int? AutomationDefinitionVersion = null,
    string? AutomationRecordingJson = null) : IntegrationEvent;

public sealed record TestCaseUpdated(
    Guid WorkspaceId,
    Guid ProjectId,
    Guid TestCaseId,
    string Code,
    string Title,
    int Version,
    string? AutomationScenarioPath = null,
    string? AutomationEngine = null,
    string? AutomationStatus = null,
    int? AutomationDefinitionVersion = null,
    string? AutomationRecordingJson = null) : IntegrationEvent;

public sealed record TestCaseDeleted(
    Guid WorkspaceId,
    Guid ProjectId,
    Guid TestCaseId) : IntegrationEvent;
