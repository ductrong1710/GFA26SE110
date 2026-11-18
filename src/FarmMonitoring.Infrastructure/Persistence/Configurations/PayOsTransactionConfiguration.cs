using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class PayOsTransactionConfiguration : IEntityTypeConfiguration<PayOsTransaction>
{
    public void Configure(EntityTypeBuilder<PayOsTransaction> b)
    {
        b.ToTable("payos_transactions");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.OrderCode).IsUnique();
        b.HasIndex(x => x.PublicStatusToken).IsUnique().HasFilter("\"PublicStatusToken\" IS NOT NULL");
        b.HasIndex(x => x.PaymentLinkId).IsUnique().HasFilter("\"PaymentLinkId\" IS NOT NULL");
        b.HasIndex(x => x.ProposalId);
        b.HasIndex(x => new { x.ProposalId, x.Status });
        b.Property(x => x.Status).HasMaxLength(32);
        b.Property(x => x.CheckoutUrl).HasMaxLength(2048);
        b.Property(x => x.QrCode).HasMaxLength(4096);
        b.Property(x => x.ProviderReference).HasMaxLength(128);
        b.Property(x => x.ProviderPaymentLinkId).HasMaxLength(128);
        b.Property(x => x.ProviderCode).HasMaxLength(32);
        b.Property(x => x.ProviderTransactionDateTime).HasMaxLength(64);
        b.HasOne(x => x.Proposal).WithMany(x => x.PayOsTransactions).HasForeignKey(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
