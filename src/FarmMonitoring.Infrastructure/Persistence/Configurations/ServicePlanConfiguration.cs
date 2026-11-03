using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;
public sealed class ServicePlanConfiguration : IEntityTypeConfiguration<ServicePlan>
{
    public void Configure(EntityTypeBuilder<ServicePlan> b)
    {
        b.ToTable("service_plans"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.JobId).HasColumnName("job_id");
        b.Property(x => x.OldAssignmentId).HasColumnName("old_assignment_id");
        b.Property(x => x.SensorNodeId).HasColumnName("sensor_node_id");
        b.Property(x => x.UavId).HasColumnName("uav_id");
        b.Property(x => x.GatewayId).HasColumnName("gateway_id");
        b.Property(x => x.TechnicianId).HasColumnName("technician_id");
        b.Property(x => x.ReviewerId).HasColumnName("reviewer_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
        b.Property(x => x.Note).HasColumnName("note").HasMaxLength(2000);
        b.Property(x => x.ReviewNote).HasColumnName("review_note").HasMaxLength(2000);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.PriceLinesJson).HasColumnName("price_lines_json").HasColumnType("jsonb");
        b.Property(x => x.EvidenceJson).HasColumnName("evidence_json").HasColumnType("jsonb");
        b.Property(x => x.TotalVnd).HasColumnName("total_vnd").HasPrecision(18,2);
        b.HasOne(x => x.Job).WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RentalDeviceAssignment>().WithMany().HasForeignKey(x => x.OldAssignmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SensorNode>().WithMany().HasForeignKey(x => x.SensorNodeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Uav>().WithMany().HasForeignKey(x => x.UavId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Gateway>().WithMany().HasForeignKey(x => x.GatewayId).OnDelete(DeleteBehavior.Restrict);
        const string active = "status IN ('PROPOSED','APPROVED','EXECUTED')";
        b.HasIndex(x => x.JobId).IsUnique().HasFilter(active);
        b.HasIndex(x => x.SensorNodeId).IsUnique().HasFilter(active + " AND sensor_node_id IS NOT NULL");
        b.HasIndex(x => x.UavId).IsUnique().HasFilter(active + " AND uav_id IS NOT NULL");
        b.HasIndex(x => x.GatewayId).IsUnique().HasFilter(active + " AND gateway_id IS NOT NULL");
    }
}
