using Azma.Auth;
using Azma.Common.Results;
using Azma.EventBus.Contracts.Identity;
using Azma.Identity.Api.Application.Common;
using Azma.Identity.Api.Domain;
using Azma.Identity.Api.Infrastructure.Persistence;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Identity.Api.Application.Members;

/// <summary>Owner/Admin updates a member's role within the current workspace.</summary>
public sealed record ChangeMemberRoleCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserRole,
    Guid TargetUserId,
    WorkspaceRole NewRole) : IRequest<Result<MembershipDto>>;

public sealed class ChangeMemberRoleValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    public ChangeMemberRoleValidator()
    {
        RuleFor(x => x.NewRole).NotEqual(WorkspaceRole.Owner)
            .WithMessage("برای تغییر مالکیت از مسیر انتقال مالکیت استفاده کنید.");
    }
}

public sealed class ChangeMemberRoleHandler
    : IRequestHandler<ChangeMemberRoleCommand, Result<MembershipDto>>
{
    private readonly IdentityDbContext _db;
    private readonly IPublishEndpoint _publisher;

    public ChangeMemberRoleHandler(IdentityDbContext db, IPublishEndpoint publisher)
    {
        _db = db; _publisher = publisher;
    }

    public async Task<Result<MembershipDto>> Handle(ChangeMemberRoleCommand cmd, CancellationToken ct)
    {
        if (cmd.CurrentUserRole != AzmaRoles.Owner && cmd.CurrentUserRole != AzmaRoles.Admin)
            return Result.Failure<MembershipDto>(IdentityErrors.AdminOnly);

        var membership = await _db.Memberships
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.WorkspaceId == cmd.WorkspaceId && m.UserId == cmd.TargetUserId, ct);

        if (membership is null || membership.User is null)
            return Result.Failure<MembershipDto>(IdentityErrors.MemberNotFound);

        if (membership.Role == WorkspaceRole.Owner)
            return Result.Failure<MembershipDto>(IdentityErrors.CannotChangeOwnerRole);

        // Only Owner can promote to Admin
        if (cmd.NewRole == WorkspaceRole.Admin && cmd.CurrentUserRole != AzmaRoles.Owner)
            return Result.Failure<MembershipDto>(IdentityErrors.OwnerOnly);

        if (membership.Role == cmd.NewRole)
        {
            return new MembershipDto(
                membership.UserId, membership.User.Email, membership.User.DisplayName,
                UserDtoFactory.AvatarUrl(membership.UserId, membership.User.AvatarObjectKey, membership.User.UpdatedAtUtc),
                membership.Role.ToString(), membership.AccessScope.ToString(),
                membership.JoinedAtUtc, membership.User.LastLoginAtUtc, membership.User.IsActive);
        }

        membership.ChangeRole(cmd.NewRole);
        await _db.SaveChangesAsync(ct);

        await _publisher.Publish(
            new MemberRoleChanged(cmd.WorkspaceId, cmd.TargetUserId, cmd.NewRole.ToString()), ct);

        return new MembershipDto(
            membership.UserId, membership.User.Email, membership.User.DisplayName,
            UserDtoFactory.AvatarUrl(membership.UserId, membership.User.AvatarObjectKey, membership.User.UpdatedAtUtc),
            membership.Role.ToString(), membership.AccessScope.ToString(),
            membership.JoinedAtUtc, membership.User.LastLoginAtUtc, membership.User.IsActive);
    }
}
