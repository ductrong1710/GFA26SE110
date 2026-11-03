using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class ThresholdChangeConfiguration : IEntityTypeConfiguration<ThresholdChange>
{
    public void Configure(EntityTypeBuilder<ThresholdChange> b)
    {
        b.ToTable("threshold_changes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.SensorChannelId).HasColumnName("sensor_channel_id");
        b.Property(x => x.ChangedByUserId).HasColumnName("changed_by_user_id");
        b.Property(x => x.ChangedAt).HasColumnName("changed_at");
        b.Property(x => x.MinValue).HasColumnName("min_value").HasPrecision(18, 6);
        b.Property(x => x.MaxValue).HasColumnName("max_value").HasPrecision(18, 6);
        b.Property(x => x.DataTimeoutMinutes).HasColumnName("data_timeout_minutes");
        b.Property(x => x.LowBatteryPercent).HasColumnName("low_battery_percent").HasPrecision(5, 2);
        b.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        b.HasOne<SensorChannel>().WithMany().HasForeignKey(x => x.SensorChannelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SensorChannelId, x.Id });
    }
}
