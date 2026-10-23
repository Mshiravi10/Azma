using Azma.EventBus.Contracts.Identity;
using Azma.EventBus.Contracts.Projects;
using Azma.Projects.Api.Domain;
using Azma.Projects.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Azma.Projects.Api.Application.Consumers;

/// <summary>
/// Seeds a default "Demo" project for every newly created workspace
/// so the user has somewhere to land after signup.
/// </summary>
public sealed class WorkspaceCreatedConsumer : IConsumer<WorkspaceCreated>
{
    private readonly ProjectsDbContext _db;
    private readonly IPublishEndpoint _publisher;
    private readonly ILogger<WorkspaceCreatedConsumer> _logger;

    public WorkspaceCreatedConsumer(
        ProjectsDbContext db,
        IPublishEndpoint publisher,
        ILogger<WorkspaceCreatedConsumer> logger)
    {
        _db = db; _publisher = publisher; _logger = logger;
    }

    public async Task Consume(ConsumeContext<WorkspaceCreated> ctx)
    {
        var msg = ctx.Message;

        // Idempotency: if a project already exists for this workspace, do nothing.
        if (await _db.Projects.AnyAsync(p => p.WorkspaceId == msg.WorkspaceId, ctx.CancellationToken))
        {
            _logger.LogInformation("Workspace {WorkspaceId} already has projects; skip seeding.", msg.WorkspaceId);
            return;
        }

        var project = Project.Create(
            workspaceId: msg.WorkspaceId,
            name: "پروژه‌ی نمونه",
            code: "DEMO",
            description: "این یک پروژه‌ی نمونه برای شروع است. می‌توانید آن را آرشیو یا ویرایش کنید.",
            createdByUserId: msg.OwnerUserId);

        var suite = Suite.Create(msg.WorkspaceId, project.Id, "Master", null, 0);

        _db.Projects.Add(project);
        _db.Suites.Add(suite);
        await _db.SaveChangesAsync(ctx.CancellationToken);

        await _publisher.Publish(
            new ProjectCreated(project.WorkspaceId, project.Id, project.Name, project.Code),
            ctx.CancellationToken);

        _logger.LogInformation(
            "Seeded default project {ProjectId} ({Code}) for workspace {WorkspaceId}.",
            project.Id, project.Code, msg.WorkspaceId);
    }
}
