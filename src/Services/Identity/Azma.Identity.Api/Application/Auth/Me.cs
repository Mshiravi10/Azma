using Azma.Common.Results;
using Azma.Identity.Api.Application.Common;
using Azma.Identity.Api.Domain;
using Azma.Identity.Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Identity.Api.Application.Auth;

public sealed record MeQuery(Guid UserId, Guid CurrentWorkspaceId) : IRequest<Result<MeResponse>>;

public sealed record MeResponse(UserDto User, WorkspaceDto Current, IReadOnlyList<WorkspaceDto> Workspaces);

public sealed class MeHandler : IRequestHandler<MeQuery, Result<MeResponse>>
{
    private readonly IdentityDbContext _db;
    public MeHandler(IdentityDbContext db) => _db = db;

    public async Task<Result<MeResponse>> Handle(MeQuery q, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == q.UserId, ct);
        if (user is null) return Result.Failure<MeResponse>(IdentityErrors.InvalidCredentials);

        var memberships = await _db.Memberships
            .Include(m => m.Workspace)
            .Where(m => m.UserId == q.UserId)
            .ToListAsync(ct);

        if (memberships.Count == 0)
            return Result.Failure<MeResponse>(IdentityErrors.NoWorkspaceMembership);

        var workspaces = memberships
            .Where(m => m.Workspace is not null)
            .Select(m => new WorkspaceDto(
                m.Workspace!.Id,
                m.Workspace.Name,
                m.Workspace.Slug,
                m.Role.ToString(),
                m.AccessScope.ToString()))
            .ToList();

        var current = workspaces.FirstOrDefault(w => w.Id == q.CurrentWorkspaceId) ?? workspaces[0];

        return new MeResponse(
            new UserDto(
                user.Id,
                user.Email,
                user.DisplayName,
                UserDtoFactory.AvatarUrl(user.Id, user.AvatarObjectKey, user.UpdatedAtUtc),
                user.MustChangePassword),
            current,
            workspaces);
    }
}
