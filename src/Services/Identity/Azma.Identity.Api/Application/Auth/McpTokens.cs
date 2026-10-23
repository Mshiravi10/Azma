using Azma.Common.Results;
using Azma.Identity.Api.Application.Common;
using Azma.Identity.Api.Domain;
using Azma.Identity.Api.Infrastructure.Persistence;
using Azma.Identity.Api.Infrastructure.Security;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Azma.Identity.Api.Application.Auth;

public sealed record ListMcpTokensQuery(Guid UserId, Guid WorkspaceId)
    : IRequest<Result<IReadOnlyList<McpTokenDto>>>;

public sealed record CreateMcpTokenCommand(
    Guid UserId,
    Guid WorkspaceId,
    string Name,
    string CurrentPassword,
    int? ExpiresInDays) : IRequest<Result<CreateMcpTokenResponse>>;

public sealed record RevokeMcpTokenCommand(Guid UserId, Guid WorkspaceId, Guid TokenId)
    : IRequest<Result>;

public sealed record ValidateMcpTokenCommand(string RawToken)
    : IRequest<Result<ValidateMcpTokenResponse>>;

public sealed class CreateMcpTokenValidator : AbstractValidator<CreateMcpTokenCommand>
{
    public CreateMcpTokenValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.ExpiresInDays)
            .InclusiveBetween(1, 365)
            .When(x => x.ExpiresInDays is not null);
    }
}

public sealed class ListMcpTokensHandler
    : IRequestHandler<ListMcpTokensQuery, Result<IReadOnlyList<McpTokenDto>>>
{
    private readonly IdentityDbContext _db;

    public ListMcpTokensHandler(IdentityDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<McpTokenDto>>> Handle(
        ListMcpTokensQuery q,
        CancellationToken ct)
    {
        var tokens = await _db.McpTokens
            .AsNoTracking()
            .Where(x => x.UserId == q.UserId && x.WorkspaceId == q.WorkspaceId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new McpTokenDto(
                x.Id,
                x.Name,
                x.CreatedAtUtc,
                x.ExpiresAtUtc,
                x.LastUsedAtUtc,
                x.RevokedAtUtc,
                StatusFor(x.ExpiresAtUtc, x.RevokedAtUtc)))
            .ToListAsync(ct);

        return tokens;
    }

    private static string StatusFor(DateTime expiresAtUtc, DateTime? revokedAtUtc)
    {
        if (revokedAtUtc is not null)
            return "Revoked";

        return DateTime.UtcNow >= expiresAtUtc ? "Expired" : "Active";
    }
}

public sealed class CreateMcpTokenHandler
    : IRequestHandler<CreateMcpTokenCommand, Result<CreateMcpTokenResponse>>
{
    private const int DefaultExpiresInDays = 90;

    private readonly IdentityDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenHasher _tokenHasher;

    public CreateMcpTokenHandler(
        IdentityDbContext db,
        IPasswordHasher passwordHasher,
        ITokenHasher tokenHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenHasher = tokenHasher;
    }

    public async Task<Result<CreateMcpTokenResponse>> Handle(
        CreateMcpTokenCommand cmd,
        CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == cmd.UserId, ct);
        if (user is null || !user.IsActive)
            return IdentityErrors.UserNotFound;

        if (!_passwordHasher.Verify(cmd.CurrentPassword, user.PasswordHash))
            return IdentityErrors.InvalidPassword;

        var membershipExists = await _db.Memberships.AnyAsync(
            x => x.UserId == cmd.UserId && x.WorkspaceId == cmd.WorkspaceId,
            ct);

        if (!membershipExists)
            return IdentityErrors.NotMemberOfWorkspace;

        var (secret, _) = _tokenHasher.GenerateAndHash();
        var rawToken = McpToken.RawTokenPrefix + secret;
        var token = McpToken.Issue(
            cmd.UserId,
            cmd.WorkspaceId,
            cmd.Name,
            _tokenHasher.Hash(rawToken),
            TimeSpan.FromDays(cmd.ExpiresInDays ?? DefaultExpiresInDays));

        _db.McpTokens.Add(token);
        await _db.SaveChangesAsync(ct);

        return new CreateMcpTokenResponse(
            token.Id,
            token.Name,
            rawToken,
            token.CreatedAtUtc,
            token.ExpiresAtUtc);
    }
}

public sealed class RevokeMcpTokenHandler : IRequestHandler<RevokeMcpTokenCommand, Result>
{
    private readonly IdentityDbContext _db;

    public RevokeMcpTokenHandler(IdentityDbContext db) => _db = db;

    public async Task<Result> Handle(RevokeMcpTokenCommand cmd, CancellationToken ct)
    {
        var token = await _db.McpTokens.FirstOrDefaultAsync(
            x => x.Id == cmd.TokenId && x.UserId == cmd.UserId && x.WorkspaceId == cmd.WorkspaceId,
            ct);

        if (token is null)
            return Result.Failure(IdentityErrors.McpTokenNotFound);

        token.Revoke();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class ValidateMcpTokenHandler
    : IRequestHandler<ValidateMcpTokenCommand, Result<ValidateMcpTokenResponse>>
{
    private readonly IdentityDbContext _db;
    private readonly ITokenHasher _tokenHasher;

    public ValidateMcpTokenHandler(IdentityDbContext db, ITokenHasher tokenHasher)
    {
        _db = db;
        _tokenHasher = tokenHasher;
    }

    public async Task<Result<ValidateMcpTokenResponse>> Handle(
        ValidateMcpTokenCommand cmd,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.RawToken) ||
            !cmd.RawToken.StartsWith(McpToken.RawTokenPrefix, StringComparison.Ordinal))
        {
            return IdentityErrors.InvalidMcpToken;
        }

        var hash = _tokenHasher.Hash(cmd.RawToken);
        var token = await _db.McpTokens
            .Include(x => x.User)
            .Include(x => x.Workspace)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, ct);

        if (token is null ||
            !token.IsActive ||
            token.User is null ||
            token.Workspace is null ||
            !token.User.IsActive)
        {
            return IdentityErrors.InvalidMcpToken;
        }

        var membership = await _db.Memberships.AsNoTracking().FirstOrDefaultAsync(
            x => x.UserId == token.UserId && x.WorkspaceId == token.WorkspaceId,
            ct);

        if (membership is null)
            return IdentityErrors.InvalidMcpToken;

        token.MarkUsed();
        await _db.SaveChangesAsync(ct);

        return new ValidateMcpTokenResponse(
            token.User.Id,
            token.User.Email,
            token.User.DisplayName,
            token.Workspace.Id,
            token.Workspace.Name,
            token.Workspace.Slug,
            membership.Role.ToString(),
            membership.AccessScope.ToString());
    }
}
