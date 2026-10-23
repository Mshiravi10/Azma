namespace Azma.EventBus.Contracts.Projects;

public sealed record ProjectCreated(
    Guid WorkspaceId,
    Guid ProjectId,
    string Name,
    string Code) : IntegrationEvent;

public sealed record ProjectArchived(
    Guid WorkspaceId,
    Guid ProjectId) : IntegrationEvent;

/// <summary>
/// Published when a project is restored from archive.
/// Downstream services use this to upsert their local project_ref read-model
/// (create if missing, un-archive if present) — making restore idempotent
/// even when the original ProjectCreated event was lost.
/// </summary>
public sealed record ProjectRestored(
    Guid WorkspaceId,
    Guid ProjectId,
    string Code) : IntegrationEvent;
