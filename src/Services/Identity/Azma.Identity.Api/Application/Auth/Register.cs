using Azma.Auth;
using Azma.Auth.Jwt;
using Azma.Common.Results;
using Azma.EventBus.Contracts.Identity;
using Azma.Identity.Api.Application.Common;
using Azma.Identity.Api.Domain;
using Azma.Identity.Api.Infrastructure;
using Azma.Identity.Api.Infrastructure.Persistence;
using Azma.Identity.Api.Infrastructure.Security;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Identity.Api.Application.Auth;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string DisplayName,
    string WorkspaceName) : IRequest<Result<AuthTokensDto>>;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.DisplayName).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(x => x.WorkspaceName).NotEmpty().MinimumLength(2).MaximumLength(120);
    }
}

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, Result<AuthTokensDto>>
{
    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenHasher _tokenHasher;
    private readonly IJwtTokenGenerator _jwt;
    private readonly JwtOptions _jwtOptions;
    private readonly IPublishEndpoint _publisher;

    public RegisterHandler(
        IdentityDbContext db,
        IPasswordHasher hasher,
        ITokenHasher tokenHasher,
        IJwtTokenGenerator jwt,
        JwtOptions jwtOptions,
        IPublishEndpoint publisher)
    {
        _db = db; _hasher = hasher; _tokenHasher = tokenHasher;
        _jwt = jwt; _jwtOptions = jwtOptions; _publisher = publisher;
    }

    public async Task<Result<AuthTokensDto>> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        var email = cmd.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Result.Failure<AuthTokensDto>(IdentityErrors.EmailAlreadyTaken);

        var user = User.Create(email, _hasher.Hash(cmd.Password), cmd.DisplayName);

        var slug = await EnsureUniqueSlugAsync(SlugGenerator.From(cmd.WorkspaceName), ct);
        var workspace = Workspace.Create(cmd.WorkspaceName, slug, user.Id);
        var membership = Membership.Create(user.Id, workspace.Id, WorkspaceRole.Owner);

        var (rawRefresh, refreshHash) = _tokenHasher.GenerateAndHash();
        var refresh = RefreshToken.Issue(
            user.Id, workspace.Id, refreshHash, TimeSpan.FromDays(_jwtOptions.RefreshTokenDays));

        _db.Users.Add(user);
        _db.Workspaces.Add(workspace);
        _db.Memberships.Add(membership);
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync(ct);

        await _publisher.Publish(new UserRegistered(user.Id, workspace.Id, user.Email, user.DisplayName), ct);
        await _publisher.Publish(new UserUpdated(user.Id, user.Email, user.DisplayName, user.AvatarObjectKey, user.IsActive), ct);
        await _publisher.Publish(new WorkspaceCreated(workspace.Id, user.Id, workspace.Name, workspace.Slug), ct);
        await _publisher.Publish(new MemberAdded(workspace.Id, user.Id, user.Email, AzmaRoles.Owner), ct);

        var access = _jwt.Generate(user.Id, workspace.Id, user.Email, user.DisplayName, AzmaRoles.Owner);

        return new AuthTokensDto(
            access.Token, access.ExpiresAtUtc,
            rawRefresh, refresh.ExpiresAtUtc,
            new UserDto(user.Id, user.Email, user.DisplayName, UserDtoFactory.AvatarUrl(user.Id, user.AvatarObjectKey, user.UpdatedAtUtc), user.MustChangePassword),
            new WorkspaceDto(workspace.Id, workspace.Name, workspace.Slug, AzmaRoles.Owner, MembershipAccessScope.Workspace.ToString()));
    }

    private async Task<string> EnsureUniqueSlugAsync(string baseSlug, CancellationToken ct)
    {
        var slug = baseSlug;
        var i = 1;
        while (await _db.Workspaces.AnyAsync(w => w.Slug == slug, ct))
        {
            i++;
            slug = $"{baseSlug}-{i}";
        }
        return slug;
    }
}
