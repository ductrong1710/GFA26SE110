using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class SiteVisitOfficeLocationConfiguration : IEntityTypeConfiguration<SiteVisitOfficeLocation>
{
    public void Configure(EntityTypeBuilder<SiteVisitOfficeLocation> b)
    {
        b.ToTable("site_visit_offices");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        b.Property(x => x.MapsUrl).HasColumnName("maps_url").HasMaxLength(2048).IsRequired();
    }
}
