using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class ServiceJobConfiguration : IEntityTypeConfiguration<ServiceJob>
{
    public void Configure(EntityTypeBuilder<ServiceJob> b)
    {
        b.ToTable("service_jobs"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.IncidentId).HasColumnName("incident_id");
        b.Property(x => x.RentalDeviceAssignmentId).HasColumnName("rental_device_assignment_id");
        b.HasOne(x => x.RentalDeviceAssignment).WithMany().HasForeignKey(x => x.RentalDeviceAssignmentId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.TechnicianId).HasColumnName("technician_id");
        b.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
        b.Property(x => x.ScheduledEndAt).HasColumnName("scheduled_end_at");
        b.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Revision).HasColumnName("revision");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.Incident).WithMany().HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.IncidentId).IsUnique();
        b.HasIndex(x => new { x.TechnicianId, x.Status, x.ScheduledAt });
    }
}
public sealed class ServiceJobHistoryConfiguration : IEntityTypeConfiguration<ServiceJobHistory>
{
    public void Configure(EntityTypeBuilder<ServiceJobHistory> b)
    {
        b.ToTable("service_job_history"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.JobId).HasColumnName("job_id");
        b.Property(x => x.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.TechnicianId).HasColumnName("technician_id");
        b.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
        b.Property(x => x.ScheduledEndAt).HasColumnName("scheduled_end_at");
        b.Property(x => x.Note).HasColumnName("note").HasMaxLength(4000).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.Job).WithMany(x => x.History).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
    }
}

