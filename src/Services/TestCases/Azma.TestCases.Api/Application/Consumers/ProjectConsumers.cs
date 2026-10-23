using Azma.EventBus.Contracts.Projects;
using Azma.TestCases.Api.Domain;
using Azma.TestCases.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Azma.TestCases.Api.Application.Consumers;

public sealed class ProjectCreatedConsumer : IConsumer<ProjectCreated>
{
    private readonly TestCasesDbContext _db;
    private readonly ILogger<ProjectCreatedConsumer> _logger;
    public ProjectCreatedConsumer(TestCasesDbContext db, ILogger<ProjectCreatedConsumer> logger)
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
    private readonly TestCasesDbContext _db;
    private readonly ILogger<ProjectArchivedConsumer> _logger;
    public ProjectArchivedConsumer(TestCasesDbContext db, ILogger<ProjectArchivedConsumer> logger)
    { _db = db; _logger = logger; }

    public async Task Consume(ConsumeContext<ProjectArchived> ctx)
    {
        var msg = ctx.Message;
        var pr = await _db.ProjectRefs.FirstOrDefaultAsync(
            x => x.Id == msg.ProjectId && x.WorkspaceId == msg.WorkspaceId, ctx.CancellationToken);
        if (pr is null) return;
        if (pr.IsArchived) return;
        pr.Archive();
        await _db.SaveChangesAsync(ctx.CancellationToken);
        _logger.LogInformation("Archived project_ref {ProjectId} for workspace {WorkspaceId}.",
            msg.ProjectId, msg.WorkspaceId);
    }
}

public sealed class ProjectRestoredConsumer : IConsumer<ProjectRestored>
{
    private readonly TestCasesDbContext _db;
    private readonly ILogger<ProjectRestoredConsumer> _logger;
    public ProjectRestoredConsumer(TestCasesDbContext db, ILogger<ProjectRestoredConsumer> logger)
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
