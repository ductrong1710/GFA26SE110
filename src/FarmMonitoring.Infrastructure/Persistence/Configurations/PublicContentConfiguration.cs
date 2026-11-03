using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class PublicContentConfiguration : IEntityTypeConfiguration<PublicContent>
{
    public void Configure(EntityTypeBuilder<PublicContent> b)
    {
        b.ToTable("public_content", t =>
        {
            t.HasCheckConstraint("ck_public_content_rent", "estimated_monthly_rent_vnd IS NULL OR (kind = 'DEVICE' AND estimated_monthly_rent_vnd >= 0)");
            t.HasCheckConstraint("ck_public_content_image", "(image IS NULL AND image_type IS NULL) OR (image IS NOT NULL AND image_type IS NOT NULL AND image_type IN ('image/png', 'image/jpeg') AND octet_length(image) BETWEEN 1 AND 2097152)");
            t.HasCheckConstraint("ck_public_content_revision", "revision > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
        b.Property(x => x.Body).HasColumnName("body").HasMaxLength(4000);
        b.Property(x => x.EstimatedMonthlyRentVnd).HasColumnName("estimated_monthly_rent_vnd").HasPrecision(18, 2);
        b.Property(x => x.ImageType).HasColumnName("image_type").HasMaxLength(20);
        b.Property(x => x.Image).HasColumnName("image");
        b.Property(x => x.Published).HasColumnName("published");
        b.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        b.HasIndex(x => new { x.Published, x.Id });
    }
}
