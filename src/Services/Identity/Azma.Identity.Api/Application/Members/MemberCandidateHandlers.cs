using Azma.Auth;
using Azma.Common.Pagination;
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

public sealed record MemberCandidateDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    bool IsInWorkspace,
    string? WorkspaceRole);

public sealed record ListMemberCandidatesQuery(
    Guid WorkspaceId,
    string CurrentUserRole,
    string? Search,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<MemberCandidateDto>>>;

public sealed class ListMemberCandidatesHandler
    : IRequestHandler<ListMemberCandidatesQuery, Result<PagedResult<MemberCandidateDto>>>
{
    private readonly IdentityDbContext _db;
    public ListMemberCandidatesHandler(IdentityDbContext db) => _db = db;

    public async Task<Result<PagedResult<MemberCandidateDto>>> Handle(ListMemberCandidatesQuery q, CancellationToken ct)
    {
        if (q.CurrentUserRole != AzmaRoles.Owner && q.CurrentUserRole != AzmaRoles.Admin)
            return Result.Failure<PagedResult<MemberCandidateDto>>(IdentityErrors.AdminOnly);

        var page = new PageRequest(q.Page, q.PageSize);
        var users = _db.Users.AsNoTracking().Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var search = q.Search.Trim();
            var like = $"%{search}%";
            users = users.Where(u =>
                EF.Functions.ILike(u.DisplayName, like) ||
                EF.Functions.ILike(u.Email, like));
        }

        var total = await users.LongCountAsync(ct);

        var memberships = await _db.Memberships.AsNoTracking()
            .Where(m => m.WorkspaceId == q.WorkspaceId)
            .ToDictionaryAsync(m => m.UserId, ct);

        var items = await users
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.Email)
            .Skip(page.Skip)
            .Take(page.Take)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.AvatarObjectKey,
                u.UpdatedAtUtc,
            })
            .ToListAsync(ct);

        var dtos = items.Select(u =>
        {
            memberships.TryGetValue(u.Id, out var membership);
            return new MemberCandidateDto(
                u.Id,
                u.Email,
                u.DisplayName,
                UserDtoFactory.AvatarUrl(u.Id, u.AvatarObjectKey, u.UpdatedAtUtc),
                IsInWorkspace: membership is not null,
                WorkspaceRole: membership?.Role.ToString());
        }).ToList();

        return new PagedResult<MemberCandidateDto>(dtos, page.Page, page.Take, total);
    }
}

public sealed record AddExistingMemberCommand(
    Guid WorkspaceId,
    Guid CurrentUserId,
    string CurrentUserRole,
    Guid UserId,
    WorkspaceRole Role) : IRequest<Result<MembershipDto>>;

public sealed class AddExistingMemberValidator : AbstractValidator<AddExistingMemberCommand>
{
    public AddExistingMemberValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Role).NotEqual(WorkspaceRole.Owner)
            .WithMessage("The Owner role must be transferred, not assigned directly.");
    }
}

public sealed class AddExistingMemberHandler
    : IRequestHandler<AddExistingMemberCommand, Result<MembershipDto>>
{
    private readonly IdentityDbContext _db;
    private readonly IPublishEndpoint _publisher;

    public AddExistingMemberHandler(IdentityDbContext db, IPublishEndpoint publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task<Result<MembershipDto>> Handle(AddExistingMemberCommand cmd, CancellationToken ct)
    {
        if (cmd.CurrentUserRole != AzmaRoles.Owner && cmd.CurrentUserRole != AzmaRoles.Admin)
            return Result.Failure<MembershipDto>(IdentityErrors.AdminOnly);

        if (cmd.Role == WorkspaceRole.Owner)
            return Result.Failure<MembershipDto>(IdentityErrors.CannotChangeOwnerRole);

        var user = await _db.Users
            .Include(u => u.Memberships)
            .FirstOrDefaultAsync(u => u.Id == cmd.UserId, ct);

        if (user is null)
            return Result.Failure<MembershipDto>(IdentityErrors.UserNotFound);

        if (!user.IsActive)
            return Result.Failure<MembershipDto>(IdentityErrors.UserDeactivated);

        if (user.Memberships.Any(m => m.WorkspaceId == cmd.WorkspaceId))
            return Result.Failure<MembershipDto>(IdentityErrors.AlreadyMember);

        var membership = Membership.Create(user.Id, cmd.WorkspaceId, cmd.Role);
        _db.Memberships.Add(membership);
        await _db.SaveChangesAsync(ct);

        await _publisher.Publish(new UserUpdated(user.Id, user.Email, user.DisplayName, user.AvatarObjectKey, user.IsActive), ct);
        await _publisher.Publish(new MemberAdded(cmd.WorkspaceId, user.Id, user.Email, cmd.Role.ToString()), ct);

        return new MembershipDto(
            user.Id,
            user.Email,
            user.DisplayName,
            UserDtoFactory.AvatarUrl(user.Id, user.AvatarObjectKey, user.UpdatedAtUtc),
            cmd.Role.ToString(),
            membership.AccessScope.ToString(),
            membership.JoinedAtUtc,
            user.LastLoginAtUtc,
            user.IsActive);
    }
}
