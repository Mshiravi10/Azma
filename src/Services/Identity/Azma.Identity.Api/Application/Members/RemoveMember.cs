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
/// Owner/Admin removes a member from the workspace. Hard-removes the membership row.
/// If the user has no remaining memberships in any workspace, deactivates the account.
/// Refresh tokens scoped to the workspace are revoked.
/// </summary>
public sealed record RemoveMemberCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserRole,
    Guid TargetUserId) : IRequest<Result>;

public sealed class RemoveMemberHandler : IRequestHandler<RemoveMemberCommand, Result>
{
    private readonly IdentityDbContext _db;
    private readonly IPublishEndpoint _publisher;

    public RemoveMemberHandler(IdentityDbContext db, IPublishEndpoint publisher)
    {
        _db = db; _publisher = publisher;
    }

    public async Task<Result> Handle(RemoveMemberCommand cmd, CancellationToken ct)
    {
        if (cmd.CurrentUserRole != AzmaRoles.Owner && cmd.CurrentUserRole != AzmaRoles.Admin)
            return Result.Failure(IdentityErrors.AdminOnly);

        if (cmd.TargetUserId == cmd.CurrentUserId)
            return Result.Failure(IdentityErrors.CannotRemoveSelf);

        var membership = await _db.Memberships
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.WorkspaceId == cmd.WorkspaceId && m.UserId == cmd.TargetUserId, ct);

        if (membership is null || membership.User is null)
            return Result.Failure(IdentityErrors.MemberNotFound);

        if (membership.Role == WorkspaceRole.Owner)
            return Result.Failure(IdentityErrors.CannotRemoveOwner);

        // Admin cannot remove another Admin (only Owner can)
        if (membership.Role == WorkspaceRole.Admin && cmd.CurrentUserRole != AzmaRoles.Owner)
            return Result.Failure(IdentityErrors.OwnerOnly);

        _db.Memberships.Remove(membership);

        // Revoke refresh tokens for this user in this workspace
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == cmd.TargetUserId && t.WorkspaceId == cmd.WorkspaceId && t.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var t in tokens) t.Revoke();

        // Check whether user still has any other memberships
        var hasOtherMembership = await _db.Memberships
            .AnyAsync(m => m.UserId == cmd.TargetUserId && m.WorkspaceId != cmd.WorkspaceId, ct);

        if (!hasOtherMembership && membership.User.IsActive)
        {
            membership.User.Deactivate();
            await _db.SaveChangesAsync(ct);
            await _publisher.Publish(new MemberRemoved(cmd.WorkspaceId, cmd.TargetUserId), ct);
            await _publisher.Publish(new UserDeactivated(cmd.TargetUserId), ct);
            await _publisher.Publish(
                new UserUpdated(membership.User.Id, membership.User.Email, membership.User.DisplayName,
                    membership.User.AvatarObjectKey, membership.User.IsActive), ct);
        }
        else
        {
            await _db.SaveChangesAsync(ct);
            await _publisher.Publish(new MemberRemoved(cmd.WorkspaceId, cmd.TargetUserId), ct);
        }

        return Result.Success();
    }
}
