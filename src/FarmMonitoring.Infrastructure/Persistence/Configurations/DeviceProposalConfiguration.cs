using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class DeviceProposalConfiguration : IEntityTypeConfiguration<DeviceProposal>
{
    public void Configure(EntityTypeBuilder<DeviceProposal> b)
    {
        b.ToTable("device_proposals"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.ConsultationId).HasColumnName("consultation_id");
        b.Property(x => x.SurveyId).HasColumnName("survey_id");
        b.Property(x => x.Revision).HasColumnName("revision");
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        b.Property(x => x.RentalMonths).HasColumnName("rental_months");
        b.Property(x => x.RequestedInstallationAt).HasColumnName("requested_installation_at");
        b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(4000);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.PlanConfirmedByUserId).HasColumnName("plan_confirmed_by_user_id");
        b.Property(x => x.PlanConfirmedAt).HasColumnName("plan_confirmed_at");
        b.Property(x => x.PlanConfirmationNote).HasColumnName("plan_confirmation_note").HasMaxLength(2000);
        b.Property(x => x.DepositConfirmedByUserId).HasColumnName("deposit_confirmed_by_user_id");
        b.Property(x => x.DepositConfirmedAt).HasColumnName("deposit_confirmed_at");
        b.Property(x => x.DepositConfirmationNote).HasColumnName("deposit_confirmation_note").HasMaxLength(2000);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.Consultation).WithMany().HasForeignKey(x => x.ConsultationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Survey).WithMany().HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.PlanConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.DepositConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ConsultationId, x.Revision }).IsUnique();
    }
}
public sealed class ProposalLineConfiguration : IEntityTypeConfiguration<ProposalLine>
{
    public void Configure(EntityTypeBuilder<ProposalLine> b)
    {
        b.ToTable("proposal_lines"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.ProposalId).HasColumnName("proposal_id");
        b.Property(x => x.SurveyZoneId).HasColumnName("survey_zone_id");
        b.Property(x => x.DeviceType).HasColumnName("device_type").HasMaxLength(150).IsRequired();
        b.Property(x => x.Quantity).HasColumnName("quantity");
        b.Property(x => x.PublicContentId).HasColumnName("public_content_id");
        b.Property(x => x.PriceRevision).HasColumnName("price_revision");
        b.Property(x => x.MonthlyRentVnd).HasColumnName("monthly_rent_vnd").HasPrecision(18,2);
        b.Property(x => x.ProductName).HasColumnName("product_name").HasMaxLength(200);
        b.HasOne<PublicContent>().WithMany().HasForeignKey(x => x.PublicContentId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.InstallationLocation).HasColumnName("installation_location").HasMaxLength(1000).IsRequired();
        b.HasOne(x => x.Proposal).WithMany(x => x.Lines).HasForeignKey(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SurveyZone).WithMany().HasForeignKey(x => x.SurveyZoneId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class InstallationRequestConfiguration : IEntityTypeConfiguration<InstallationRequest>
{
    public void Configure(EntityTypeBuilder<InstallationRequest> b)
    {
        b.ToTable("installation_requests"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.ProposalId).HasColumnName("proposal_id");
        b.Property(x => x.FarmId).HasColumnName("farm_id");
        b.Property(x => x.OwnerId).HasColumnName("owner_id");
        b.Property(x => x.SubmittedByUserId).HasColumnName("submitted_by_user_id");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
        b.Property(x => x.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
        b.Property(x => x.ReviewedAt).HasColumnName("reviewed_at");
        b.Property(x => x.ReviewNote).HasColumnName("review_note").HasMaxLength(2000);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Proposal).WithMany().HasForeignKey(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SubmittedByUser).WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ProposalId).IsUnique();
        b.HasIndex(x => new { x.Status, x.SubmittedAt });
    }
}
