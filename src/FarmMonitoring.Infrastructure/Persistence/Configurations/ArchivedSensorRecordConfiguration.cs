using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class ArchivedSensorRecordConfiguration : IEntityTypeConfiguration<ArchivedSensorRecord>
{
    public void Configure(EntityTypeBuilder<ArchivedSensorRecord> b)
    {
        b.ToTable("archived_sensor_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.SensorNodeId).HasColumnName("sensor_node_id");
        b.Property(x => x.GatewayId).HasColumnName("gateway_id");
        b.Property(x => x.MissionId).HasColumnName("mission_id");
        b.Property(x => x.SourceRecordKey).HasColumnName("source_record_key").HasMaxLength(150).IsRequired();
        b.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.ReceivedAt).HasColumnName("received_at");
        b.HasOne(x => x.SensorNode).WithMany().HasForeignKey(x => x.SensorNodeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Gateway).WithMany().HasForeignKey(x => x.GatewayId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Mission).WithMany().HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SensorNodeId, x.SourceRecordKey }).IsUnique();
    }
}
