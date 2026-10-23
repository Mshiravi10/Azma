using Azma.EventBus.Contracts.Projects;
using Azma.EventBus.Contracts.TestCases;
using Azma.Automation;
using Azma.Runs.Api.Application.Runs;
using Azma.Runs.Api.Domain;
using Azma.Runs.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Azma.Runs.Api.Application.Consumers;

public sealed class ProjectCreatedConsumer : IConsumer<ProjectCreated>
{
    private readonly RunsDbContext _db;
    private readonly ILogger<ProjectCreatedConsumer> _logger;
    public ProjectCreatedConsumer(RunsDbContext db, ILogger<ProjectCreatedConsumer> logger)
    { _db = db; _logger = logger; }

    public async Task Consume(ConsumeContext<ProjectCreated> ctx)
    {
        var msg = ctx.Message;
        var existing = await _db.ProjectRefs.FindAsync(new object[] { msg.ProjectId }, ctx.CancellationToken);
        if (existing is not null) return;

        _db.ProjectRefs.Add(ProjectRef.Create(msg.ProjectId, msg.WorkspaceId, msg.Code));
        await _db.SaveChangesAsync(ctx.CancellationToken);
        _logger.LogInformation("Registered project_ref {ProjectId} ({Code}) for workspace {WorkspaceId}.",
            msg.ProjectId, msg.Code, msg.WorkspaceId);
    }
}

public sealed class ProjectArchivedConsumer : IConsumer<ProjectArchived>
{
    private readonly RunsDbContext _db;
    private readonly ILogger<ProjectArchivedConsumer> _logger;
    public ProjectArchivedConsumer(RunsDbContext db, ILogger<ProjectArchivedConsumer> logger)
    { _db = db; _logger = logger; }

    public async Task Consume(ConsumeContext<ProjectArchived> ctx)
    {
        var msg = ctx.Message;
        var pr = await _db.ProjectRefs.FirstOrDefaultAsync(
            x => x.Id == msg.ProjectId && x.WorkspaceId == msg.WorkspaceId, ctx.CancellationToken);
        if (pr is null || pr.IsArchived) return;
        pr.Archive();
        await _db.SaveChangesAsync(ctx.CancellationToken);
        _logger.LogInformation("Archived project_ref {ProjectId} for workspace {WorkspaceId}.",
            msg.ProjectId, msg.WorkspaceId);
    }
}

public sealed class ProjectRestoredConsumer : IConsumer<ProjectRestored>
{
    private readonly RunsDbContext _db;
    private readonly ILogger<ProjectRestoredConsumer> _logger;
    public ProjectRestoredConsumer(RunsDbContext db, ILogger<ProjectRestoredConsumer> logger)
    { _db = db; _logger = logger; }

    public async Task Consume(ConsumeContext<ProjectRestored> ctx)
    {
        var msg = ctx.Message;
        var pr = await _db.ProjectRefs.FirstOrDefaultAsync(
            x => x.Id == msg.ProjectId && x.WorkspaceId == msg.WorkspaceId, ctx.CancellationToken);

        if (pr is null)
        {
            // ProjectCreated event was never received — create the ref now.
            _db.ProjectRefs.Add(ProjectRef.Create(msg.ProjectId, msg.WorkspaceId, msg.Code));
            _logger.LogInformation("Created missing project_ref {ProjectId} ({Code}) via ProjectRestored.",
                msg.ProjectId, msg.Code);
        }
        else
        {
            pr.Restore();
            _logger.LogInformation("Restored project_ref {ProjectId} for workspace {WorkspaceId}.",
                msg.ProjectId, msg.WorkspaceId);
        }

        await _db.SaveChangesAsync(ctx.CancellationToken);
    }
}

public sealed class TestCaseCreatedConsumer : IConsumer<TestCaseCreated>
{
    private readonly RunsDbContext _db;
    private readonly ILogger<TestCaseCreatedConsumer> _logger;
    public TestCaseCreatedConsumer(RunsDbContext db, ILogger<TestCaseCreatedConsumer> logger)
    { _db = db; _logger = logger; }

    public async Task Consume(ConsumeContext<TestCaseCreated> ctx)
    {
        var msg = ctx.Message;
        var existing = await _db.TestCaseRefs.FindAsync(new object[] { msg.TestCaseId }, ctx.CancellationToken);
        var automationEngine = ConsumerMapping.ParseEnum(msg.AutomationEngine, AutomationEngine.None);
        var automationStatus = ConsumerMapping.ParseEnum(msg.AutomationStatus, AutomationStatus.None);
        if (existing is not null)
        {
            existing.Apply(
                msg.Code, msg.Title, existing.Version, msg.AutomationScenarioPath,
                automationEngine, automationStatus,
                msg.AutomationDefinitionVersion, msg.AutomationRecordingJson);
        }
        else
        {
            _db.TestCaseRefs.Add(TestCaseRef.Create(
                msg.TestCaseId, msg.WorkspaceId, msg.ProjectId, msg.Code, msg.Title, msg.AutomationScenarioPath,
                automationEngine, automationStatus,
                msg.AutomationDefinitionVersion, msg.AutomationRecordingJson));
        }
        await _db.SaveChangesAsync(ctx.CancellationToken);
        _logger.LogInformation("TestCaseRef {TestCaseId} ({Code}) registered.", msg.TestCaseId, msg.Code);
    }
}

public sealed class TestCaseUpdatedConsumer : IConsumer<TestCaseUpdated>
{
    private readonly RunsDbContext _db;
    private readonly ILogger<TestCaseUpdatedConsumer> _logger;
    public TestCaseUpdatedConsumer(RunsDbContext db, ILogger<TestCaseUpdatedConsumer> logger)
    { _db = db; _logger = logger; }

    public async Task Consume(ConsumeContext<TestCaseUpdated> ctx)
    {
        var msg = ctx.Message;
        var automationEngine = ConsumerMapping.ParseEnum(msg.AutomationEngine, AutomationEngine.None);
        var automationStatus = ConsumerMapping.ParseEnum(msg.AutomationStatus, AutomationStatus.None);
        var tcr = await _db.TestCaseRefs.FirstOrDefaultAsync(x => x.Id == msg.TestCaseId, ctx.CancellationToken);
        if (tcr is null)
        {
            // Out-of-order: insert the projection so future runs work.
            _db.TestCaseRefs.Add(TestCaseRef.Create(
                msg.TestCaseId, msg.WorkspaceId, msg.ProjectId, msg.Code, msg.Title, msg.AutomationScenarioPath,
                automationEngine, automationStatus,
                msg.AutomationDefinitionVersion, msg.AutomationRecordingJson));
        }
        else
        {
            tcr.Apply(
                msg.Code, msg.Title, msg.Version, msg.AutomationScenarioPath,
                automationEngine, automationStatus,
                msg.AutomationDefinitionVersion, msg.AutomationRecordingJson);
        }

        // Publications update future executions. Existing RunCase snapshots describe
        // the definition selected for that run and must not be rewritten by events.
        await RunScheduleAutomationSynchronizer.ApplyPublicationAsync(_db, msg, ctx.CancellationToken);

        await _db.SaveChangesAsync(ctx.CancellationToken);
        _logger.LogDebug("TestCaseRef {TestCaseId} updated to v{Version}.", msg.TestCaseId, msg.Version);
    }
}

internal static class ConsumerMapping
{
    public static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct =>
        Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;
}

public sealed class TestCaseDeletedConsumer : IConsumer<TestCaseDeleted>
{
    private readonly RunsDbContext _db;
    public TestCaseDeletedConsumer(RunsDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<TestCaseDeleted> ctx)
    {
        var tcr = await _db.TestCaseRefs.FirstOrDefaultAsync(x => x.Id == ctx.Message.TestCaseId, ctx.CancellationToken);
        if (tcr is null || tcr.IsDeleted) return;
        tcr.MarkDeleted();
        await _db.SaveChangesAsync(ctx.CancellationToken);
    }
}
