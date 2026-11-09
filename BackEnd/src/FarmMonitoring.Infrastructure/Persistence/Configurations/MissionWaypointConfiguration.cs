using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class MissionWaypointConfiguration : IEntityTypeConfiguration<MissionWaypoint>
{
    public void Configure(EntityTypeBuilder<MissionWaypoint> b)
    {
        b.ToTable("mission_waypoints", t =>
        {
            t.HasCheckConstraint("CK_mission_waypoints_sequence", "sequence_no > 0");
            t.HasCheckConstraint("CK_mission_waypoints_coordinates", "((latitude IS NOT NULL AND longitude IS NOT NULL) OR (local_x IS NOT NULL AND local_y IS NOT NULL)) AND (latitude IS NULL) = (longitude IS NULL) AND (local_x IS NULL) = (local_y IS NULL) AND (latitude BETWEEN -90 AND 90) AND (longitude BETWEEN -180 AND 180)");
            t.HasCheckConstraint("CK_mission_waypoints_hold", "planned_hold_seconds >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.MissionId).HasColumnName("mission_id");
        b.Property(x => x.SequenceNo).HasColumnName("sequence_no");
        b.Property(x => x.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
        b.Property(x => x.Longitude).HasColumnName("longitude").HasPrecision(10, 7);
        b.Property(x => x.LocalX).HasColumnName("local_x").HasPrecision(10, 3);
        b.Property(x => x.LocalY).HasColumnName("local_y").HasPrecision(10, 3);
        b.Property(x => x.AltitudeM).HasColumnName("altitude_m").HasPrecision(10, 3);
        b.Property(x => x.ActionType).HasColumnName("action_type").HasMaxLength(50);
        b.Property(x => x.PlannedHoldSeconds).HasColumnName("planned_hold_seconds");
        b.HasOne(x => x.Mission).WithMany(x => x.Waypoints).HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.MissionId, x.SequenceNo }).IsUnique();
    }
}
