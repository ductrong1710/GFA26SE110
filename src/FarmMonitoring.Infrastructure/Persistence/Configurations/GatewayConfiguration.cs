using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class GatewayConfiguration : IEntityTypeConfiguration<Gateway>
{
    public void Configure(EntityTypeBuilder<Gateway> b)
    {
        b.ToTable("gateways", t => t.HasCheckConstraint("CK_gateways_battery_percent", "battery_percent BETWEEN 0 AND 100"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        b.Property(x => x.GatewayType).HasColumnName("gateway_type").HasMaxLength(50).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        b.Property(x => x.UavId).HasColumnName("uav_id");
        b.Property(x => x.BatteryPercent).HasColumnName("battery_percent").HasPrecision(5, 2);
        b.Property(x => x.LastSeenAt).HasColumnName("last_seen_at").HasColumnType("timestamp with time zone");
        b.Property(x => x.FirmwareVersion).HasColumnName("firmware_version").HasMaxLength(100);
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        b.HasIndex(x => x.Code).IsUnique();
        b.HasOne(x => x.Uav).WithMany().HasForeignKey(x => x.UavId).OnDelete(DeleteBehavior.Restrict);
    }
}
