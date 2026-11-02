using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;
[Collection("PostgreSQL Auth")]
public class ServiceReplacementTests(ApiFactory factory)
{
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }
    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateApiClient();
        var data = await Data(await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        return client;
    }
    [Fact]
    public async Task Maintenance_without_incident_supports_replacement_report_and_acceptance()
    {
        using var admin = await Login("admin@example.com"); using var owner = await Login("owner@example.com");
        var email = $"scheduled-tech-{Guid.NewGuid():N}@example.com";
        var tech = await Data(await admin.PostAsJsonAsync("/api/users", new { email, fullName = "Maintenance technician", password = "Test-password-123!", roles = new[] { "Technician" } }));
        using var technician = await Login(email);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm = new Farm { Name = "Preventive maintenance farm", CreatedAt = DateTimeOffset.UtcNow };
        var uav = new Uav { Code = Guid.NewGuid().ToString("N"), Name = "Maintenance UAV", CreatedAt = DateTimeOffset.UtcNow };
        var next = new Uav { Code = Guid.NewGuid().ToString("N"), Name = "Maintenance replacement", CreatedAt = DateTimeOffset.UtcNow };
        var gateway = new Gateway { Code = Guid.NewGuid().ToString("N"), Name = "Maintenance gateway", GatewayType = "ESP32", CreatedAt = DateTimeOffset.UtcNow };
        db.Farms.Add(farm); db.Uavs.AddRange(uav, next); db.Gateways.Add(gateway); await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, "owner@example.com"); await factory.SeedRentalAsync(farm.Id, uav.Id, gateway.Id);
        var assignment = await db.RentalDeviceAssignments.SingleAsync(x => x.UavId == uav.Id);
        var job = new ServiceJob { RentalDeviceAssignmentId = assignment.Id, Type = ServiceJobType.MAINTENANCE,
            TechnicianId = tech.GetProperty("id").GetInt32(), ScheduledAt = DateTimeOffset.UtcNow.AddDays(1), CreatedAt = DateTimeOffset.UtcNow };
        db.ServiceJobs.Add(job); await db.SaveChangesAsync();
        var path = $"/api/service-jobs/{job.Id}";
        var detail = await Data(await owner.GetAsync(path));
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("incidentId").ValueKind);
        Assert.Empty(detail.GetProperty("images").EnumerateArray());
        (await technician.PostAsJsonAsync(path + "/start", new { expectedRevision = 1 })).EnsureSuccessStatusCode();
        var replaced = await Data(await technician.PostAsJsonAsync(path + "/replacements", new { expectedRevision = 2, uavId = next.Id, oldDeviceRetrieved = true, note = "Worn unit replaced during periodic inspection" }));
        Assert.Equal(next.Code, replaced.GetProperty("currentDeviceCode").GetString());
        var report = await Data(await technician.PostAsJsonAsync(path + "/reports", new { expectedRevision = 3, cause = "Periodic inspection", work = "Preventive replacement", components = "UAV", testResults = "All checks pass", notes = "Ready for operation", connectivityPassed = true, operationPassed = true }));
        var reportId = report.GetProperty("id").GetInt32();
        (await owner.PostAsJsonAsync(path + "/confirm-operation", new { expectedRevision = 4, reportId, note = "Operation checked" })).EnsureSuccessStatusCode();
        Assert.Equal("CLOSED", (await Data(await admin.PostAsJsonAsync(path + "/approve", new { expectedRevision = 5, reportId, note = "Maintenance accepted" }))).GetProperty("status").GetString());
        Assert.False(await db.DeviceIncidents.AnyAsync(x => x.FarmId == farm.Id));
        var ownerId = (await Data(await owner.GetAsync("/api/auth/me"))).GetProperty("id").GetInt32();
        (await admin.DeleteAsync($"/api/farms/{farm.Id}/members/{ownerId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(path)).StatusCode);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Gateway_and_sensor_replacements_enforce_kind_and_sensor_farm(bool sensorReplacement)
    {
        using var admin = await Login("admin@example.com"); using var owner = await Login("owner@example.com");
        var email = $"replace-kind-{Guid.NewGuid():N}@example.com";
        var tech = await Data(await admin.PostAsJsonAsync("/api/users", new { email, fullName = "Device technician", password = "Test-password-123!", roles = new[] { "Technician" } }));
        using var technician = await Login(email);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm = new Farm { Name = "Typed replacement farm", CreatedAt = DateTimeOffset.UtcNow };
        var otherFarm = new Farm { Name = "Other replacement farm", CreatedAt = DateTimeOffset.UtcNow };
        var uav = new Uav { Code = Guid.NewGuid().ToString("N"), Name = "Typed fixture UAV", CreatedAt = DateTimeOffset.UtcNow };
        var oldGateway = new Gateway { Code = Guid.NewGuid().ToString("N"), Name = "Old gateway", GatewayType = "ESP32", CreatedAt = DateTimeOffset.UtcNow };
        var newGateway = new Gateway { Code = Guid.NewGuid().ToString("N"), Name = "New gateway", GatewayType = "ESP32", CreatedAt = DateTimeOffset.UtcNow };
        db.Farms.AddRange(farm, otherFarm); db.Uavs.Add(uav); db.Gateways.AddRange(oldGateway, newGateway); await db.SaveChangesAsync();
        var zone = new Zone { FarmId = farm.Id, Name = "Replacement zone", CreatedAt = DateTimeOffset.UtcNow };
        var otherZone = new Zone { FarmId = otherFarm.Id, Name = "Outside zone", CreatedAt = DateTimeOffset.UtcNow };
        db.Zones.AddRange(zone, otherZone); await db.SaveChangesAsync();
        var sensors = new[] { zone.Id, zone.Id, otherZone.Id }.Select(z => new SensorNode { ZoneId = z, DeviceCode = Guid.NewGuid().ToString("N"), Name = "Replacement sensor", CreatedAt = DateTimeOffset.UtcNow }).ToArray();
        db.SensorNodes.AddRange(sensors); await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, "owner@example.com"); await factory.SeedRentalAsync(farm.Id, uav.Id, oldGateway.Id);
        var old = await db.RentalDeviceAssignments.SingleAsync(x => x.GatewayId == oldGateway.Id);
        if (sensorReplacement)
        {
            old.GatewayId = null; old.SensorNodeId = sensors[0].Id; old.Code = sensors[0].DeviceCode;
            var allocation = await db.InstallationDeviceAllocations.SingleAsync(x => x.Id == old.InstallationAllocationId);
            allocation.GatewayId = null; allocation.SensorNodeId = sensors[0].Id; allocation.Code = sensors[0].DeviceCode;
            await db.SaveChangesAsync();
        }
        var incident = await Data(await owner.PostAsJsonAsync("/api/device-incidents", new { rentalDeviceAssignmentId = old.Id, description = "Replace faulty device" }));
        var job = await Data(await admin.PostAsJsonAsync($"/api/device-incidents/{incident.GetProperty("id").GetInt32()}/jobs", new { type = "REPAIR", technicianId = tech.GetProperty("id").GetInt32(), scheduledAt = DateTimeOffset.UtcNow.AddDays(1), note = "Perform exchange" }));
        var path = $"/api/service-jobs/{job.GetProperty("id").GetInt32()}";
        (await technician.PostAsJsonAsync(path + "/start", new { expectedRevision = 1 })).EnsureSuccessStatusCode();
        var targetCode = sensorReplacement ? sensors[1].DeviceCode : newGateway.Code;
        var options = await Data(await technician.GetAsync(path + $"/replacement-options?search={targetCode}"));
        Assert.Single(options.EnumerateArray()); Assert.Equal(targetCode, options[0].GetProperty("code").GetString());
        var request = new { expectedRevision = 2, sensorNodeId = sensorReplacement ? (int?)sensors[1].Id : null, uavId = (int?)null, gatewayId = sensorReplacement ? null : (int?)newGateway.Id, oldDeviceRetrieved = true, note = "Retrieved old device" };
        if (sensorReplacement)
        {
            Assert.Empty((await Data(await technician.GetAsync(path + $"/replacement-options?search={sensors[2].DeviceCode}"))).EnumerateArray());
            Assert.Equal(HttpStatusCode.Conflict, (await technician.PostAsJsonAsync(path + "/replacements", request with { sensorNodeId = sensors[2].Id })).StatusCode);
        }
        var result = await Data(await technician.PostAsJsonAsync(path + "/replacements", request));
        Assert.Equal(targetCode, result.GetProperty("currentDeviceCode").GetString());
        Assert.NotNull(await db.RentalDeviceAssignments.Where(x => x.Id == old.Id).Select(x => x.ReturnedAt).SingleAsync());
        Assert.False(sensorReplacement ? await db.SensorNodes.Where(x => x.Id == sensors[0].Id).Select(x => x.IsActive).SingleAsync() : await db.Gateways.Where(x => x.Id == oldGateway.Id).Select(x => x.IsActive).SingleAsync());
    }
    [Fact]
    public async Task Replacements_preserve_lineage_report_snapshots_rental_history_and_reservations()
    {
        using var admin = await Login("admin@example.com"); using var owner = await Login("owner@example.com");
        using var engineer = await Login("engineer@example.com");
        var email = $"replace-{Guid.NewGuid():N}@example.com";
        var tech = await Data(await admin.PostAsJsonAsync("/api/users", new { email, fullName = "Replacement technician", password = "Test-password-123!", roles = new[] { "Technician" } }));
        using var technician = await Login(email);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm = new Farm { Name = "Replacement farm", CreatedAt = DateTimeOffset.UtcNow };
        var devices = Enumerable.Range(0, 3).Select(i => new Uav { Code = $"replacement-{Guid.NewGuid():N}", Name = $"Replacement UAV {i}", Status = "READY", CreatedAt = DateTimeOffset.UtcNow }).ToArray();
        var gateway = new Gateway { Code = Guid.NewGuid().ToString("N"), Name = "Replacement gateway", GatewayType = "ESP32", CreatedAt = DateTimeOffset.UtcNow };
        db.Farms.Add(farm); db.Uavs.AddRange(devices); db.Gateways.Add(gateway); await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, "owner@example.com"); await factory.SeedRentalAsync(farm.Id, devices[0].Id, gateway.Id);
        var old = await db.RentalDeviceAssignments.AsNoTracking().SingleAsync(x => x.UavId == devices[0].Id);
        var ownerId = (await Data(await owner.GetAsync("/api/auth/me"))).GetProperty("id").GetInt32();
        var incident = await Data(await owner.PostAsJsonAsync("/api/device-incidents", new { rentalDeviceAssignmentId = old.Id, description = "UAV motor failure" }));
        var job = await Data(await admin.PostAsJsonAsync($"/api/device-incidents/{incident.GetProperty("id").GetInt32()}/jobs", new { type = "REPAIR", technicianId = tech.GetProperty("id").GetInt32(), scheduledAt = DateTimeOffset.UtcNow.AddDays(1), note = "Replace failed equipment" }));
        var path = $"/api/service-jobs/{job.GetProperty("id").GetInt32()}";
        (await technician.PostAsJsonAsync(path + "/start", new { expectedRevision = 1 })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(path + "/replacement-options")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await engineer.GetAsync(path + "/replacement-options")).StatusCode);
        var options = await Data(await technician.GetAsync(path + $"/replacement-options?search={devices[1].Code}"));
        Assert.Equal(devices[1].Id, options[0].GetProperty("uavId").GetInt32());
        var request = new { expectedRevision = 2, sensorNodeId = (int?)null, uavId = (int?)devices[1].Id, gatewayId = (int?)null, oldDeviceRetrieved = true, note = "Retrieved failed motor unit" };
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync(path + "/replacements", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await technician.PostAsJsonAsync(path + "/replacements", request with { oldDeviceRetrieved = false })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await technician.PostAsJsonAsync(path + "/replacements", request with { uavId = null, gatewayId = gateway.Id })).StatusCode);
        var mission = new Mission { FarmId = farm.Id, UavId = devices[0].Id, GatewayId = gateway.Id, Name = "Active flight", Status = MissionStatus.RUNNING, CreatedByUserId = ownerId, CreatedAt = DateTimeOffset.UtcNow };
        db.Missions.Add(mission); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await technician.PostAsJsonAsync(path + "/replacements", request)).StatusCode);
        await db.Missions.Where(x => x.Id == mission.Id).ExecuteUpdateAsync(x => x.SetProperty(m => m.Status, MissionStatus.FAILED));
        var flight = new FlightTest { GatewayId = gateway.Id, UavId = devices[1].Id, RequestedByUserId = ownerId, RequestKey = Guid.NewGuid().ToString("N"), Status = FlightTestStatus.RECOVERY_REQUIRED, CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        db.FlightTests.Add(flight); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await technician.PostAsJsonAsync(path + "/replacements", request)).StatusCode);
        await db.FlightTests.Where(x => x.Id == flight.Id).ExecuteUpdateAsync(x => x.SetProperty(f => f.Status, FlightTestStatus.COMPLETED));
        var attempts = await Task.WhenAll(technician.PostAsJsonAsync(path + "/replacements", request), technician.PostAsJsonAsync(path + "/replacements", request));
        Assert.Single(attempts, x => x.IsSuccessStatusCode); Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.Conflict);
        var changed = await Data(attempts.Single(x => x.IsSuccessStatusCode));
        Assert.Equal(devices[1].Code, changed.GetProperty("currentDeviceCode").GetString());
        Assert.Equal(1, changed.GetProperty("replacements").GetArrayLength());
        var returnedAt = await db.RentalDeviceAssignments.Where(x => x.Id == old.Id).Select(x => x.ReturnedAt).SingleAsync(); Assert.NotNull(returnedAt);
        Assert.False(await db.Uavs.Where(x => x.Id == devices[0].Id).Select(x => x.IsActive).SingleAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/uavs/{devices[0].Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/uavs/{devices[1].Id}")).StatusCode);
        var report = await Data(await technician.PostAsJsonAsync(path + "/reports", new { expectedRevision = 3, cause = "Motor failure", work = "Replaced UAV", components = "Replacement UAV", testResults = "Needs calibration", notes = "Return for tuning", connectivityPassed = true, operationPassed = false }));
        var reportId = report.GetProperty("id").GetInt32();
        Assert.Equal(devices[1].Code, report.GetProperty("replacement").GetProperty("newDeviceCode").GetString());
        (await admin.PostAsJsonAsync(path + "/return", new { expectedRevision = 4, reportId, note = "Use another available unit" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await technician.PostAsJsonAsync(path + "/replacements", request with { expectedRevision = 5 })).StatusCode);
        await db.RentalContracts.Where(x => x.Id == old.ContractId).ExecuteUpdateAsync(x => x.SetProperty(r => r.Status, RentalStatus.EXPIRED));
        var second = await Data(await technician.PostAsJsonAsync(path + "/replacements", request with { expectedRevision = 5, uavId = devices[2].Id }));
        Assert.Equal(2, second.GetProperty("replacements").GetArrayLength());
        Assert.Equal(devices[2].Code, second.GetProperty("currentDeviceCode").GetString());
        Assert.Equal(devices[1].Code, (await Data(await owner.GetAsync(path + $"/reports/{reportId}"))).GetProperty("replacement").GetProperty("newDeviceCode").GetString());
        Assert.Equal(old.Id, (await Data(await owner.GetAsync($"/api/device-incidents/{incident.GetProperty("id").GetInt32()}"))).GetProperty("rentalDeviceAssignmentId").GetInt32());
        var rentalPath = $"/api/rental-contracts/{old.ContractId}";
        var rental = await Data(await admin.GetAsync(rentalPath));
        var extended = await Data(await admin.PostAsJsonAsync(rentalPath + "/extend", new { expectedRevision = rental.GetProperty("revision").GetInt32(), endAt = DateTimeOffset.UtcNow.AddYears(2), note = "Confirmed renewal after replacement" }));
        Assert.Equal("ACTIVE", extended.GetProperty("status").GetString());
        await db.Missions.Where(x => x.Id == mission.Id).ExecuteUpdateAsync(x => x.SetProperty(m => m.Status, MissionStatus.RUNNING).SetProperty(m => m.GatewayId, (int?)null));
        (await admin.PostAsJsonAsync(rentalPath + "/end", new { expectedRevision = extended.GetProperty("revision").GetInt32(), devicesReturned = true, note = "Retrieved remaining equipment" })).EnsureSuccessStatusCode();
        Assert.Equal(returnedAt, await db.RentalDeviceAssignments.Where(x => x.Id == old.Id).Select(x => x.ReturnedAt).SingleAsync());
        Assert.Equal(old.UavId, (await db.RentalDeviceAssignments.AsNoTracking().SingleAsync(x => x.Id == old.Id)).UavId);
        Assert.Equal(HttpStatusCode.Conflict, (await technician.PostAsJsonAsync(path + "/replacements", request with { expectedRevision = 6 })).StatusCode);
    }
}
