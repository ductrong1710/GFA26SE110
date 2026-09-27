using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class SyncBatchConfiguration : IEntityTypeConfiguration<SyncBatch>
{
    public void Configure(EntityTypeBuilder<SyncBatch> b)
    {
        b.ToTable("sync_batches", t => t.HasCheckConstraint("CK_sync_batches_counts", "record_count >= 0 AND accepted_count >= 0 AND duplicate_count >= 0 AND rejected_count >= 0 AND record_count = accepted_count + duplicate_count + rejected_count"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.GatewayId).HasColumnName("gateway_id");
        b.Property(x => x.MissionId).HasColumnName("mission_id");
        b.Property(x => x.BatchKey).HasColumnName("batch_key").HasMaxLength(150).IsRequired();
        b.Property(x => x.RecordCount).HasColumnName("record_count");
        b.Property(x => x.AcceptedCount).HasColumnName("accepted_count");
        b.Property(x => x.DuplicateCount).HasColumnName("duplicate_count");
        b.Property(x => x.RejectedCount).HasColumnName("rejected_count");
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.Property(x => x.ErrorMessage).HasColumnName("error_message").HasColumnType("text");
        b.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64).IsRequired();
        b.Property(x => x.ResponseJson).HasColumnName("response_json").HasColumnType("jsonb").IsRequired();
        b.HasOne(x => x.Gateway).WithMany().HasForeignKey(x => x.GatewayId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Mission).WithMany().HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.GatewayId, x.BatchKey }).IsUnique();
    }
}
