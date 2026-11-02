using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;
public sealed class InstallationReportConfiguration : IEntityTypeConfiguration<InstallationReport>
{
    public void Configure(EntityTypeBuilder<InstallationReport> b)
    {
        b.ToTable("installation_reports"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.JobId).HasColumnName("job_id"); b.Property(x => x.Revision).HasColumnName("revision");
        b.Property(x => x.TechnicianId).HasColumnName("technician_id"); b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(4000).IsRequired(); b.Property(x => x.Issues).HasColumnName("issues").HasMaxLength(4000);
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
public sealed class InstalledDeviceCheckConfiguration : IEntityTypeConfiguration<InstalledDeviceCheck>
{
    public void Configure(EntityTypeBuilder<InstalledDeviceCheck> b)
    {
        b.ToTable("installed_device_checks", t => t.HasCheckConstraint("CK_installed_device_identity", "num_nonnulls(sensor_node_id, uav_id, gateway_id) = 1")); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn(); b.Property(x => x.ReportId).HasColumnName("report_id");
        b.Property(x => x.ProposalLineId).HasColumnName("proposal_line_id"); b.Property(x => x.SensorNodeId).HasColumnName("sensor_node_id");
        b.Property(x => x.UavId).HasColumnName("uav_id"); b.Property(x => x.GatewayId).HasColumnName("gateway_id");
        b.Property(x => x.InstallationLocation).HasColumnName("installation_location").HasMaxLength(1000).IsRequired();
        b.Property(x => x.AccessoriesChecked).HasColumnName("accessories_checked"); b.Property(x => x.ConnectivityPassed).HasColumnName("connectivity_passed");
        b.Property(x => x.OperationPassed).HasColumnName("operation_passed"); b.Property(x => x.TestResults).HasColumnName("test_results").HasMaxLength(2000).IsRequired();
        b.HasOne(x => x.Report).WithMany(x => x.Devices).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProposalLine>().WithMany().HasForeignKey(x => x.ProposalLineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SensorNode>().WithMany().HasForeignKey(x => x.SensorNodeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Uav>().WithMany().HasForeignKey(x => x.UavId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Gateway>().WithMany().HasForeignKey(x => x.GatewayId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ReportId, x.SensorNodeId }).IsUnique(); b.HasIndex(x => new { x.ReportId, x.UavId }).IsUnique(); b.HasIndex(x => new { x.ReportId, x.GatewayId }).IsUnique();
    }
}
