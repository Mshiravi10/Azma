using Azma.EventBus.Contracts.Identity;
using Azma.Projects.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Azma.Projects.Api.Application.Consumers;

/// <summary>
/// When a member is removed from the workspace in Identity, drop all of their per-project overrides
/// in this service so stale rows don't accumulate.
/// </summary>
public sealed class MemberRemovedConsumer : IConsumer<MemberRemoved>
{
    private readonly ProjectsDbContext _db;
    public MemberRemovedConsumer(ProjectsDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<MemberRemoved> ctx)
    {
        var msg = ctx.Message;
        var rows = await _db.ProjectMemberships
            .Where(m => m.WorkspaceId == msg.WorkspaceId && m.UserId == msg.UserId)
            .ToListAsync(ctx.CancellationToken);
        if (rows.Count == 0) return;
        _db.ProjectMemberships.RemoveRange(rows);
        await _db.SaveChangesAsync(ctx.CancellationToken);
    }
}
