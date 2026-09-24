using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class SensorNodeConfiguration : IEntityTypeConfiguration<SensorNode>
{
    public void Configure(EntityTypeBuilder<SensorNode> b)
    {
        b.ToTable("sensor_nodes", t => t.HasCheckConstraint("CK_sensor_nodes_battery_percent", "battery_percent BETWEEN 0 AND 100"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.ZoneId).HasColumnName("zone_id");
        b.Property(x => x.DeviceCode).HasColumnName("device_code").HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        b.Property(x => x.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
        b.Property(x => x.Longitude).HasColumnName("longitude").HasPrecision(10, 7);
        b.Property(x => x.LocalX).HasColumnName("local_x").HasPrecision(10, 3);
        b.Property(x => x.LocalY).HasColumnName("local_y").HasPrecision(10, 3);
        b.Property(x => x.BatteryPercent).HasColumnName("battery_percent").HasPrecision(5, 2);
        b.Property(x => x.LastSeenAt).HasColumnName("last_seen_at").HasColumnType("timestamp with time zone");
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        b.HasIndex(x => x.DeviceCode).IsUnique();
        b.HasIndex(x => x.ZoneId);
        b.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
    }
}
