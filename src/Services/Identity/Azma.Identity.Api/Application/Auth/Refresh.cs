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

public sealed record RefreshCommand(string RefreshToken) : IRequest<Result<AuthTokensDto>>;

public sealed class RefreshValidator : AbstractValidator<RefreshCommand>
{
    public RefreshValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class RefreshHandler : IRequestHandler<RefreshCommand, Result<AuthTokensDto>>
{
    private readonly IdentityDbContext _db;
    private readonly ITokenHasher _tokenHasher;
    private readonly IJwtTokenGenerator _jwt;
    private readonly JwtOptions _jwtOptions;

    public RefreshHandler(
        IdentityDbContext db, ITokenHasher tokenHasher,
        IJwtTokenGenerator jwt, JwtOptions jwtOptions)
    {
        _db = db; _tokenHasher = tokenHasher; _jwt = jwt; _jwtOptions = jwtOptions;
    }

    public async Task<Result<AuthTokensDto>> Handle(RefreshCommand cmd, CancellationToken ct)
    {
        var hash = _tokenHasher.Hash(cmd.RefreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is null || !existing.IsActive)
            return Result.Failure<AuthTokensDto>(IdentityErrors.InvalidRefreshToken);

        var membership = await _db.Memberships
            .Include(m => m.User)
            .Include(m => m.Workspace)
            .FirstOrDefaultAsync(m => m.UserId == existing.UserId && m.WorkspaceId == existing.WorkspaceId, ct);

        if (membership is null || membership.User is null || membership.Workspace is null || !membership.User.IsActive)
            return Result.Failure<AuthTokensDto>(IdentityErrors.InvalidRefreshToken);

        var (rawRefresh, newHash) = _tokenHasher.GenerateAndHash();
        existing.Revoke(newHash);
        var refresh = RefreshToken.Issue(
            existing.UserId, existing.WorkspaceId, newHash,
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
            rawRefresh, refresh.ExpiresAtUtc,
            new UserDto(membership.User.Id, membership.User.Email, membership.User.DisplayName, UserDtoFactory.AvatarUrl(membership.User.Id, membership.User.AvatarObjectKey, membership.User.UpdatedAtUtc), membership.User.MustChangePassword),
            new WorkspaceDto(membership.Workspace.Id, membership.Workspace.Name, membership.Workspace.Slug, roleName, scopeName));
    }
}
