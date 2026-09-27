using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class MissionLogConfiguration : IEntityTypeConfiguration<MissionLog>
{
    public void Configure(EntityTypeBuilder<MissionLog> b)
    {
        b.ToTable("mission_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.MissionId).HasColumnName("mission_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.GatewayId).HasColumnName("gateway_id");
        b.Property(x => x.LogType).HasColumnName("log_type").HasMaxLength(50).IsRequired();
        b.Property(x => x.Message).HasColumnName("message").HasColumnType("text").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasOne(x => x.Mission).WithMany().HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Gateway).WithMany().HasForeignKey(x => x.GatewayId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.MissionId, x.CreatedAt });
    }
}
