using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> b)
    {
        b.ToTable("alerts", t =>
        {
            t.HasCheckConstraint("CK_alerts_gateway_error", "alert_type <> 'GATEWAY_ERROR' OR gateway_id IS NOT NULL");
            t.HasCheckConstraint("CK_alerts_status", "status IN ('OPEN','ACKNOWLEDGED','CLOSED')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.AlertType).HasColumnName("alert_type").HasConversion<string>().HasMaxLength(100).IsRequired();
        b.Property(x => x.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(50).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50).IsRequired();
        b.Property(x => x.SensorNodeId).HasColumnName("sensor_node_id");
        b.Property(x => x.SensorChannelId).HasColumnName("sensor_channel_id");
        b.Property(x => x.GatewayId).HasColumnName("gateway_id");
        b.Property(x => x.UavId).HasColumnName("uav_id");
        b.Property(x => x.MissionId).HasColumnName("mission_id");
        b.Property(x => x.Message).HasColumnName("message").HasColumnType("text").IsRequired();
        b.Property(x => x.TriggeredValue).HasColumnName("triggered_value").HasPrecision(18, 6);
        b.Property(x => x.OpenedAt).HasColumnName("opened_at");
        b.Property(x => x.AcknowledgedAt).HasColumnName("acknowledged_at");
        b.Property(x => x.ClosedAt).HasColumnName("closed_at");
        b.HasOne(x => x.SensorNode).WithMany().HasForeignKey(x => x.SensorNodeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SensorChannel).WithMany().HasForeignKey(x => x.SensorChannelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Gateway).WithMany().HasForeignKey(x => x.GatewayId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Uav).WithMany().HasForeignKey(x => x.UavId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Mission).WithMany().HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.AlertType, x.Status, x.SensorChannelId });
        b.HasIndex(x => new { x.Status, x.OpenedAt });
    }
}
