using Azma.Common.Results;
using Azma.Identity.Api.Domain;
using Azma.Identity.Api.Infrastructure.Persistence;
using Azma.Identity.Api.Infrastructure.Security;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Identity.Api.Application.Auth;

/// <summary>
/// Authenticated user changes their own password. Verifies current password.
/// Clears the MustChangePassword flag and revokes all refresh tokens (forces re-login on other devices).
/// </summary>
public sealed record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result>;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public sealed class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _hasher;

    public ChangePasswordHandler(IdentityDbContext db, IPasswordHasher hasher)
    {
        _db = db; _hasher = hasher;
    }

    public async Task<Result> Handle(ChangePasswordCommand cmd, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == cmd.UserId, ct);
        if (user is null) return Result.Failure(IdentityErrors.UserNotFound);

        if (!_hasher.Verify(cmd.CurrentPassword, user.PasswordHash))
            return Result.Failure(IdentityErrors.InvalidPassword);

        user.ChangePassword(_hasher.Hash(cmd.NewPassword));

        // Revoke all refresh tokens (force re-login on other devices). The current axios interceptor
        // will handle the resulting 401 on next call by trying refresh — which will also fail and log out,
        // so the UI must call /me or re-login after change-password (frontend handles this).
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == cmd.UserId && t.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var t in tokens) t.Revoke();

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
