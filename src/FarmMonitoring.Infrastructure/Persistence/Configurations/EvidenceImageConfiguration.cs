using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class EvidenceImageConfiguration : IEntityTypeConfiguration<EvidenceImage>
{
    public void Configure(EntityTypeBuilder<EvidenceImage> builder)
    {
        builder.ToTable("evidence_images", t =>
        {
            t.HasCheckConstraint("ck_evidence_image_parent", "num_nonnulls(survey_id, report_id, mission_log_id, incident_id, service_report_id, crop_condition_report_id, crop_consultation_update_id, cultivation_activity_id) = 1");
            t.HasCheckConstraint("ck_evidence_image_size", "octet_length(content) BETWEEN 4 AND 2097152");
            t.HasCheckConstraint("ck_evidence_image_type", "content_type IN ('image/png', 'image/jpeg')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CultivationActivityId).HasColumnName("cultivation_activity_id");
        builder.HasOne<CultivationActivity>().WithMany(x => x.Images).HasForeignKey(x => x.CultivationActivityId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CropConditionReportId).HasColumnName("crop_condition_report_id");
        builder.Property(x => x.CropConsultationUpdateId).HasColumnName("crop_consultation_update_id");
        builder.HasOne<CropConditionReport>().WithMany(x => x.Images).HasForeignKey(x => x.CropConditionReportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CropConsultationUpdate>().WithMany(x => x.Images).HasForeignKey(x => x.CropConsultationUpdateId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.IncidentId).HasColumnName("incident_id");
        builder.Property(x => x.SurveyId).HasColumnName("survey_id");
        builder.Property(x => x.ReportId).HasColumnName("report_id");
        builder.Property(x => x.ServiceReportId).HasColumnName("service_report_id");
        builder.Property(x => x.MissionLogId).HasColumnName("mission_log_id");
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").IsRequired();
        builder.HasOne<FarmSurvey>().WithMany(x => x.Images).HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DeviceIncident>().WithMany(x => x.Images).HasForeignKey(x => x.IncidentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InstallationReport>().WithMany(x => x.Images).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceReport>().WithMany(x => x.Images).HasForeignKey(x => x.ServiceReportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MissionLog>().WithMany(x => x.Images).HasForeignKey(x => x.MissionLogId).OnDelete(DeleteBehavior.Restrict);
    }
}
