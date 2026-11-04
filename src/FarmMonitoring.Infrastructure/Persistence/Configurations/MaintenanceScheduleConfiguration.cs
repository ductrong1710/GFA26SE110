using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class MaintenanceScheduleConfiguration : IEntityTypeConfiguration<MaintenanceSchedule>
{
    public void Configure(EntityTypeBuilder<MaintenanceSchedule> b)
    {
        b.ToTable("maintenance_schedules", t =>
        {
            t.HasCheckConstraint("ck_maintenance_schedule_scope", "(farm_id IS NOT NULL AND rental_device_assignment_id IS NULL AND device_type IS NULL) OR (farm_id IS NULL AND NOT enabled)");
            t.HasCheckConstraint("ck_maintenance_schedule_interval", "interval_days BETWEEN 1 AND 3650");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.FarmId).HasColumnName("farm_id");
        b.HasOne<Farm>().WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.RentalDeviceAssignmentId).HasColumnName("rental_device_assignment_id");
        b.Property(x => x.DeviceType).HasColumnName("device_type").HasMaxLength(20);
        b.Property(x => x.Policy).HasColumnName("policy").HasMaxLength(2000).IsRequired();
        b.Property(x => x.IntervalDays).HasColumnName("interval_days");
        b.Property(x => x.NextDueAt).HasColumnName("next_due_at");
        b.Property(x => x.TechnicianId).HasColumnName("technician_id");
        b.Property(x => x.Enabled).HasColumnName("enabled");
        b.Property(x => x.Revision).HasColumnName("revision");
        b.Property(x => x.CreatedById).HasColumnName("created_by_id");
        b.Property(x => x.UpdatedById).HasColumnName("updated_by_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasOne<RentalDeviceAssignment>().WithMany().HasForeignKey(x => x.RentalDeviceAssignmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.Enabled, x.NextDueAt });
    }
}

public sealed class MaintenanceOccurrenceConfiguration : IEntityTypeConfiguration<MaintenanceOccurrence>
{
    public void Configure(EntityTypeBuilder<MaintenanceOccurrence> b)
    {
        b.ToTable("maintenance_occurrences"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.ScheduleId).HasColumnName("schedule_id");
        b.Property(x => x.DueAt).HasColumnName("due_at");
        b.Property(x => x.RentalDeviceAssignmentId).HasColumnName("rental_device_assignment_id");
        b.Property(x => x.JobId).HasColumnName("job_id");
        b.Property(x => x.Policy).HasColumnName("policy").HasMaxLength(2000).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne<MaintenanceSchedule>().WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RentalDeviceAssignment>().WithMany().HasForeignKey(x => x.RentalDeviceAssignmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ServiceJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ScheduleId, x.DueAt, x.RentalDeviceAssignmentId }).IsUnique();
        b.HasIndex(x => x.JobId).IsUnique();
    }
}
