using Azma.EventBus.Contracts.Identity;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Azma.UserDirectory.Consumers;

/// <summary>Upserts the user read-model on profile changes (create/update/avatar/active).</summary>
public sealed class UserUpdatedConsumer : IConsumer<UserUpdated>
{
    private readonly IUserDirectoryDbContext _db;
    private readonly ILogger<UserUpdatedConsumer> _logger;

    public UserUpdatedConsumer(IUserDirectoryDbContext db, ILogger<UserUpdatedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserUpdated> ctx)
    {
        var msg = ctx.Message;
        var existing = await _db.UserDirectory.FirstOrDefaultAsync(x => x.Id == msg.UserId, ctx.CancellationToken);
        if (existing is null)
        {
            _db.UserDirectory.Add(new UserReadModel
            {
                Id = msg.UserId,
                Email = msg.Email,
                DisplayName = msg.DisplayName,
                AvatarObjectKey = msg.AvatarObjectKey,
                IsActive = msg.IsActive,
                UpdatedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Email = msg.Email;
            existing.DisplayName = msg.DisplayName;
            existing.AvatarObjectKey = msg.AvatarObjectKey;
            existing.IsActive = msg.IsActive;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ctx.CancellationToken);
        _logger.LogDebug("UserDirectory upserted {UserId}", msg.UserId);
    }
}

/// <summary>Updates only the avatar field (lightweight broadcast).</summary>
public sealed class UserAvatarChangedConsumer : IConsumer<UserAvatarChanged>
{
    private readonly IUserDirectoryDbContext _db;

    public UserAvatarChangedConsumer(IUserDirectoryDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<UserAvatarChanged> ctx)
    {
        var existing = await _db.UserDirectory.FirstOrDefaultAsync(x => x.Id == ctx.Message.UserId, ctx.CancellationToken);
        if (existing is null) return; // will be filled by next UserUpdated
        existing.AvatarObjectKey = ctx.Message.AvatarObjectKey;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ctx.CancellationToken);
    }
}

/// <summary>Marks a user inactive in this service's read-model.</summary>
public sealed class UserDeactivatedConsumer : IConsumer<UserDeactivated>
{
    private readonly IUserDirectoryDbContext _db;

    public UserDeactivatedConsumer(IUserDirectoryDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<UserDeactivated> ctx)
    {
        var existing = await _db.UserDirectory.FirstOrDefaultAsync(x => x.Id == ctx.Message.UserId, ctx.CancellationToken);
        if (existing is null) return;
        existing.IsActive = false;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ctx.CancellationToken);
    }
}
