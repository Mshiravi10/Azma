using Azma.EventBus.Contracts.Identity;
using Azma.Identity.Api.Domain;
using Azma.Identity.Api.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Azma.Identity.Api.Application.Members;

public sealed class EnsureWorkspaceMemberConsumer : IConsumer<EnsureWorkspaceMemberRequested>
{
    private readonly IdentityDbContext _db;
    private readonly IPublishEndpoint _publisher;
    private readonly ILogger<EnsureWorkspaceMemberConsumer> _logger;

    public EnsureWorkspaceMemberConsumer(
        IdentityDbContext db,
        IPublishEndpoint publisher,
        ILogger<EnsureWorkspaceMemberConsumer> logger)
    {
        _db = db;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EnsureWorkspaceMemberRequested> ctx)
    {
        var msg = ctx.Message;

        var existing = await _db.Memberships.AsNoTracking()
            .AnyAsync(m => m.WorkspaceId == msg.WorkspaceId && m.UserId == msg.UserId, ctx.CancellationToken);
        if (existing)
            return;

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == msg.UserId && u.IsActive, ctx.CancellationToken);
        if (user is null)
        {
            _logger.LogWarning("Cannot ensure project-scoped membership for missing/inactive user {UserId}.", msg.UserId);
            return;
        }

        var workspaceExists = await _db.Workspaces.AsNoTracking()
            .AnyAsync(w => w.Id == msg.WorkspaceId, ctx.CancellationToken);
        if (!workspaceExists)
        {
            _logger.LogWarning("Cannot ensure project-scoped membership in missing workspace {WorkspaceId}.", msg.WorkspaceId);
            return;
        }

        var role = Enum.TryParse<WorkspaceRole>(msg.Role, ignoreCase: true, out var parsedRole)
            ? parsedRole
            : WorkspaceRole.Viewer;
        if (role == WorkspaceRole.Owner || role == WorkspaceRole.Admin)
            role = WorkspaceRole.Viewer;

        var scope = Enum.TryParse<MembershipAccessScope>(msg.AccessScope, ignoreCase: true, out var parsedScope)
            ? parsedScope
            : MembershipAccessScope.ProjectScoped;
        if (scope == MembershipAccessScope.Workspace)
            scope = MembershipAccessScope.ProjectScoped;

        var membership = Membership.Create(msg.UserId, msg.WorkspaceId, role, scope);
        _db.Memberships.Add(membership);
        await _db.SaveChangesAsync(ctx.CancellationToken);

        await _publisher.Publish(new MemberAdded(msg.WorkspaceId, msg.UserId, user.Email, role.ToString()), ctx.CancellationToken);
        await _publisher.Publish(
            new UserUpdated(user.Id, user.Email, user.DisplayName, user.AvatarObjectKey, user.IsActive),
            ctx.CancellationToken);
    }
}
