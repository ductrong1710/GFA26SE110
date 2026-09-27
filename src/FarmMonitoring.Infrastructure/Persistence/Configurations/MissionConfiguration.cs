using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class MissionConfiguration : IEntityTypeConfiguration<Mission>
{
    public void Configure(EntityTypeBuilder<Mission> b)
    {
        b.ToTable("missions", t =>
        {
            t.HasCheckConstraint("CK_missions_schedule", "(scheduled_start_at IS NULL AND scheduled_end_at IS NULL) OR (scheduled_start_at IS NOT NULL AND scheduled_end_at IS NOT NULL AND scheduled_end_at > scheduled_start_at)");
            t.HasCheckConstraint("CK_missions_status", "status IN ('PENDING','SCHEDULED','RUNNING','COMPLETED','FAILED','CANCELLED')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50).IsRequired();
        b.Property(x => x.FarmId).HasColumnName("farm_id");
        b.Property(x => x.UavId).HasColumnName("uav_id");
        b.Property(x => x.GatewayId).HasColumnName("gateway_id");
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        b.Property(x => x.ScheduledStartAt).HasColumnName("scheduled_start_at");
        b.Property(x => x.ScheduledEndAt).HasColumnName("scheduled_end_at");
        b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.OperatorNotes).HasColumnName("operator_notes").HasColumnType("text");
        b.Property(x => x.FailureReason).HasColumnName("failure_reason").HasColumnType("text");
        b.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Uav).WithMany().HasForeignKey(x => x.UavId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Gateway).WithMany().HasForeignKey(x => x.GatewayId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UavId, x.Status, x.ScheduledStartAt, x.ScheduledEndAt });
        b.HasIndex(x => new { x.GatewayId, x.Status, x.ScheduledStartAt, x.ScheduledEndAt });
        b.HasIndex(x => new { x.FarmId, x.Status });
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.ScheduledStartAt);
    }
}
