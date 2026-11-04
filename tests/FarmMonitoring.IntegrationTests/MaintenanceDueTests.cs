using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;
[Collection("PostgreSQL Auth")]
public sealed class MaintenanceDueTests(ApiFactory factory)
{
    [Fact]
    public async Task Farm_schedule_covers_all_device_types_excludes_other_farms_and_is_transactional_and_idempotent()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var admin = await db.Users.SingleAsync(x => x.Email == "admin@example.com");
        var owner = await db.Users.SingleAsync(x => x.Email == "owner@example.com");
        var role = await db.Roles.SingleAsync(x => x.Name == "Technician");
        var tech = new User { Email = $"farm-maintenance-{Guid.NewGuid():N}@example.com", FullName = "Farm technician", PasswordHash = "unused", IsActive = true, CreatedAt = now };
        tech.UserRoles.Add(new UserRole { RoleId = role.Id });
        db.Users.Add(tech); await db.SaveChangesAsync();
        async Task<Farm> Seed(string name)
        {
            var farm = new Farm { Name = name, CreatedAt = now };
            var uav = new Uav { Code = Guid.NewGuid().ToString("N"), Name = name + " UAV", CreatedAt = now };
            var gateway = new Gateway { Code = Guid.NewGuid().ToString("N"), Name = name + " gateway", GatewayType = "ESP32", CreatedAt = now };
            db.AddRange(farm, uav, gateway); await db.SaveChangesAsync();
            await factory.AssignFarmAsync(farm.Id, owner.Email);
            await factory.SeedRentalAsync(farm.Id, uav.Id, gateway.Id);
            return farm;
        }
        var farm = await Seed("Selected farm");
        var other = await Seed("Other farm");
        var assignment = await db.RentalDeviceAssignments.Include(x => x.Contract).FirstAsync(x => x.Contract.FarmId == farm.Id);
        var allocation = await db.InstallationDeviceAllocations.SingleAsync(x => x.Id == assignment.InstallationAllocationId);
        var zone = new Zone { FarmId = farm.Id, Name = "Sensor zone" };
        var sensor = new SensorNode { Zone = zone, Name = "Farm sensor", DeviceCode = Guid.NewGuid().ToString("N"), Status = "OFFLINE", CreatedAt = now };
        db.SensorNodes.Add(sensor); await db.SaveChangesAsync();
        var sensorAllocation = new InstallationDeviceAllocation { JobId = allocation.JobId, ProposalLineId = allocation.ProposalLineId, SensorNodeId = sensor.Id, Code = sensor.DeviceCode, Name = sensor.Name, AllocatedByUserId = admin.Id, AllocatedAt = now };
        db.InstallationDeviceAllocations.Add(sensorAllocation); await db.SaveChangesAsync();
        db.RentalDeviceAssignments.Add(new RentalDeviceAssignment { ContractId = assignment.ContractId, InstallationAllocationId = sensorAllocation.Id, SensorNodeId = sensor.Id, Code = sensor.DeviceCode, Name = sensor.Name, InstallationLocation = "Field" });
        await db.SaveChangesAsync();
        var ids = await db.RentalDeviceAssignments.Where(x => x.Contract.FarmId == farm.Id).Select(x => x.Id).ToArrayAsync();
        var due = now.AddDays(-15);
        var schedule = new MaintenanceSchedule { FarmId = farm.Id, Policy = "All farm equipment", IntervalDays = 7, NextDueAt = due,
            TechnicianId = tech.Id, CreatedById = admin.Id, UpdatedById = admin.Id, CreatedAt = now, UpdatedAt = now };
        db.MaintenanceSchedules.Add(schedule); await db.SaveChangesAsync();
        async Task<int> Run()
        {
            await using var run = factory.Services.CreateAsyncScope();
            return await run.ServiceProvider.GetRequiredService<IMaintenanceScheduleRepository>().ProcessDueAsync(schedule.Id, now, default);
        }
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_reject_farm_maintenance() RETURNS trigger LANGUAGE plpgsql AS
            $$ BEGIN IF NEW.subject = 'Maintenance due' THEN RAISE EXCEPTION 'test failure'; END IF; RETURN NEW; END; $$;
            CREATE TRIGGER test_reject_farm_maintenance BEFORE INSERT ON notifications FOR EACH ROW EXECUTE FUNCTION test_reject_farm_maintenance();
            """);
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(Run);
            Assert.False(await db.MaintenanceOccurrences.AnyAsync(x => x.ScheduleId == schedule.Id));
            Assert.False(await db.ServiceJobs.AnyAsync(x => ids.Contains(x.RentalDeviceAssignmentId)));
            Assert.Equal(due, (await db.MaintenanceSchedules.AsNoTracking().SingleAsync(x => x.Id == schedule.Id)).NextDueAt);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_reject_farm_maintenance ON notifications; DROP FUNCTION test_reject_farm_maintenance();"); }
        Assert.Equal(3, (await Task.WhenAll(Run(), Run())).Sum());
        var occurrences = await db.MaintenanceOccurrences.Where(x => x.ScheduleId == schedule.Id).ToArrayAsync();
        Assert.Equal(ids.Order(), occurrences.Select(x => x.RentalDeviceAssignmentId).Order());
        var jobs = await db.ServiceJobs.Where(x => occurrences.Select(o => o.JobId).Contains(x.Id)).ToArrayAsync();
        Assert.All(jobs, job => { Assert.Equal(tech.Id, job.TechnicianId); Assert.Equal(ServiceJobType.MAINTENANCE, job.Type); });
        Assert.False(await db.ServiceJobs.AnyAsync(x => x.RentalDeviceAssignment.Contract.FarmId == other.Id));
        var refreshed = await db.MaintenanceSchedules.AsNoTracking().SingleAsync(x => x.Id == schedule.Id);
        Assert.Equal(due.AddDays(21), refreshed.NextDueAt);
        Assert.Equal(0, await Run());
        // On the next due pass, returned equipment is excluded.
        await db.RentalDeviceAssignments.Where(x => x.Id == assignment.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReturnedAt, now));
        var nextDue = due.AddDays(7);
        await db.MaintenanceSchedules.Where(x => x.Id == schedule.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.NextDueAt, nextDue));
        Assert.Equal(2, await Run());
        Assert.DoesNotContain(await db.MaintenanceOccurrences.Where(x => x.ScheduleId == schedule.Id && x.DueAt == nextDue).ToArrayAsync(), x => x.RentalDeviceAssignmentId == assignment.Id);
        await db.MaintenanceSchedules.Where(x => x.Id == schedule.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.NextDueAt, due).SetProperty(x => x.Enabled, false));
        Assert.Equal(0, await Run());
        await db.MaintenanceSchedules.Where(x => x.Id == schedule.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Enabled, true));
        await db.UserFarms.Where(x => x.FarmId == farm.Id).ExecuteDeleteAsync();
        Assert.Equal(0, await Run());
    }
}
