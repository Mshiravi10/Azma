using Azma.Auth.Jwt;
using Azma.Common.Results;
using Azma.Identity.Api.Application.Common;
using Azma.Identity.Api.Domain;
using Azma.Identity.Api.Infrastructure.Persistence;
using Azma.Identity.Api.Infrastructure.Security;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Azma.Auth;

namespace Azma.Identity.Api.Application.Auth;

public sealed record SwitchWorkspaceCommand(Guid UserId, Guid WorkspaceId) : IRequest<Result<AuthTokensDto>>;

public sealed class SwitchWorkspaceValidator : AbstractValidator<SwitchWorkspaceCommand>
{
    public SwitchWorkspaceValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.WorkspaceId).NotEmpty();
    }
}

public sealed class SwitchWorkspaceHandler : IRequestHandler<SwitchWorkspaceCommand, Result<AuthTokensDto>>
{
    private readonly IdentityDbContext _db;
    private readonly ITokenHasher _tokenHasher;
    private readonly IJwtTokenGenerator _jwt;
    private readonly JwtOptions _jwtOptions;

    public SwitchWorkspaceHandler(
        IdentityDbContext db, ITokenHasher tokenHasher,
        IJwtTokenGenerator jwt, JwtOptions jwtOptions)
    {
        _db = db; _tokenHasher = tokenHasher; _jwt = jwt; _jwtOptions = jwtOptions;
    }

    public async Task<Result<AuthTokensDto>> Handle(SwitchWorkspaceCommand cmd, CancellationToken ct)
    {
        var membership = await _db.Memberships
            .Include(m => m.User)
            .Include(m => m.Workspace)
            .FirstOrDefaultAsync(m => m.UserId == cmd.UserId && m.WorkspaceId == cmd.WorkspaceId, ct);

        if (membership is null || membership.User is null || membership.Workspace is null)
            return Result.Failure<AuthTokensDto>(IdentityErrors.NotMemberOfWorkspace);

        var (raw, hash) = _tokenHasher.GenerateAndHash();
        var refresh = RefreshToken.Issue(
            membership.UserId, membership.WorkspaceId, hash,
            TimeSpan.FromDays(_jwtOptions.RefreshTokenDays));
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync(ct);

        var roleName = membership.Role.ToString();
        var scopeName = membership.AccessScope.ToString();
        var access = _jwt.Generate(
            membership.UserId, membership.WorkspaceId,
            membership.User.Email, membership.User.DisplayName, roleName, scopeName);

        return new AuthTokensDto(
            access.Token, access.ExpiresAtUtc,
            raw, refresh.ExpiresAtUtc,
            new UserDto(membership.User.Id, membership.User.Email, membership.User.DisplayName, UserDtoFactory.AvatarUrl(membership.User.Id, membership.User.AvatarObjectKey, membership.User.UpdatedAtUtc), membership.User.MustChangePassword),
            new WorkspaceDto(membership.Workspace.Id, membership.Workspace.Name, membership.Workspace.Slug, roleName, scopeName));
    }
}
