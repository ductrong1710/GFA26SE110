using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class GatewayCredentialConfiguration : IEntityTypeConfiguration<GatewayCredential>
{
    public void Configure(EntityTypeBuilder<GatewayCredential> b)
    {
        b.ToTable("gateway_credentials", t =>
        {
            t.HasCheckConstraint("CK_gateway_credentials_hash", "key_hash ~ '^[0-9A-Fa-f]{64}$'");
            t.HasCheckConstraint("CK_gateway_credentials_state", "(is_active AND revoked_at IS NULL) OR (NOT is_active AND revoked_at IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.GatewayId).HasColumnName("gateway_id");
        b.Property(x => x.KeyHash).HasColumnName("key_hash").HasMaxLength(64).IsRequired();
        b.Property(x => x.KeyPrefix).HasColumnName("key_prefix").HasMaxLength(8);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        b.Property(x => x.LastUsedAt).HasColumnName("last_used_at").HasColumnType("timestamp with time zone");
        b.Property(x => x.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        b.HasOne(x => x.Gateway).WithMany().HasForeignKey(x => x.GatewayId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.GatewayId).IsUnique().HasFilter("is_active");
        b.HasIndex(x => new { x.GatewayId, x.CreatedAt });
    }
}
