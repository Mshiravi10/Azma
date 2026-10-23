using Azma.Common.Results;
using Azma.EventBus.Contracts.Identity;
using Azma.Identity.Api.Application.Common;
using Azma.Identity.Api.Domain;
using Azma.Identity.Api.Infrastructure.Persistence;
using Azma.Storage;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Azma.Identity.Api.Application.Users;

// ──────────────────────────── Errors ────────────────────────────
internal static class UserErrors
{
    public static readonly Error UserNotFound =
        Error.NotFound("identity.user_not_found", "کاربر یافت نشد.");

    public static readonly Error AvatarTooLarge =
        Error.Validation("identity.avatar_too_large", "اندازه تصویر بیش از حد مجاز است (حداکثر ۵ مگابایت).");

    public static readonly Error AvatarEmpty =
        Error.Validation("identity.avatar_empty", "فایل تصویر خالی است.");

    public static readonly Error AvatarNotImage =
        Error.Validation("identity.avatar_not_image", "فقط فایل تصویر مجاز است (PNG, JPG, WebP, GIF).");

    public static readonly Error AvatarMissing =
        Error.NotFound("identity.avatar_missing", "آواتاری برای این کاربر تنظیم نشده است.");
}

internal static class AvatarKeys
{
    public static string ForUser(Guid userId) => $"users/{userId:N}/avatar";

    public static readonly string[] AllowedContentTypes = new[]
    {
        "image/png", "image/jpeg", "image/jpg", "image/webp", "image/gif",
    };
}

// ──────────────────────────── Upload Avatar ────────────────────────────
public sealed record UploadAvatarCommand(
    Guid UserId,
    string ContentType,
    long Size,
    Stream Content) : IRequest<Result<UserDto>>;

public sealed class UploadAvatarHandler : IRequestHandler<UploadAvatarCommand, Result<UserDto>>
{
    private readonly IdentityDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly StorageOptions _opt;
    private readonly IPublishEndpoint _publisher;

    public UploadAvatarHandler(
        IdentityDbContext db,
        IObjectStorage storage,
        IOptions<StorageOptions> opt,
        IPublishEndpoint publisher)
    {
        _db = db;
        _storage = storage;
        _opt = opt.Value;
        _publisher = publisher;
    }

    public async Task<Result<UserDto>> Handle(UploadAvatarCommand cmd, CancellationToken ct)
    {
        if (cmd.Size <= 0) return Result.Failure<UserDto>(UserErrors.AvatarEmpty);
        if (cmd.Size > _opt.MaxUploadBytes) return Result.Failure<UserDto>(UserErrors.AvatarTooLarge);

        var ct0 = cmd.ContentType?.ToLowerInvariant() ?? string.Empty;
        if (!AvatarKeys.AllowedContentTypes.Contains(ct0))
            return Result.Failure<UserDto>(UserErrors.AvatarNotImage);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == cmd.UserId, ct);
        if (user is null) return Result.Failure<UserDto>(UserErrors.UserNotFound);

        var key = AvatarKeys.ForUser(user.Id);
        await _storage.PutAsync(_opt.DefaultBucket, key, cmd.Content, cmd.Size, ct0, ct);

        user.SetAvatar(key);
        await _db.SaveChangesAsync(ct);

        await _publisher.Publish(new UserAvatarChanged(user.Id, key), ct);
        await _publisher.Publish(new UserUpdated(user.Id, user.Email, user.DisplayName, user.AvatarObjectKey, user.IsActive), ct);

        return new UserDto(
            user.Id, user.Email, user.DisplayName,
            UserDtoFactory.AvatarUrl(user.Id, user.AvatarObjectKey, user.UpdatedAtUtc),
            user.MustChangePassword);
    }
}

// ──────────────────────────── Delete Avatar ────────────────────────────
public sealed record DeleteAvatarCommand(Guid UserId) : IRequest<Result<UserDto>>;

public sealed class DeleteAvatarHandler : IRequestHandler<DeleteAvatarCommand, Result<UserDto>>
{
    private readonly IdentityDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly StorageOptions _opt;
    private readonly IPublishEndpoint _publisher;

    public DeleteAvatarHandler(
        IdentityDbContext db,
        IObjectStorage storage,
        IOptions<StorageOptions> opt,
        IPublishEndpoint publisher)
    {
        _db = db;
        _storage = storage;
        _opt = opt.Value;
        _publisher = publisher;
    }

    public async Task<Result<UserDto>> Handle(DeleteAvatarCommand cmd, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == cmd.UserId, ct);
        if (user is null) return Result.Failure<UserDto>(UserErrors.UserNotFound);

        if (!string.IsNullOrEmpty(user.AvatarObjectKey))
        {
            await _storage.DeleteAsync(_opt.DefaultBucket, user.AvatarObjectKey, ct);
        }

        user.SetAvatar(null);
        await _db.SaveChangesAsync(ct);

        await _publisher.Publish(new UserAvatarChanged(user.Id, null), ct);
        await _publisher.Publish(new UserUpdated(user.Id, user.Email, user.DisplayName, null, user.IsActive), ct);

        return new UserDto(
            user.Id, user.Email, user.DisplayName,
            null, user.MustChangePassword);
    }
}

// ──────────────────────────── Get Avatar Stream ────────────────────────────
public sealed record GetAvatarQuery(Guid UserId) : IRequest<Result<AvatarStreamDto>>;

public sealed record AvatarStreamDto(Stream Content, string ContentType, string ObjectKey);

public sealed class GetAvatarHandler : IRequestHandler<GetAvatarQuery, Result<AvatarStreamDto>>
{
    private readonly IdentityDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly StorageOptions _opt;

    public GetAvatarHandler(IdentityDbContext db, IObjectStorage storage, IOptions<StorageOptions> opt)
    {
        _db = db;
        _storage = storage;
        _opt = opt.Value;
    }

    public async Task<Result<AvatarStreamDto>> Handle(GetAvatarQuery q, CancellationToken ct)
    {
        var info = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == q.UserId)
            .Select(u => new { u.AvatarObjectKey })
            .FirstOrDefaultAsync(ct);

        if (info is null || string.IsNullOrEmpty(info.AvatarObjectKey))
            return Result.Failure<AvatarStreamDto>(UserErrors.AvatarMissing);

        // Heuristic content-type from extension; default to png. The actual stored type matches uploaded file.
        var contentType = info.AvatarObjectKey.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                          info.AvatarObjectKey.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            ? "image/jpeg"
            : info.AvatarObjectKey.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
            : info.AvatarObjectKey.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? "image/gif"
            : "image/png";

        var stream = await _storage.GetAsync(_opt.DefaultBucket, info.AvatarObjectKey, ct);
        return new AvatarStreamDto(stream, contentType, info.AvatarObjectKey);
    }
}

// ──────────────────────────── Backfill UserUpdated ────────────────────────────
/// <summary>
/// Admin/dev command to re-publish a <see cref="UserUpdated"/> event for every user,
/// hydrating the read-models in downstream services after they're added.
/// </summary>
public sealed record BackfillUsersCommand : IRequest<Result<int>>;

public sealed class BackfillUsersHandler : IRequestHandler<BackfillUsersCommand, Result<int>>
{
    private readonly IdentityDbContext _db;
    private readonly IPublishEndpoint _publisher;

    public BackfillUsersHandler(IdentityDbContext db, IPublishEndpoint publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public async Task<Result<int>> Handle(BackfillUsersCommand cmd, CancellationToken ct)
    {
        var users = await _db.Users.AsNoTracking().ToListAsync(ct);
        foreach (var u in users)
        {
            await _publisher.Publish(new UserUpdated(u.Id, u.Email, u.DisplayName, u.AvatarObjectKey, u.IsActive), ct);
        }
        return users.Count;
    }
}
