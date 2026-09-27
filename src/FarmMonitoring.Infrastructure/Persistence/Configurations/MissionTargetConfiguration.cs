using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class MissionTargetConfiguration : IEntityTypeConfiguration<MissionTarget>
{
    public void Configure(EntityTypeBuilder<MissionTarget> b)
    {
        b.ToTable("mission_targets", t => t.HasCheckConstraint("CK_mission_targets_status", "status IN ('PENDING','COLLECTED','FAILED','SKIPPED')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.MissionId).HasColumnName("mission_id");
        b.Property(x => x.SensorNodeId).HasColumnName("sensor_node_id");
        b.Property(x => x.WaypointId).HasColumnName("waypoint_id");
        b.Property(x => x.SequenceNo).HasColumnName("sequence_no");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50).IsRequired();
        b.HasOne(x => x.Mission).WithMany(x => x.Targets).HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SensorNode).WithMany().HasForeignKey(x => x.SensorNodeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Waypoint).WithMany().HasForeignKey(x => x.WaypointId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.MissionId, x.SensorNodeId }).IsUnique();
    }
}
