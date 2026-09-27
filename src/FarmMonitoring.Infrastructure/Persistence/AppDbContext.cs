using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<SensorType> SensorTypes => Set<SensorType>();
    public DbSet<SensorNode> SensorNodes => Set<SensorNode>();
    public DbSet<SensorChannel> SensorChannels => Set<SensorChannel>();
    public DbSet<Uav> Uavs => Set<Uav>();
    public DbSet<Gateway> Gateways => Set<Gateway>();
    public DbSet<SensorThreshold> SensorThresholds => Set<SensorThreshold>();
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<MissionTarget> MissionTargets => Set<MissionTarget>();
    public DbSet<MissionWaypoint> MissionWaypoints => Set<MissionWaypoint>();
    public DbSet<MissionLog> MissionLogs => Set<MissionLog>();
    public DbSet<TelemetryRecord> TelemetryRecords => Set<TelemetryRecord>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
    public DbSet<SyncBatch> SyncBatches => Set<SyncBatch>();
    public DbSet<CollectionAttempt> CollectionAttempts => Set<CollectionAttempt>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertHistory> AlertHistories => Set<AlertHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
