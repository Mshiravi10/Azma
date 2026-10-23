using Azma.Auth;
using Azma.Common.Results;
using Azma.EventBus.Contracts.Identity;
using Azma.Identity.Api.Domain;
using Azma.Identity.Api.Infrastructure.Persistence;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Identity.Api.Application.Members;

/// <summary>
/// Owner transfers workspace ownership to another existing member.
/// Current Owner is demoted to Admin. Requires that the caller is the current Owner.
/// </summary>
public sealed record TransferOwnershipCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserRole,
    Guid NewOwnerUserId) : IRequest<Result>;

public sealed class TransferOwnershipHandler : IRequestHandler<TransferOwnershipCommand, Result>
{
    private readonly IdentityDbContext _db;
    private readonly IPublishEndpoint _publisher;

    public TransferOwnershipHandler(IdentityDbContext db, IPublishEndpoint publisher)
    {
        _db = db; _publisher = publisher;
    }

    public async Task<Result> Handle(TransferOwnershipCommand cmd, CancellationToken ct)
    {
        if (cmd.CurrentUserRole != AzmaRoles.Owner)
            return Result.Failure(IdentityErrors.OwnerOnly);

        if (cmd.NewOwnerUserId == cmd.CurrentUserId)
            return Result.Success(); // no-op

        var workspace = await _db.Workspaces.FirstOrDefaultAsync(w => w.Id == cmd.WorkspaceId, ct);
        if (workspace is null)
            return Result.Failure(IdentityErrors.NoWorkspaceMembership);

        var currentOwnerMembership = await _db.Memberships
            .FirstOrDefaultAsync(m => m.WorkspaceId == cmd.WorkspaceId && m.UserId == cmd.CurrentUserId, ct);
        var newOwnerMembership = await _db.Memberships
            .FirstOrDefaultAsync(m => m.WorkspaceId == cmd.WorkspaceId && m.UserId == cmd.NewOwnerUserId, ct);

        if (currentOwnerMembership is null) return Result.Failure(IdentityErrors.NotMemberOfWorkspace);
        if (newOwnerMembership is null) return Result.Failure(IdentityErrors.MemberNotFound);

        currentOwnerMembership.ChangeRole(WorkspaceRole.Admin);
        newOwnerMembership.ChangeRole(WorkspaceRole.Owner);

        // Update workspace owner reference (simple field set via reflection-free approach: re-create
        // through ef tracking — Workspace exposes no SetOwner; use raw update)
        _db.Entry(workspace).Property("OwnerUserId").CurrentValue = cmd.NewOwnerUserId;

        await _db.SaveChangesAsync(ct);

        await _publisher.Publish(
            new MemberRoleChanged(cmd.WorkspaceId, cmd.CurrentUserId, WorkspaceRole.Admin.ToString()), ct);
        await _publisher.Publish(
            new MemberRoleChanged(cmd.WorkspaceId, cmd.NewOwnerUserId, WorkspaceRole.Owner.ToString()), ct);

        return Result.Success();
    }
}
