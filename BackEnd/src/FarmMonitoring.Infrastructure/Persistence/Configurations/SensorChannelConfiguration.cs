using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class SensorChannelConfiguration : IEntityTypeConfiguration<SensorChannel>
{
    public void Configure(EntityTypeBuilder<SensorChannel> b)
    {
        b.ToTable("sensor_channels");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.SensorNodeId).HasColumnName("sensor_node_id");
        b.Property(x => x.SensorTypeId).HasColumnName("sensor_type_id");
        b.Property(x => x.ChannelCode).HasColumnName("channel_code").HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.SensorNodeId, x.ChannelCode }).IsUnique();
        b.HasIndex(x => x.SensorTypeId);
        b.HasOne(x => x.SensorNode).WithMany().HasForeignKey(x => x.SensorNodeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SensorType).WithMany().HasForeignKey(x => x.SensorTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
