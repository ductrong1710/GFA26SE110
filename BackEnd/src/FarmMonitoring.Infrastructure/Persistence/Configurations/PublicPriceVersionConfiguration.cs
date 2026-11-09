using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace FarmMonitoring.Infrastructure.Persistence.Configurations;
public sealed class PublicPriceVersionConfiguration : IEntityTypeConfiguration<PublicPriceVersion>
{
    public void Configure(EntityTypeBuilder<PublicPriceVersion> b)
    {
        b.ToTable("public_price_versions"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.PublicContentId).HasColumnName("public_content_id");
        b.Property(x => x.Revision).HasColumnName("revision");
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
        b.Property(x => x.MonthlyRentVnd).HasColumnName("monthly_rent_vnd").HasPrecision(18,2);
        b.Property(x => x.ReplacementPriceVnd).HasColumnName("replacement_price_vnd").HasPrecision(18,2);
        b.Property(x => x.RecordedAt).HasColumnName("recorded_at");
        b.Property(x => x.Published).HasColumnName("published");
        b.HasOne<PublicContent>().WithMany().HasForeignKey(x => x.PublicContentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new {x.PublicContentId,x.Revision}).IsUnique();
    }
}
