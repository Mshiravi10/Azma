using System.Security.Cryptography;
using Azma.Auth;
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
/// Admin creates a new user directly inside the current workspace with a temporary password.
/// The user is forced to change the password on first login (MustChangePassword flag).
/// Returns the temporary password in the response so the admin can hand it to the user.
/// </summary>
public sealed record AdminCreateUserCommand(
    Guid WorkspaceId,
    string CurrentUserRole,
    string Email,
    string DisplayName,
    WorkspaceRole Role) : IRequest<Result<AdminCreateUserResult>>;

public sealed record AdminCreateUserResult(MembershipDto Member, string TemporaryPassword);

public sealed class AdminCreateUserValidator : AbstractValidator<AdminCreateUserCommand>
{
    public AdminCreateUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.DisplayName).NotEmpty().MinimumLength(2).MaximumLength(120);
        RuleFor(x => x.Role).NotEqual(WorkspaceRole.Owner)
            .WithMessage("نقش مالک قابل اعطا با ساخت کاربر نیست.");
    }
}

public sealed class AdminCreateUserHandler
    : IRequestHandler<AdminCreateUserCommand, Result<AdminCreateUserResult>>
{
    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IPublishEndpoint _publisher;

    public AdminCreateUserHandler(IdentityDbContext db, IPasswordHasher hasher, IPublishEndpoint publisher)
    {
        _db = db; _hasher = hasher; _publisher = publisher;
    }

    public async Task<Result<AdminCreateUserResult>> Handle(AdminCreateUserCommand cmd, CancellationToken ct)
    {
        if (cmd.CurrentUserRole != AzmaRoles.Owner && cmd.CurrentUserRole != AzmaRoles.Admin)
            return Result.Failure<AdminCreateUserResult>(IdentityErrors.AdminOnly);

        var email = cmd.Email.Trim().ToLowerInvariant();
        var displayName = cmd.DisplayName.Trim();

        var existing = await _db.Users
            .Include(u => u.Memberships)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (existing is not null)
        {
            if (existing.Memberships.Any(m => m.WorkspaceId == cmd.WorkspaceId))
                return Result.Failure<AdminCreateUserResult>(IdentityErrors.AlreadyMember);

            // Existing user from another workspace — just add membership, no temp password needed
            var existingMembership = Membership.Create(existing.Id, cmd.WorkspaceId, cmd.Role);
            _db.Memberships.Add(existingMembership);
            await _db.SaveChangesAsync(ct);

            await _publisher.Publish(
                new MemberAdded(cmd.WorkspaceId, existing.Id, existing.Email, cmd.Role.ToString()), ct);

            return new AdminCreateUserResult(
                new MembershipDto(
                    existing.Id, existing.Email, existing.DisplayName,
                    UserDtoFactory.AvatarUrl(existing.Id, existing.AvatarObjectKey, existing.UpdatedAtUtc),
                    cmd.Role.ToString(), existingMembership.AccessScope.ToString(),
                    existingMembership.JoinedAtUtc, existing.LastLoginAtUtc, existing.IsActive),
                TemporaryPassword: string.Empty);
        }

        var tempPassword = GenerateTemporaryPassword();
        var user = User.Create(email, _hasher.Hash(tempPassword), displayName, mustChangePassword: true);
        var membership = Membership.Create(user.Id, cmd.WorkspaceId, cmd.Role);

        _db.Users.Add(user);
        _db.Memberships.Add(membership);
        await _db.SaveChangesAsync(ct);

        await _publisher.Publish(new UserRegistered(user.Id, cmd.WorkspaceId, user.Email, user.DisplayName), ct);
        await _publisher.Publish(new UserUpdated(user.Id, user.Email, user.DisplayName, user.AvatarObjectKey, user.IsActive), ct);
        await _publisher.Publish(new MemberAdded(cmd.WorkspaceId, user.Id, user.Email, cmd.Role.ToString()), ct);

        return new AdminCreateUserResult(
            new MembershipDto(
                user.Id, user.Email, user.DisplayName,
                UserDtoFactory.AvatarUrl(user.Id, user.AvatarObjectKey, user.UpdatedAtUtc),
                cmd.Role.ToString(), membership.AccessScope.ToString(),
                membership.JoinedAtUtc, user.LastLoginAtUtc, user.IsActive),
            tempPassword);
    }

    private static string GenerateTemporaryPassword()
    {
        // 12 chars: a-zA-Z0-9 plus a couple of symbols, must satisfy 8-char minimum.
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789@#$";
        Span<byte> bytes = stackalloc byte[12];
        RandomNumberGenerator.Fill(bytes);
        Span<char> chars = stackalloc char[12];
        for (var i = 0; i < bytes.Length; i++)
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }
}
