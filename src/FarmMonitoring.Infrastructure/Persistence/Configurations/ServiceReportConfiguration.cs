using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;
public sealed class ServiceReportConfiguration : IEntityTypeConfiguration<ServiceReport>
{
    public void Configure(EntityTypeBuilder<ServiceReport> b)
    {
        b.ToTable("service_reports"); b.HasKey(x => x.Id);
        b.Property(x => x.ReplacementId).HasColumnName("replacement_id");
        b.HasOne(x => x.Replacement).WithMany().HasForeignKey(x => x.ReplacementId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.JobId).HasColumnName("job_id"); b.Property(x => x.Revision).HasColumnName("revision");
        b.Property(x => x.TechnicianId).HasColumnName("technician_id"); b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(4000).IsRequired(); b.Property(x => x.Cause).HasColumnName("cause").HasMaxLength(4000).IsRequired();
        b.Property(x => x.Work).HasColumnName("work").HasMaxLength(4000).IsRequired();
        b.Property(x => x.Components).HasColumnName("components").HasMaxLength(4000).IsRequired();
        b.Property(x => x.TestResults).HasColumnName("test_results").HasMaxLength(2000).IsRequired();
        b.Property(x => x.ConnectivityPassed).HasColumnName("connectivity_passed");
        b.Property(x => x.OperationPassed).HasColumnName("operation_passed");
        b.Property(x => x.ConfirmedByUserId).HasColumnName("confirmed_by_user_id"); b.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
        b.Property(x => x.ConfirmationNote).HasColumnName("confirmation_note").HasMaxLength(2000);
        b.Property(x => x.ReviewedByUserId).HasColumnName("reviewed_by_user_id"); b.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
        b.Property(x => x.ReviewNote).HasColumnName("review_note").HasMaxLength(2000); b.Property(x => x.Approved).HasColumnName("approved");
        b.HasOne(x => x.Job).WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.JobId, x.Revision }).IsUnique();
    }
}

