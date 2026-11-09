using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class CollectionAttemptConfiguration : IEntityTypeConfiguration<CollectionAttempt>
{
    public void Configure(EntityTypeBuilder<CollectionAttempt> b)
    {
        b.ToTable("collection_attempts", t => t.HasCheckConstraint("CK_collection_attempts_values", "attempt_no > 0 AND records_received >= 0 AND finished_at >= started_at"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.MissionId).HasColumnName("mission_id");
        b.Property(x => x.MissionTargetId).HasColumnName("mission_target_id");
        b.Property(x => x.GatewayId).HasColumnName("gateway_id");
        b.Property(x => x.AttemptNo).HasColumnName("attempt_no");
        b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.FinishedAt).HasColumnName("finished_at");
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        b.Property(x => x.RecordsReceived).HasColumnName("records_received");
        b.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(100);
        b.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(500);
        b.HasOne(x => x.Mission).WithMany().HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.MissionTarget).WithMany().HasForeignKey(x => x.MissionTargetId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Gateway).WithMany().HasForeignKey(x => x.GatewayId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.MissionTargetId, x.AttemptNo }).IsUnique();
    }
}
