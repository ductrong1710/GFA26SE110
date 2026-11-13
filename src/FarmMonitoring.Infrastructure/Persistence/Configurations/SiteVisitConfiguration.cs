using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class SiteVisitConfiguration : IEntityTypeConfiguration<SiteVisitProposal>, IEntityTypeConfiguration<SiteVisitPayment>, IEntityTypeConfiguration<SiteVisitBill>
{
    public void Configure(EntityTypeBuilder<SiteVisitProposal> b)
    {
        b.ToTable("site_visit_proposals");
        b.HasIndex(x => new { x.ConsultationId, x.Revision }).IsUnique();
        b.HasOne<Consultation>().WithMany().HasForeignKey(x => x.ConsultationId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.DistanceKm).HasPrecision(9, 2);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ScheduledBy).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<SiteVisitPayment> b)
    {
        b.ToTable("site_visit_payments");
        b.HasKey(x => x.ProposalId);
        b.HasOne<SiteVisitProposal>().WithOne(x => x.Payment).HasForeignKey<SiteVisitPayment>(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.VerifiedBy).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<SiteVisitBill> b)
    {
        b.ToTable("site_visit_bills");
        b.HasKey(x => x.ProposalId);
        b.Property(x => x.ImagesJson).HasColumnType("jsonb");
        b.HasOne<SiteVisitPayment>().WithOne(x => x.Bill).HasForeignKey<SiteVisitBill>(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict);
    }
}
