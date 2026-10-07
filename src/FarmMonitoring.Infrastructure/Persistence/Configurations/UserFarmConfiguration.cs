using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class UserFarmConfiguration : IEntityTypeConfiguration<UserFarm>
{
    public void Configure(EntityTypeBuilder<UserFarm> b)
    {
        b.ToTable("user_farms");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.FarmId).HasColumnName("farm_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        b.HasIndex(x => new { x.UserId, x.FarmId }).IsUnique();
        b.HasIndex(x => x.FarmId);
        b.HasOne(x => x.User).WithMany(x => x.UserFarms).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Farm).WithMany(x => x.UserFarms).HasForeignKey(x => x.FarmId).OnDelete(DeleteBehavior.Restrict);
    }
}
