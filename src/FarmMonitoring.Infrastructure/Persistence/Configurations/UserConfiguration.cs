using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.PasswordHash).HasColumnName("password_hash").HasColumnType("text").IsRequired();
        b.Property(x => x.PasswordVersion).HasColumnName("password_version").HasDefaultValue(0).IsConcurrencyToken();
        b.Property(x => x.PasswordResetHash).HasColumnName("password_reset_hash").HasMaxLength(64);
        b.HasIndex(x => x.PasswordResetHash).IsUnique();
        b.Property(x => x.PasswordResetExpiresAt).HasColumnName("password_reset_expires_at");
        b.Property(x => x.PasswordResetRequestedAt).HasColumnName("password_reset_requested_at");
        b.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
    }
}
