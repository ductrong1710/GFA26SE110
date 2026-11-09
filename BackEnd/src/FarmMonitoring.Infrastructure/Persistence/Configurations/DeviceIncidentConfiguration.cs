using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;
public sealed class DeviceIncidentConfiguration : IEntityTypeConfiguration<DeviceIncident>
{
    public void Configure(EntityTypeBuilder<DeviceIncident> b)
    {
        b.ToTable("device_incidents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.FarmId).HasColumnName("farm_id");
        b.Property(x => x.RentalDeviceAssignmentId).HasColumnName("rental_device_assignment_id");
        b.Property(x => x.DeviceCode).HasColumnName("device_code").HasMaxLength(150).IsRequired();
        b.Property(x => x.ReporterId).HasColumnName("reporter_id");
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000).IsRequired();
        b.Property(x => x.ReportedAt).HasColumnName("reported_at");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.MissionId).HasColumnName("mission_id");
        b.HasOne<Farm>().WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RentalDeviceAssignment).WithMany().HasForeignKey(x => x.RentalDeviceAssignmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Mission>().WithMany().HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.FarmId, x.Id });
    }
}
