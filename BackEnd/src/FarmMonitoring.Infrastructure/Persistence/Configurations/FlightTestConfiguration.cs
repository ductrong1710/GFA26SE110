using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmMonitoring.Infrastructure.Persistence.Configurations;

public sealed class FlightTestConfiguration : IEntityTypeConfiguration<FlightTest>
{
    public void Configure(EntityTypeBuilder<FlightTest> b)
    {
        b.ToTable("flight_tests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.GatewayId).HasColumnName("gateway_id");
        b.Property(x => x.UavId).HasColumnName("uav_id");
        b.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id");
        b.Property(x => x.RequestKey).HasColumnName("request_key").HasMaxLength(100).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        b.Property(x => x.ClaimedAt).HasColumnName("claimed_at");
        b.Property(x => x.ReportedAt).HasColumnName("reported_at");
        b.Property(x => x.GatewayConnected).HasColumnName("gateway_connected");
        b.Property(x => x.UavConnected).HasColumnName("uav_connected");
        b.Property(x => x.Landed).HasColumnName("landed");
        b.Property(x => x.TakeoffAttempted).HasColumnName("takeoff_attempted");
        b.Property(x => x.ObservedAltitudeCm).HasColumnName("observed_altitude_cm");
        b.Property(x => x.HeldMs).HasColumnName("held_ms");
        b.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(100);
        b.HasIndex(x => new {x.GatewayId,x.RequestKey}).IsUnique();
        b.HasIndex(x => new {x.GatewayId,x.Status});
        b.HasIndex(x => new {x.UavId,x.Status});
        b.HasOne<Gateway>().WithMany().HasForeignKey(x => x.GatewayId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Uav>().WithMany().HasForeignKey(x => x.UavId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
