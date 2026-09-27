using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class SensorThresholdConfiguration : IEntityTypeConfiguration<SensorThreshold>
{
    public void Configure(EntityTypeBuilder<SensorThreshold> b)
    {
        b.ToTable("sensor_thresholds", t =>
        {
            t.HasCheckConstraint("CK_sensor_thresholds_value_bounds", "min_value <= max_value");
            t.HasCheckConstraint("CK_sensor_thresholds_timeout", "data_timeout_minutes > 0");
            t.HasCheckConstraint("CK_sensor_thresholds_battery", "low_battery_percent BETWEEN 0 AND 100");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.SensorChannelId).HasColumnName("sensor_channel_id");
        b.Property(x => x.MinValue).HasColumnName("min_value").HasPrecision(18, 6);
        b.Property(x => x.MaxValue).HasColumnName("max_value").HasPrecision(18, 6);
        b.Property(x => x.DataTimeoutMinutes).HasColumnName("data_timeout_minutes");
        b.Property(x => x.LowBatteryPercent).HasColumnName("low_battery_percent").HasPrecision(5, 2);
        b.Property(x => x.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        b.HasIndex(x => x.SensorChannelId).IsUnique();
        b.HasOne(x => x.SensorChannel).WithMany().HasForeignKey(x => x.SensorChannelId).OnDelete(DeleteBehavior.Restrict);
    }
}
