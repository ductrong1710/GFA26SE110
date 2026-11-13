using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class ConsultationConfiguration : IEntityTypeConfiguration<Consultation>
{
    public void Configure(EntityTypeBuilder<Consultation> b)
    {
        b.ToTable("consultations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.CustomerName).HasColumnName("customer_name").HasMaxLength(150).IsRequired();
        b.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(10);
        b.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(30).IsRequired();
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255);
        b.Property(x => x.FarmLocation).HasColumnName("farm_location").HasMaxLength(500).IsRequired();
        b.Property(x => x.FarmArea).HasColumnName("farm_area").HasPrecision(14, 4);
        b.Property(x => x.ServiceNeed).HasColumnName("service_need").HasMaxLength(500).IsRequired();
        b.Property(x => x.Content).HasColumnName("content").HasMaxLength(4000).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.EngineerId).HasColumnName("engineer_id");
        b.Property(x => x.OwnerId).HasColumnName("owner_id");
        b.Property(x => x.FarmId).HasColumnName("farm_id");
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Farm).WithMany().HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OwnerId, x.FarmId });
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasOne(x => x.Engineer).WithMany().HasForeignKey(x => x.EngineerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EngineerId, x.Status, x.CreatedAt });
    }
}

public sealed class ConsultationHistoryConfiguration : IEntityTypeConfiguration<ConsultationHistory>
{
    public void Configure(EntityTypeBuilder<ConsultationHistory> b)
    {
        b.ToTable("consultation_history");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.ConsultationId).HasColumnName("consultation_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(4000);
        b.Property(x => x.ContactResult).HasColumnName("contact_result").HasMaxLength(1000);
        b.Property(x => x.NextAction).HasColumnName("next_action").HasMaxLength(1000);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.Consultation).WithMany(x => x.History).HasForeignKey(x => x.ConsultationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ConsultationId, x.Id });
    }
}
