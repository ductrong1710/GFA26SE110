using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FarmMonitoring.Infrastructure.Persistence.Configurations;
public sealed class InstallationDeviceAllocationConfiguration : IEntityTypeConfiguration<InstallationDeviceAllocation>
{
    public void Configure(EntityTypeBuilder<InstallationDeviceAllocation> b)
    {
        b.ToTable("installation_device_allocations", t => t.HasCheckConstraint("CK_installation_allocation_identity", "num_nonnulls(sensor_node_id, uav_id, gateway_id) = 1")); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn(); b.Property(x => x.JobId).HasColumnName("job_id");
        b.Property(x => x.ProposalLineId).HasColumnName("proposal_line_id"); b.Property(x => x.SensorNodeId).HasColumnName("sensor_node_id");
        b.Property(x => x.UavId).HasColumnName("uav_id"); b.Property(x => x.GatewayId).HasColumnName("gateway_id");
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(150).IsRequired(); b.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        b.Property(x => x.AllocatedByUserId).HasColumnName("allocated_by_user_id"); b.Property(x => x.AllocatedAt).HasColumnName("allocated_at"); b.Property(x => x.ReleasedAt).HasColumnName("released_at");
        b.HasOne<InstallationJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProposalLine>().WithMany().HasForeignKey(x => x.ProposalLineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AllocatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SensorNode>().WithMany().HasForeignKey(x => x.SensorNodeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Uav>().WithMany().HasForeignKey(x => x.UavId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Gateway>().WithMany().HasForeignKey(x => x.GatewayId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.SensorNodeId).IsUnique().HasFilter("released_at IS NULL");
        b.HasIndex(x => x.UavId).IsUnique().HasFilter("released_at IS NULL");
        b.HasIndex(x => x.GatewayId).IsUnique().HasFilter("released_at IS NULL");
        b.HasIndex(x => new { x.JobId, x.ReleasedAt });
    }
}
