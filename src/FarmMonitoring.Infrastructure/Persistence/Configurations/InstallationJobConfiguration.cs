using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class InstallationJobConfiguration : IEntityTypeConfiguration<InstallationJob>
{
    public void Configure(EntityTypeBuilder<InstallationJob> b)
    {
        b.ToTable("installation_jobs"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.InstallationRequestId).HasColumnName("installation_request_id");
        b.Property(x => x.TechnicianId).HasColumnName("technician_id");
        b.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Revision).HasColumnName("revision");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.InstallationRequest).WithMany().HasForeignKey(x => x.InstallationRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.InstallationRequestId).IsUnique();
        b.HasIndex(x => new { x.TechnicianId, x.Status, x.ScheduledAt });
    }
}
public sealed class InstallationJobHistoryConfiguration : IEntityTypeConfiguration<InstallationJobHistory>
{
    public void Configure(EntityTypeBuilder<InstallationJobHistory> b)
    {
        b.ToTable("installation_job_history"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.JobId).HasColumnName("job_id");
        b.Property(x => x.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.TechnicianId).HasColumnName("technician_id");
        b.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
        b.Property(x => x.Note).HasColumnName("note").HasMaxLength(4000).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.Job).WithMany(x => x.History).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
    }
}
