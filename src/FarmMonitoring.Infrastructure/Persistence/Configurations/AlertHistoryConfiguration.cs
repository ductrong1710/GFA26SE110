using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class AlertHistoryConfiguration : IEntityTypeConfiguration<AlertHistory>
{
    public void Configure(EntityTypeBuilder<AlertHistory> b)
    {
        b.ToTable("alert_histories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.AlertId).HasColumnName("alert_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(50).IsRequired();
        b.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.Alert).WithMany(x => x.History).HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.AlertId, x.CreatedAt });
    }
}
