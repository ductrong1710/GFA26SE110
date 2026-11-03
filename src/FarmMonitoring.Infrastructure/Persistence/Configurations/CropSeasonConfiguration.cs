using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;
public sealed class CropSeasonConfiguration : IEntityTypeConfiguration<CropSeason>
{
    public void Configure(EntityTypeBuilder<CropSeason> b)
    {
        b.ToTable("crop_seasons", t =>
        {
            t.HasCheckConstraint("ck_crop_season_values", "(area_hectares IS NULL OR area_hectares > 0) AND (crop_age_days IS NULL OR crop_age_days >= 0) AND (yield_kg IS NULL OR yield_kg >= 0) AND (total_cost_vnd IS NULL OR total_cost_vnd >= 0) AND (revenue_vnd IS NULL OR revenue_vnd >= 0) AND revision > 0");
            t.HasCheckConstraint("ck_crop_season_dates", "(start_date IS NULL OR expected_end_date IS NULL OR expected_end_date >= start_date) AND (start_date IS NULL OR end_date IS NULL OR end_date >= start_date)");
            t.HasCheckConstraint("ck_crop_season_status", "(status = 'ACTIVE' AND completed_at IS NULL) OR (status = 'COMPLETED' AND completed_at IS NOT NULL)");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.FarmId).HasColumnName("farm_id");
        b.Property(x => x.ZoneId).HasColumnName("zone_id");
        b.Property(x => x.OwnerId).HasColumnName("owner_id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(x => x.Crop).HasColumnName("crop").HasMaxLength(200).IsRequired();
        b.Property(x => x.AreaHectares).HasColumnName("area_hectares").HasPrecision(18, 4);
        b.Property(x => x.StartDate).HasColumnName("start_date");
        b.Property(x => x.ExpectedEndDate).HasColumnName("expected_end_date");
        b.Property(x => x.CropAgeDays).HasColumnName("crop_age_days");
        b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(4000);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Revision).HasColumnName("revision");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.Property(x => x.EndDate).HasColumnName("end_date");
        b.Property(x => x.YieldKg).HasColumnName("yield_kg").HasPrecision(18, 4);
        b.Property(x => x.TotalCostVnd).HasColumnName("total_cost_vnd").HasPrecision(18, 2);
        b.Property(x => x.RevenueVnd).HasColumnName("revenue_vnd").HasPrecision(18, 2);
        b.Property(x => x.CompletionNotes).HasColumnName("completion_notes").HasMaxLength(4000);
        b.HasOne<Farm>().WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Zone>().WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OwnerId, x.FarmId, x.Id });
    }
}
public sealed class CultivationActivityConfiguration : IEntityTypeConfiguration<CultivationActivity>
{
    public void Configure(EntityTypeBuilder<CultivationActivity> b)
    {
        b.ToTable("cultivation_activities", t =>
        {
            t.HasCheckConstraint("ck_cultivation_activity_cost", "cost_vnd IS NULL OR cost_vnd >= 0");
            t.HasCheckConstraint("ck_cultivation_activity_type", "type IN ('WATERING', 'FERTILIZING', 'PESTICIDE', 'INSPECTION', 'OTHER')");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.SeasonId).HasColumnName("season_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Date).HasColumnName("date");
        b.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000).IsRequired();
        b.Property(x => x.CostVnd).HasColumnName("cost_vnd").HasPrecision(18, 2);
        b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(4000);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.Season).WithMany(x => x.Activities).HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SeasonId, x.Date, x.Id });
    }
}
