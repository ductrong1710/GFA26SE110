using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;
public sealed class CropConditionReportConfiguration : IEntityTypeConfiguration<CropConditionReport>
{
    public void Configure(EntityTypeBuilder<CropConditionReport> b)
    {
        b.ToTable("crop_condition_reports"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.FarmId).HasColumnName("farm_id");
        b.Property(x => x.ZoneId).HasColumnName("zone_id");
        b.Property(x => x.CropSeasonId).HasColumnName("crop_season_id");
        b.Property(x => x.ObservedAt).HasColumnName("observed_at");
        b.HasOne<CropSeason>().WithMany().HasForeignKey(x => x.CropSeasonId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.ReporterId).HasColumnName("reporter_id");
        b.Property(x => x.EngineerId).HasColumnName("engineer_id");
        b.Property(x => x.Crop).HasColumnName("crop").HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000).IsRequired();
        b.Property(x => x.ReportedAt).HasColumnName("reported_at");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Revision).HasColumnName("revision");
        b.HasOne<Farm>().WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Zone>().WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.EngineerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EngineerId, x.Status, x.ReportedAt });
        b.HasIndex(x => new { x.FarmId, x.ReportedAt });
    }
}
public sealed class CropConsultationUpdateConfiguration : IEntityTypeConfiguration<CropConsultationUpdate>
{
    public void Configure(EntityTypeBuilder<CropConsultationUpdate> b)
    {
        b.ToTable("crop_consultation_updates"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.ReportId).HasColumnName("report_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Content).HasColumnName("content").HasMaxLength(4000).IsRequired();
        b.Property(x => x.RecommendSiteSurvey).HasColumnName("recommend_site_survey");
        b.Property(x => x.FollowUpType).HasColumnName("follow_up_type").HasMaxLength(30);
        b.Property(x => x.ReferralType).HasColumnName("referral_type").HasMaxLength(30);
        b.Property(x => x.CultivationActivityId).HasColumnName("cultivation_activity_id");
        b.Property(x => x.AssignedEngineerId).HasColumnName("assigned_engineer_id");
        b.Property(x => x.DeviceIncidentId).HasColumnName("device_incident_id");
        b.HasOne<CultivationActivity>().WithMany().HasForeignKey(x => x.CultivationActivityId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedEngineerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DeviceIncident>().WithMany().HasForeignKey(x => x.DeviceIncidentId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.Report).WithMany(x => x.Updates).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ReportId, x.Id });
    }
}
public sealed class CropConditionSensorConfiguration : IEntityTypeConfiguration<CropConditionSensor>
{
    public void Configure(EntityTypeBuilder<CropConditionSensor> b)
    {
        b.ToTable("crop_condition_sensors"); b.HasKey(x => new { x.ReportId, x.SensorChannelId });
        b.Property(x => x.ReportId).HasColumnName("report_id");
        b.Property(x => x.SensorChannelId).HasColumnName("sensor_channel_id");
        b.HasOne(x => x.Report).WithMany(x => x.Sensors).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SensorChannel>().WithMany().HasForeignKey(x => x.SensorChannelId).OnDelete(DeleteBehavior.Restrict);
    }
}
