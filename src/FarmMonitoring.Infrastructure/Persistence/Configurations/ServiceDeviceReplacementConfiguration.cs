using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FarmMonitoring.Infrastructure.Persistence.Configurations;
public sealed class ServiceDeviceReplacementConfiguration : IEntityTypeConfiguration<ServiceDeviceReplacement>
{
    public void Configure(EntityTypeBuilder<ServiceDeviceReplacement> b)
    {
        b.ToTable("service_device_replacements"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.JobId).HasColumnName("job_id");
        b.Property(x => x.OldAssignmentId).HasColumnName("old_assignment_id");
        b.Property(x => x.NewAssignmentId).HasColumnName("new_assignment_id");
        b.Property(x => x.OldDeviceCode).HasColumnName("old_device_code").HasMaxLength(150).IsRequired();
        b.Property(x => x.NewDeviceCode).HasColumnName("new_device_code").HasMaxLength(150).IsRequired();
        b.Property(x => x.TechnicianId).HasColumnName("technician_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.Note).HasColumnName("note").HasMaxLength(2000).IsRequired();
        b.HasOne(x => x.Job).WithMany(x => x.Replacements).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.OldAssignment).WithMany().HasForeignKey(x => x.OldAssignmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.NewAssignment).WithMany().HasForeignKey(x => x.NewAssignmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.OldAssignmentId).IsUnique(); b.HasIndex(x => x.NewAssignmentId).IsUnique();
        b.HasIndex(x => new { x.JobId, x.Id });
    }
}
