using Azma.Auth;
using Azma.Auth.Jwt;
using Azma.Common.Results;
using Azma.EventBus.Contracts.Identity;
using Azma.Identity.Api.Application.Common;
using Azma.Identity.Api.Domain;
using Azma.Identity.Api.Infrastructure.Persistence;
using Azma.Identity.Api.Infrastructure.Security;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Identity.Api.Application.Members;

/// <summary>
/// Accepts an invitation. If a user with the invited email exists, just adds the membership.
/// Otherwise creates the user (using supplied password + name) and adds the membership.
/// Returns auth tokens for the new workspace.
/// </summary>
public sealed record AcceptInvitationCommand(
    string InvitationToken,
    string? Password,
    string? DisplayName) : IRequest<Result<AuthTokensDto>>;

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator() =>
        RuleFor(x => x.InvitationToken).NotEmpty();
}

public sealed class AcceptInvitationHandler : IRequestHandler<AcceptInvitationCommand, Result<AuthTokensDto>>
{
    private readonly IdentityDbContext _db;
    private readonly ITokenHasher _tokenHasher;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwt;
    private readonly JwtOptions _jwtOptions;
    private readonly IPublishEndpoint _publisher;

    public AcceptInvitationHandler(
        IdentityDbContext db,
        ITokenHasher tokenHasher,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwt,
        JwtOptions jwtOptions,
        IPublishEndpoint publisher)
    {
        _db = db; _tokenHasher = tokenHasher; _passwordHasher = passwordHasher;
        _jwt = jwt; _jwtOptions = jwtOptions; _publisher = publisher;
    }

    public async Task<Result<AuthTokensDto>> Handle(AcceptInvitationCommand cmd, CancellationToken ct)
    {
        var hash = _tokenHasher.Hash(cmd.InvitationToken);
        var invitation = await _db.Invitations
            .Include(i => i.Workspace)
            .FirstOrDefaultAsync(i => i.TokenHash == hash, ct);

        if (invitation is null || !invitation.IsUsable() || invitation.Workspace is null)
            return Result.Failure<AuthTokensDto>(IdentityErrors.InvitationNotFound);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == invitation.Email, ct);
        var isNewUser = user is null;

        if (isNewUser)
        {
            if (string.IsNullOrWhiteSpace(cmd.Password) || cmd.Password.Length < 8)
                return Result.Failure<AuthTokensDto>(
                    Error.Validation("identity.password_required", "گذرواژه (حداقل ۸ کاراکتر) لازم است."));
            if (string.IsNullOrWhiteSpace(cmd.DisplayName))
                return Result.Failure<AuthTokensDto>(
                    Error.Validation("identity.name_required", "نام نمایشی لازم است."));

            user = User.Create(invitation.Email, _passwordHasher.Hash(cmd.Password), cmd.DisplayName);
            _db.Users.Add(user);
        }

        var alreadyMember = await _db.Memberships
            .AnyAsync(m => m.UserId == user!.Id && m.WorkspaceId == invitation.WorkspaceId, ct);
        if (alreadyMember)
            return Result.Failure<AuthTokensDto>(IdentityErrors.AlreadyMember);

        var membership = Membership.Create(user!.Id, invitation.WorkspaceId, invitation.Role);
        _db.Memberships.Add(membership);

        invitation.MarkAccepted();

        var (rawRefresh, refreshHash) = _tokenHasher.GenerateAndHash();
        var refresh = RefreshToken.Issue(
            user.Id, invitation.WorkspaceId, refreshHash,
            TimeSpan.FromDays(_jwtOptions.RefreshTokenDays));
        _db.RefreshTokens.Add(refresh);

        await _db.SaveChangesAsync(ct);

        if (isNewUser)
            await _publisher.Publish(new UserRegistered(user.Id, invitation.WorkspaceId, user.Email, user.DisplayName), ct);
        await _publisher.Publish(new UserUpdated(user.Id, user.Email, user.DisplayName, user.AvatarObjectKey, user.IsActive), ct);
        await _publisher.Publish(new MemberAdded(invitation.WorkspaceId, user.Id, user.Email, invitation.Role.ToString()), ct);

        var roleName = invitation.Role.ToString();
        var scopeName = membership.AccessScope.ToString();
        var access = _jwt.Generate(user.Id, invitation.WorkspaceId, user.Email, user.DisplayName, roleName, scopeName);

        return new AuthTokensDto(
            access.Token, access.ExpiresAtUtc,
            rawRefresh, refresh.ExpiresAtUtc,
            new UserDto(user.Id, user.Email, user.DisplayName, UserDtoFactory.AvatarUrl(user.Id, user.AvatarObjectKey, user.UpdatedAtUtc), user.MustChangePassword),
            new WorkspaceDto(invitation.Workspace.Id, invitation.Workspace.Name, invitation.Workspace.Slug, roleName, scopeName));
    }
}
