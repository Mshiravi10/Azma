using Azma.Auth;
using Azma.Auth.Jwt;
using Azma.Common.Results;
using Azma.Identity.Api.Application.Common;
using Azma.Identity.Api.Domain;
using Azma.Identity.Api.Infrastructure.Persistence;
using Azma.Identity.Api.Infrastructure.Security;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Identity.Api.Application.Auth;

public sealed record LoginCommand(string Email, string Password, Guid? WorkspaceId)
    : IRequest<Result<AuthTokensDto>>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginHandler : IRequestHandler<LoginCommand, Result<AuthTokensDto>>
{
    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenHasher _tokenHasher;
    private readonly IJwtTokenGenerator _jwt;
    private readonly JwtOptions _jwtOptions;

    public LoginHandler(
        IdentityDbContext db,
        IPasswordHasher hasher,
        ITokenHasher tokenHasher,
        IJwtTokenGenerator jwt,
        JwtOptions jwtOptions)
    {
        _db = db; _hasher = hasher; _tokenHasher = tokenHasher;
        _jwt = jwt; _jwtOptions = jwtOptions;
    }

    public async Task<Result<AuthTokensDto>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var email = cmd.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !_hasher.Verify(cmd.Password, user.PasswordHash))
            return Result.Failure<AuthTokensDto>(IdentityErrors.InvalidCredentials);

        if (!user.IsActive)
            return Result.Failure<AuthTokensDto>(IdentityErrors.UserDeactivated);

        var membership = cmd.WorkspaceId is null
            ? await _db.Memberships
                .Include(m => m.Workspace)
                .Where(m => m.UserId == user.Id)
                .OrderBy(m => m.JoinedAtUtc)
                .FirstOrDefaultAsync(ct)
            : await _db.Memberships
                .Include(m => m.Workspace)
                .FirstOrDefaultAsync(m => m.UserId == user.Id && m.WorkspaceId == cmd.WorkspaceId, ct);

        if (membership is null || membership.Workspace is null)
            return Result.Failure<AuthTokensDto>(IdentityErrors.NoWorkspaceMembership);

        user.UpdateLastLogin();

        var (rawRefresh, refreshHash) = _tokenHasher.GenerateAndHash();
        var refresh = RefreshToken.Issue(
            user.Id, membership.WorkspaceId, refreshHash,
            TimeSpan.FromDays(_jwtOptions.RefreshTokenDays));
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync(ct);

        var roleName = membership.Role.ToString();
        var scopeName = membership.AccessScope.ToString();
        var access = _jwt.Generate(user.Id, membership.WorkspaceId, user.Email, user.DisplayName, roleName, scopeName);

        return new AuthTokensDto(
            access.Token, access.ExpiresAtUtc,
            rawRefresh, refresh.ExpiresAtUtc,
            new UserDto(user.Id, user.Email, user.DisplayName, UserDtoFactory.AvatarUrl(user.Id, user.AvatarObjectKey, user.UpdatedAtUtc), user.MustChangePassword),
            new WorkspaceDto(membership.Workspace.Id, membership.Workspace.Name, membership.Workspace.Slug, roleName, scopeName));
    }
}
