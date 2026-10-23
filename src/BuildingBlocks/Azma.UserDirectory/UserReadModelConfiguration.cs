using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Azma.UserDirectory;

/// <summary>
/// EF configuration for the shared <see cref="UserReadModel"/> table.
/// Uses the DbContext's default schema; each service controls placement
/// via <c>modelBuilder.HasDefaultSchema(...)</c>.
/// </summary>
public sealed class UserReadModelConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<UserReadModel>
{
    public const string TableName = "users_read_model";

    public void Configure(EntityTypeBuilder<UserReadModel> b)
    {
        b.ToTable(TableName);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Email).IsRequired().HasMaxLength(256);
        b.Property(x => x.DisplayName).IsRequired().HasMaxLength(128);
        b.Property(x => x.AvatarObjectKey).HasMaxLength(256);
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.UpdatedAtUtc).IsRequired();

        b.HasIndex(x => x.Email);
    }
}
