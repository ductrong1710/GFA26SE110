using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

public class BoundaryMigrationTests
{
    [Fact]
    public async Task Upgrade_preserves_legacy_centers_and_adds_nullable_boundaries()
    {
        await using var factory = new ApiFactory { InitialMigration = "20261020023657_PasswordRecovery", SeedAccounts = false };
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO farms (id,name,latitude,longitude,is_active,created_at) VALUES (99,'Legacy',10.1234567,106.1234567,true,now());
            INSERT INTO zones (farm_id,name,center_latitude,center_longitude,created_at) VALUES (99,'Legacy zone',10.2,106.2,now());
            """);
        await db.GetService<IMigrator>().MigrateAsync();
        var farm = await db.Farms.SingleAsync(x => x.Id == 99);
        var zone = await db.Zones.SingleAsync(x => x.FarmId == 99);
        Assert.Null(farm.BoundaryGeoJson);
        Assert.Null(zone.BoundaryGeoJson);
        Assert.Equal(10.1234567m, farm.Latitude);
        Assert.Equal(106.1234567m, farm.Longitude);
        Assert.Equal(10.2m, zone.CenterLatitude);
        Assert.Equal(106.2m, zone.CenterLongitude);
    }
}
