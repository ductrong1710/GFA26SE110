using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.AlertId).HasColumnName("alert_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Channel).HasColumnName("channel").HasConversion<string>().HasMaxLength(50).IsRequired();
        b.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(255);
        b.Property(x => x.Message).HasColumnName("message").HasColumnType("text").IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50).IsRequired();
        b.Property(x => x.SentAt).HasColumnName("sent_at");
        b.Property(x => x.ErrorMessage).HasColumnName("error_message").HasColumnType("text");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.Alert).WithMany().HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UserId, x.Status, x.CreatedAt });
        b.HasIndex(x => new { x.AlertId, x.UserId, x.Channel }).IsUnique();
    }
}
