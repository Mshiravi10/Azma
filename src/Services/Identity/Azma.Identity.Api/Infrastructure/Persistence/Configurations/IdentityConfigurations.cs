using Azma.Identity.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Azma.Identity.Api.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.AvatarObjectKey).HasMaxLength(256);
        b.Property(x => x.MustChangePassword).HasDefaultValue(false);
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
        b.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");

        b.HasMany(x => x.Memberships)
            .WithOne(x => x.User!)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.RefreshTokens)
            .WithOne()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.McpTokens)
            .WithOne(x => x.User!)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> b)
    {
        b.ToTable("workspaces");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.OwnerUserId).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");

        b.HasMany(x => x.Memberships)
            .WithOne(x => x.Workspace!)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Invitations)
            .WithOne(x => x.Workspace!)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> b)
    {
        b.ToTable("memberships");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.WorkspaceId }).IsUnique();
        b.Property(x => x.Role).HasConversion<int>();
        b.Property(x => x.AccessScope).HasConversion<int>().HasDefaultValue(MembershipAccessScope.Workspace);
        b.Property(x => x.JoinedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.WorkspaceId });
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
    }
}

public sealed class McpTokenConfiguration : IEntityTypeConfiguration<McpToken>
{
    public void Configure(EntityTypeBuilder<McpToken> b)
    {
        b.ToTable("mcp_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.WorkspaceId });
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");

        b.HasOne(x => x.Workspace)
            .WithMany()
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> b)
    {
        b.ToTable("invitations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.WorkspaceId, x.Email, x.Status });
        b.Property(x => x.Role).HasConversion<int>();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("now() at time zone 'utc'");
    }
}
