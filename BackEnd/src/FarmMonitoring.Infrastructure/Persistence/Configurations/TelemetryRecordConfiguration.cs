using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class TelemetryRecordConfiguration : IEntityTypeConfiguration<TelemetryRecord>
{
    public void Configure(EntityTypeBuilder<TelemetryRecord> b)
    {
        b.ToTable("telemetry_records", t =>
        {
            t.HasCheckConstraint("CK_telemetry_battery", "battery_percent BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_telemetry_coordinates", "(latitude IS NULL) = (longitude IS NULL) AND (local_x IS NULL) = (local_y IS NULL) AND latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.MissionId).HasColumnName("mission_id");
        b.Property(x => x.UavId).HasColumnName("uav_id");
        b.Property(x => x.GatewayId).HasColumnName("gateway_id");
        b.Property(x => x.RecordedAt).HasColumnName("recorded_at");
        b.Property(x => x.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
        b.Property(x => x.Longitude).HasColumnName("longitude").HasPrecision(10, 7);
        b.Property(x => x.LocalX).HasColumnName("local_x").HasPrecision(10, 3);
        b.Property(x => x.LocalY).HasColumnName("local_y").HasPrecision(10, 3);
        b.Property(x => x.AltitudeM).HasColumnName("altitude_m").HasPrecision(10, 3);
        b.Property(x => x.BatteryPercent).HasColumnName("battery_percent").HasPrecision(5, 2);
        b.Property(x => x.CurrentWaypointNo).HasColumnName("current_waypoint_no");
        b.Property(x => x.FlightStatus).HasColumnName("flight_status").HasMaxLength(50);
        b.HasOne(x => x.Mission).WithMany().HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Uav).WithMany().HasForeignKey(x => x.UavId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Gateway).WithMany().HasForeignKey(x => x.GatewayId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.MissionId, x.RecordedAt });
    }
}
