using Azma.Auth;
using Azma.Common.Results;
using Azma.Identity.Api.Application.Common;
using Azma.Identity.Api.Domain;
using Azma.Identity.Api.Infrastructure.Persistence;
using Azma.Identity.Api.Infrastructure.Security;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Identity.Api.Application.Members;

public sealed record InviteMemberCommand(
    Guid WorkspaceId,
    Guid InvitedByUserId,
    string CurrentUserRole,
    string Email,
    WorkspaceRole Role) : IRequest<Result<InvitationDto>>;

public sealed class InviteMemberValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Role)
            .NotEqual(WorkspaceRole.Owner)
            .WithMessage("نقش مالک قابل دعوت نیست.");
    }
}

public sealed class InviteMemberHandler : IRequestHandler<InviteMemberCommand, Result<InvitationDto>>
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    private readonly IdentityDbContext _db;
    private readonly ITokenHasher _tokenHasher;

    public InviteMemberHandler(IdentityDbContext db, ITokenHasher tokenHasher)
    {
        _db = db; _tokenHasher = tokenHasher;
    }

    public async Task<Result<InvitationDto>> Handle(InviteMemberCommand cmd, CancellationToken ct)
    {
        if (cmd.CurrentUserRole != AzmaRoles.Owner && cmd.CurrentUserRole != AzmaRoles.Admin)
            return Result.Failure<InvitationDto>(Error.Forbidden("identity.forbidden", "اجازهٔ دعوت ندارید."));

        var email = cmd.Email.Trim().ToLowerInvariant();

        var existingMember = await _db.Memberships
            .Include(m => m.User)
            .AnyAsync(m => m.WorkspaceId == cmd.WorkspaceId && m.User!.Email == email, ct);
        if (existingMember)
            return Result.Failure<InvitationDto>(IdentityErrors.AlreadyMember);

        var (raw, hash) = _tokenHasher.GenerateAndHash();
        var invitation = Invitation.Create(
            cmd.WorkspaceId, email, cmd.Role, hash, cmd.InvitedByUserId, InvitationLifetime);

        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync(ct);

        return new InvitationDto(
            invitation.Id, invitation.Email, invitation.Role.ToString(),
            invitation.Status.ToString(), invitation.CreatedAtUtc, invitation.ExpiresAtUtc, raw);
    }
}
