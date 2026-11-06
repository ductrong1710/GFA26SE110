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
    public async Task Concurrent_due_processing_creates_one_job_and_notifications_per_device_and_advances_anchor()
    {
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var admin = await db.Users.SingleAsync(x => x.Email == "admin@example.com");
        var owner = await db.Users.SingleAsync(x => x.Email == "owner@example.com");
        var techRole = await db.Roles.SingleAsync(x => x.Name == "Technician");
        var tech = new User { Email = $"due-tech-{Guid.NewGuid():N}@example.com", FullName = "Due technician", PasswordHash = "unused", IsActive = true, CreatedAt = now };
        db.Users.Add(tech); await db.SaveChangesAsync(); db.UserRoles.Add(new UserRole { UserId = tech.Id, RoleId = techRole.Id });
        var farm = new Farm { Name = "Due farm", CreatedAt = now };
        var uav = new Uav { Code = Guid.NewGuid().ToString("N"), Name = "Due UAV", CreatedAt = now };
        var gateway = new Gateway { Code = Guid.NewGuid().ToString("N"), Name = "Due gateway", GatewayType = "ESP32", CreatedAt = now };
        db.AddRange(farm, uav, gateway); await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, owner.Email); await factory.SeedRentalAsync(farm.Id, uav.Id, gateway.Id);
        var assignment = await db.RentalDeviceAssignments.SingleAsync(x => x.UavId == uav.Id);
        var due = now.AddDays(-15);
        var schedule = new MaintenanceSchedule { RentalDeviceAssignmentId = assignment.Id, Policy = "Weekly inspection", IntervalDays = 7, NextDueAt = due,
            TechnicianId = tech.Id, CreatedById = admin.Id, UpdatedById = admin.Id, CreatedAt = now, UpdatedAt = now };
        db.MaintenanceSchedules.Add(schedule); await db.SaveChangesAsync();
        async Task<int> Run()
        {
            await using var runScope = factory.Services.CreateAsyncScope();
            return await runScope.ServiceProvider.GetRequiredService<IMaintenanceScheduleRepository>().ProcessDueAsync(schedule.Id, now, default);
        }
        // Fail the notification write after job insertion in the disposable database.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_reject_maintenance_notification() RETURNS trigger LANGUAGE plpgsql AS
            $$ BEGIN IF NEW.subject = 'Maintenance due' THEN RAISE EXCEPTION 'maintenance-test-failure'; END IF; RETURN NEW; END; $$;
            CREATE TRIGGER test_reject_maintenance_notification BEFORE INSERT ON notifications
            FOR EACH ROW EXECUTE FUNCTION test_reject_maintenance_notification();
            """);
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(Run);
            Assert.False(await db.ServiceJobs.AnyAsync(x => x.RentalDeviceAssignmentId == assignment.Id));
            Assert.False(await db.MaintenanceOccurrences.AnyAsync(x => x.ScheduleId == schedule.Id));
            Assert.False(await db.Notifications.AnyAsync(x => x.UserId == tech.Id));
            var unchanged = await db.MaintenanceSchedules.AsNoTracking().SingleAsync(x => x.Id == schedule.Id);
            Assert.Equal(due, unchanged.NextDueAt); Assert.Equal(1, unchanged.Revision);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_reject_maintenance_notification ON notifications; DROP FUNCTION test_reject_maintenance_notification();");
        }
        var results = await Task.WhenAll(Run(), Run()); Assert.Equal(1, results.Sum());
        var occurrence = await db.MaintenanceOccurrences.AsNoTracking().SingleAsync(x => x.ScheduleId == schedule.Id);
        var job = await db.ServiceJobs.AsNoTracking().SingleAsync(x => x.Id == occurrence.JobId);
        Assert.Null(job.IncidentId); Assert.Equal(ServiceJobType.MAINTENANCE, job.Type); Assert.Equal(tech.Id, job.TechnicianId);
        Assert.Equal(due, occurrence.DueAt); Assert.Equal("Weekly inspection", occurrence.Policy);
        var refreshed = await db.MaintenanceSchedules.AsNoTracking().SingleAsync(x => x.Id == schedule.Id);
        Assert.Equal(due.AddDays(21), refreshed.NextDueAt); Assert.Equal(2, refreshed.Revision);
        Assert.Equal(2, await db.Notifications.CountAsync(x => x.UserId == tech.Id && x.Subject == "Maintenance due"));
        Assert.Equal(2, await db.Notifications.CountAsync(x => x.UserId == owner.Id && x.Subject == "Maintenance due" && x.Message == $"Scheduled maintenance is due for device {assignment.Code}. Job #{job.Id}: {schedule.Policy}"));
        Assert.Equal(0, await Run());
        await db.MaintenanceSchedules.Where(x => x.Id == schedule.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.NextDueAt, due).SetProperty(x => x.Enabled, false));
        Assert.Equal(0, await Run());
        await db.MaintenanceSchedules.Where(x => x.Id == schedule.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Enabled, true));
        tech.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(0, await Run());
        Assert.Equal(due, (await db.MaintenanceSchedules.AsNoTracking().SingleAsync(x => x.Id == schedule.Id)).NextDueAt);
        tech.IsActive = true; assignment.ReturnedAt = now; await db.SaveChangesAsync();
        Assert.Equal(0, await Run());
        await db.MaintenanceSchedules.Where(x => x.Id == schedule.Id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.RentalDeviceAssignmentId, (int?)null).SetProperty(x => x.DeviceType, "GATEWAY"));
        Assert.True(await Run() >= 1);
        var generated = await db.MaintenanceOccurrences.Where(x => x.ScheduleId == schedule.Id && x.JobId != job.Id)
            .Join(db.RentalDeviceAssignments, x => x.RentalDeviceAssignmentId, x => x.Id, (o, a) => a).ToArrayAsync();
        Assert.Contains(generated, x => x.GatewayId == gateway.Id);
        Assert.All(generated, x => Assert.NotNull(x.GatewayId));

        var successor = new Uav { Code = Guid.NewGuid().ToString("N"), Name = "Successor UAV", CreatedAt = now };
        db.Uavs.Add(successor); await db.SaveChangesAsync();
        var originalAllocation = await db.InstallationDeviceAllocations.SingleAsync(x => x.Id == assignment.InstallationAllocationId);
        originalAllocation.ReleasedAt = now;
        var newAllocation = new InstallationDeviceAllocation { JobId = originalAllocation.JobId, ProposalLineId = originalAllocation.ProposalLineId,
            UavId = successor.Id, Code = successor.Code, Name = successor.Name, AllocatedByUserId = admin.Id, AllocatedAt = now };
        db.InstallationDeviceAllocations.Add(newAllocation); await db.SaveChangesAsync();
        var newAssignment = new RentalDeviceAssignment { ContractId = assignment.ContractId, InstallationAllocationId = newAllocation.Id,
            UavId = successor.Id, Code = successor.Code, Name = successor.Name, InstallationLocation = assignment.InstallationLocation };
        db.RentalDeviceAssignments.Add(newAssignment); await db.SaveChangesAsync();
        db.ServiceDeviceReplacements.Add(new() { JobId = job.Id, OldAssignmentId = assignment.Id, NewAssignmentId = newAssignment.Id,
            OldDeviceCode = assignment.Code, NewDeviceCode = successor.Code, TechnicianId = tech.Id, CreatedAt = now, Note = "Replacement fixture" });
        await db.SaveChangesAsync();
        var nextDue = now.AddDays(-1);
        await db.MaintenanceSchedules.Where(x => x.Id == schedule.Id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.RentalDeviceAssignmentId, assignment.Id).SetProperty(x => x.DeviceType, (string?)null).SetProperty(x => x.NextDueAt, nextDue));
        Assert.Equal(1, await Run());
        var successorOccurrence = await db.MaintenanceOccurrences.AsNoTracking().SingleAsync(x => x.ScheduleId == schedule.Id && x.DueAt == nextDue);
        Assert.Equal(newAssignment.Id, successorOccurrence.RentalDeviceAssignmentId);
        Assert.Equal(newAssignment.Id, (await db.ServiceJobs.AsNoTracking().SingleAsync(x => x.Id == successorOccurrence.JobId)).RentalDeviceAssignmentId);
        Assert.Equal(assignment.Id, (await db.MaintenanceSchedules.AsNoTracking().SingleAsync(x => x.Id == schedule.Id)).RentalDeviceAssignmentId);
        Assert.Equal(assignment.Id, (await db.ServiceJobs.AsNoTracking().SingleAsync(x => x.Id == job.Id)).RentalDeviceAssignmentId);
        var ownerRole = await db.Roles.SingleAsync(x => x.Name == "FarmOwner");
        db.UserRoles.Add(new UserRole { UserId = tech.Id, RoleId = ownerRole.Id });
        db.UserFarms.Add(new UserFarm { UserId = tech.Id, FarmId = farm.Id });
        var contract = await db.RentalContracts.SingleAsync(x => x.Id == assignment.ContractId);
        contract.OwnerId = tech.Id; await db.SaveChangesAsync();
        await db.MaintenanceSchedules.Where(x => x.Id == schedule.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.NextDueAt, now.AddDays(-2)));
        var notificationCount = await db.Notifications.CountAsync(x => x.UserId == tech.Id && x.Subject == "Maintenance due");
        Assert.Equal(1, await Run());
        Assert.Equal(notificationCount + 2, await db.Notifications.CountAsync(x => x.UserId == tech.Id && x.Subject == "Maintenance due"));
        await db.MaintenanceSchedules.Where(x => x.Id == schedule.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Policy, "New future policy"));
        var repository = scope.ServiceProvider.GetRequiredService<IMaintenanceScheduleRepository>();
        var history = await repository.OccurrencesAsync(schedule.Id, admin.Id, new() { PageSize = 100 }, default);
        Assert.Equal("Weekly inspection", Assert.Single(history.Items, x => x.JobId == job.Id).Policy);
        Assert.Equal(assignment.Code, Assert.Single(history.Items, x => x.JobId == job.Id).DeviceCode);
        Assert.Contains(history.Items, x => x.JobId == successorOccurrence.JobId && x.RentalDeviceAssignmentId == newAssignment.Id);
        var firstPage = await repository.OccurrencesAsync(schedule.Id, admin.Id, new() { PageSize = 1 }, default);
        var secondPage = await repository.OccurrencesAsync(schedule.Id, admin.Id, new() { PageSize = 1, Page = 2 }, default);
        Assert.Single(firstPage.Items); Assert.Single(secondPage.Items);
        Assert.NotEqual(firstPage.Items[0].Id, secondPage.Items[0].Id);
        await using var editScope = factory.Services.CreateAsyncScope();
        var editor = editScope.ServiceProvider.GetRequiredService<IMaintenanceScheduleRepository>();
        var current = await editor.GetAsync(schedule.Id, admin.Id, default);
        var edited = await editor.SaveAsync(schedule.Id, admin.Id, new(assignment.Id, null, "Updated successor inspection", 30,
            now.AddDays(30), tech.Id, true, current.Revision), now, default);
        Assert.Equal(assignment.Id, edited.RentalDeviceAssignmentId);
        Assert.Equal("Updated successor inspection", edited.Policy);
        await db.RentalDeviceAssignments.Where(x => x.Id == newAssignment.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReturnedAt, now));
        await using var rejectedScope = factory.Services.CreateAsyncScope();
        var rejectedEditor = rejectedScope.ServiceProvider.GetRequiredService<IMaintenanceScheduleRepository>();
        await Assert.ThrowsAsync<FarmMonitoring.Application.Common.BusinessRuleException>(() => rejectedEditor.SaveAsync(schedule.Id, admin.Id,
            new(assignment.Id, null, "Must reject returned successor", 30, now.AddDays(30), tech.Id, true, edited.Revision), now, default));
    }
}
