using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class VnpayConfiguration : IEntityTypeConfiguration<VnpayTransaction>
{
    public void Configure(EntityTypeBuilder<VnpayTransaction> b)
    {
        b.ToTable("vnpay_transactions");
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => x.TxnRef);
        b.Property(x => x.TxnRef).HasMaxLength(100);
        b.Property(x => x.TmnCode).HasMaxLength(32);
        b.Property(x => x.PublicToken).HasMaxLength(64);
        b.HasIndex(x => x.PublicToken).IsUnique();
        b.Property(x => x.Status).HasMaxLength(32);
        b.HasOne<SiteVisitProposal>().WithMany(x => x.Transactions).HasForeignKey(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
