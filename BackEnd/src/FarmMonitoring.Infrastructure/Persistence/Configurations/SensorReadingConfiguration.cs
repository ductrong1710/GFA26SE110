using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class SensorReadingConfiguration : IEntityTypeConfiguration<SensorReading>
{
    public void Configure(EntityTypeBuilder<SensorReading> b)
    {
        b.ToTable("sensor_readings", t => t.HasCheckConstraint("CK_sensor_readings_time", "measured_at <= collected_at"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.SensorChannelId).HasColumnName("sensor_channel_id");
        b.Property(x => x.GatewayId).HasColumnName("gateway_id");
        b.Property(x => x.MissionId).HasColumnName("mission_id");
        b.Property(x => x.SourceRecordKey).HasColumnName("source_record_key").HasMaxLength(150).IsRequired();
        b.Property(x => x.Value).HasColumnName("value").HasPrecision(18, 6);
        b.Property(x => x.MeasuredAt).HasColumnName("measured_at");
        b.Property(x => x.CollectedAt).HasColumnName("collected_at");
        b.Property(x => x.ReceivedAt).HasColumnName("received_at");
        b.Property(x => x.QualityStatus).HasColumnName("quality_status").HasMaxLength(50);
        b.Property(x => x.IsValid).HasColumnName("is_valid").HasDefaultValue(true);
        b.Property(x => x.ValidationError).HasColumnName("validation_error").HasMaxLength(500);
        b.HasOne(x => x.SensorChannel).WithMany().HasForeignKey(x => x.SensorChannelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Gateway).WithMany().HasForeignKey(x => x.GatewayId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Mission).WithMany().HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SensorChannelId, x.SourceRecordKey }).IsUnique();
        b.HasIndex(x => new { x.SensorChannelId, x.MeasuredAt });
    }
}
