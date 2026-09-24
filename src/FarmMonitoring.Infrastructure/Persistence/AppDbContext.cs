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

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
