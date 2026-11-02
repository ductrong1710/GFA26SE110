using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;
public sealed class RentalContractConfiguration : IEntityTypeConfiguration<RentalContract>
{
    public void Configure(EntityTypeBuilder<RentalContract> b)
    {
        b.ToTable("rental_contracts", t => t.HasCheckConstraint("ck_rental_dates", "end_at > start_at")); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn(); b.Property(x => x.InstallationJobId).HasColumnName("installation_job_id");
        b.Property(x => x.FarmId).HasColumnName("farm_id"); b.Property(x => x.OwnerId).HasColumnName("owner_id");
        b.Property(x => x.StartAt).HasColumnName("start_at"); b.Property(x => x.EndAt).HasColumnName("end_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.WarnedForEndAt).HasColumnName("warned_for_end_at");
        b.Property(x => x.Revision).HasColumnName("revision"); b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        b.HasOne(x => x.InstallationJob).WithMany().HasForeignKey(x => x.InstallationJobId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Farm>().WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.InstallationJobId).IsUnique(); b.HasIndex(x => new { x.Status, x.EndAt });
    }
}
public sealed class RentalDeviceAssignmentConfiguration : IEntityTypeConfiguration<RentalDeviceAssignment>
{
    public void Configure(EntityTypeBuilder<RentalDeviceAssignment> b)
    {
        b.ToTable("rental_device_assignments", t => t.HasCheckConstraint("ck_rental_device_identity", "num_nonnulls(sensor_node_id,uav_id,gateway_id) = 1")); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn(); b.Property(x => x.ContractId).HasColumnName("contract_id");
        b.Property(x => x.InstallationAllocationId).HasColumnName("installation_allocation_id"); b.Property(x => x.SensorNodeId).HasColumnName("sensor_node_id");
        b.Property(x => x.UavId).HasColumnName("uav_id"); b.Property(x => x.GatewayId).HasColumnName("gateway_id");
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(150).IsRequired(); b.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        b.Property(x => x.InstallationLocation).HasColumnName("installation_location").HasMaxLength(1000).IsRequired(); b.Property(x => x.ReturnedAt).HasColumnName("returned_at");
        b.HasOne(x => x.Contract).WithMany(x => x.Devices).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<InstallationDeviceAllocation>().WithMany().HasForeignKey(x => x.InstallationAllocationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SensorNode>().WithMany().HasForeignKey(x => x.SensorNodeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Uav>().WithMany().HasForeignKey(x => x.UavId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Gateway>().WithMany().HasForeignKey(x => x.GatewayId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.InstallationAllocationId).IsUnique();
        b.HasIndex(x => x.SensorNodeId).IsUnique().HasFilter("returned_at IS NULL");
        b.HasIndex(x => x.UavId).IsUnique().HasFilter("returned_at IS NULL");
        b.HasIndex(x => x.GatewayId).IsUnique().HasFilter("returned_at IS NULL");
    }
}
public sealed class RentalHistoryConfiguration : IEntityTypeConfiguration<RentalHistory>
{
    public void Configure(EntityTypeBuilder<RentalHistory> b)
    {
        b.ToTable("rental_history"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn(); b.Property(x => x.ContractId).HasColumnName("contract_id");
        b.Property(x => x.Action).HasColumnName("action").HasMaxLength(30).IsRequired(); b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Note).HasColumnName("note").HasMaxLength(2000).IsRequired(); b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.StartAt).HasColumnName("start_at"); b.Property(x => x.EndAt).HasColumnName("end_at");
        b.HasOne(x => x.Contract).WithMany(x => x.History).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
