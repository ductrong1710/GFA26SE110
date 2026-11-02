using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Application.Features.TechnicalSupport;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;
[Collection("PostgreSQL Auth")]
public sealed class MaintenanceScheduleApiTests(ApiFactory factory)
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
    public async Task Admin_manages_schedule_with_current_roles_validation_and_revision_conflicts()
    {
        using var admin = await Login("admin@example.com"); using var owner = await Login("owner@example.com");
        using var visitor = factory.CreateApiClient();
        const string path = "/api/maintenance-schedules";
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(path)).StatusCode);
        var email = $"schedule-tech-{Guid.NewGuid():N}@example.com";
        var tech = await Data(await admin.PostAsJsonAsync("/api/users", new { email, fullName = "Schedule technician", password = "Test-password-123!", roles = new[] { "Technician" } }));
        using var technician = await Login(email);
        var request = new MaintenanceScheduleRequest(null, "UAV", "Quarterly service", 90, DateTimeOffset.UtcNow.AddDays(1), tech.GetProperty("id").GetInt32(), true, 0);
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.PostAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(path, request with { IntervalDays = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(path, request with { TechnicianId = int.MaxValue })).StatusCode);
        var created = await Data(await admin.PostAsJsonAsync(path, request));
        var route = $"{path}/{created.GetProperty("id").GetInt32()}";
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(route + "/occurrences")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(route + "/occurrences")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(route + "/occurrences?pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(path + "/2147483647/occurrences")).StatusCode);
        Assert.Equal(1, created.GetProperty("revision").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PutAsJsonAsync(route, request with { ExpectedRevision = 1 })).StatusCode);
        var replies = await Task.WhenAll(admin.PutAsJsonAsync(route, request with { ExpectedRevision = 1, Policy = "First edit" }), admin.PutAsJsonAsync(route, request with { ExpectedRevision = 1, Policy = "Second edit" }));
        Assert.Single(replies, x => x.IsSuccessStatusCode); Assert.Single(replies, x => x.StatusCode == HttpStatusCode.Conflict);
        var updated = await Data(await admin.GetAsync(route)); Assert.Equal(2, updated.GetProperty("revision").GetInt32());
        var disabled = await Data(await admin.PostAsJsonAsync(route + "/disable", new { expectedRevision = 2 }));
        Assert.False(disabled.GetProperty("enabled").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(route + "/disable", new { expectedRevision = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(path + "?pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(path + "/2147483647")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(path)).StatusCode);
        var techId = tech.GetProperty("id").GetInt32();
        (await admin.PutAsJsonAsync($"/api/users/{techId}/roles", new { roles = new[] { "FarmAdministrator", "Technician" } })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await technician.GetAsync(route)).StatusCode);
        (await admin.PutAsJsonAsync($"/api/users/{techId}/roles", new { roles = new[] { "Technician" } })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.GetAsync(route)).StatusCode);
        (await admin.PatchAsJsonAsync($"/api/users/{techId}/status", new { isActive = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PutAsJsonAsync(route, request with { ExpectedRevision = 3 })).StatusCode);
    }

    [Fact]
    public async Task Device_schedule_checks_assignment_and_current_owner_membership()
    {
        using var admin = await Login("admin@example.com");
        var email = $"device-schedule-{Guid.NewGuid():N}@example.com";
        var tech = await Data(await admin.PostAsJsonAsync("/api/users", new { email, fullName = "Device technician", password = "Test-password-123!", roles = new[] { "Technician" } }));
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var farm = new Farm { Name = "Maintenance scope", CreatedAt = now };
        var uav = new Uav { Code = Guid.NewGuid().ToString("N"), Name = "Scheduled UAV", CreatedAt = now };
        var gateway = new Gateway { Code = Guid.NewGuid().ToString("N"), Name = "Scheduled gateway", GatewayType = "ESP32", CreatedAt = now };
        db.AddRange(farm, uav, gateway); await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, "owner@example.com"); await factory.SeedRentalAsync(farm.Id, uav.Id, gateway.Id);
        var assignment = await db.RentalDeviceAssignments.Include(x => x.Contract).SingleAsync(x => x.UavId == uav.Id);
        const string path = "/api/maintenance-schedules";
        var request = new MaintenanceScheduleRequest(assignment.Id, null, "Inspect rented UAV", 30, now.AddDays(1), tech.GetProperty("id").GetInt32(), true, 0);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(path, request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync(path, request with { RentalDeviceAssignmentId = int.MaxValue })).StatusCode);
        assignment.ReturnedAt = now; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(path, request)).StatusCode);
        assignment.ReturnedAt = null; await db.SaveChangesAsync();
        await db.UserFarms.Where(x => x.FarmId == farm.Id).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(path, request)).StatusCode);
    }
}
