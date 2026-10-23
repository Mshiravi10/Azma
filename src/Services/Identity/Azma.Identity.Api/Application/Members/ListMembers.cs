using Azma.Common.Results;
using Azma.Identity.Api.Application.Common;
using Azma.Identity.Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Identity.Api.Application.Members;

public sealed record ListMembersQuery(Guid WorkspaceId) : IRequest<Result<IReadOnlyList<MembershipDto>>>;

public sealed class ListMembersHandler : IRequestHandler<ListMembersQuery, Result<IReadOnlyList<MembershipDto>>>
{
    private readonly IdentityDbContext _db;
    public ListMembersHandler(IdentityDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<MembershipDto>>> Handle(ListMembersQuery q, CancellationToken ct)
    {
        var items = await _db.Memberships
            .Include(m => m.User)
            .Where(m => m.WorkspaceId == q.WorkspaceId && m.User != null)
            .OrderBy(m => m.JoinedAtUtc)
            .Select(m => new MembershipDto(
                m.UserId,
                m.User!.Email,
                m.User.DisplayName,
                UserDtoFactory.AvatarUrl(m.UserId, m.User.AvatarObjectKey, m.User.UpdatedAtUtc),
                m.Role.ToString(),
                m.AccessScope.ToString(),
                m.JoinedAtUtc,
                m.User.LastLoginAtUtc,
                m.User.IsActive))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<MembershipDto>>(items);
    }
}

public sealed record ListInvitationsQuery(Guid WorkspaceId) : IRequest<Result<IReadOnlyList<InvitationDto>>>;

public sealed class ListInvitationsHandler : IRequestHandler<ListInvitationsQuery, Result<IReadOnlyList<InvitationDto>>>
{
    private readonly IdentityDbContext _db;
    public ListInvitationsHandler(IdentityDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<InvitationDto>>> Handle(ListInvitationsQuery q, CancellationToken ct)
    {
        var items = await _db.Invitations
            .Where(i => i.WorkspaceId == q.WorkspaceId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new InvitationDto(
                i.Id, i.Email, i.Role.ToString(), i.Status.ToString(),
                i.CreatedAtUtc, i.ExpiresAtUtc, null))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<InvitationDto>>(items);
    }
}
