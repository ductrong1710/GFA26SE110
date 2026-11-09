using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class FarmSurveyConfiguration : IEntityTypeConfiguration<FarmSurvey>
{
    public void Configure(EntityTypeBuilder<FarmSurvey> b)
    {
        b.ToTable("farm_surveys");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.ConsultationId).HasColumnName("consultation_id");
        b.Property(x => x.Revision).HasColumnName("revision");
        b.Property(x => x.EngineerId).HasColumnName("engineer_id");
        b.Property(x => x.FarmName).HasColumnName("farm_name").HasMaxLength(150).IsRequired();
        b.Property(x => x.FarmLocation).HasColumnName("farm_location").HasMaxLength(500).IsRequired();
        b.Property(x => x.FarmArea).HasColumnName("farm_area").HasPrecision(14, 4);
        b.Property(x => x.MonitoringNeeds).HasColumnName("monitoring_needs").HasMaxLength(4000).IsRequired();
        b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(4000);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.Consultation).WithMany().HasForeignKey(x => x.ConsultationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Engineer).WithMany().HasForeignKey(x => x.EngineerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ConsultationId, x.Revision }).IsUnique();
    }
}

public sealed class SurveyZoneConfiguration : IEntityTypeConfiguration<SurveyZone>
{
    public void Configure(EntityTypeBuilder<SurveyZone> b)
    {
        b.ToTable("survey_zones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.SurveyId).HasColumnName("survey_id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        b.Property(x => x.Crop).HasColumnName("crop").HasMaxLength(150);
        b.Property(x => x.CropAgeDays).HasColumnName("crop_age_days");
        b.Property(x => x.Symptoms).HasColumnName("symptoms").HasMaxLength(2000);
        b.Property(x => x.InstallationLocation).HasColumnName("installation_location").HasMaxLength(1000).IsRequired();
        b.HasOne(x => x.Survey).WithMany(x => x.Zones).HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SurveyId, x.Name }).IsUnique();
    }
}
