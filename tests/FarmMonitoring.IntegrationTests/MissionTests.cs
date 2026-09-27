using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class MissionTests(ApiFactory factory)
{
    private async Task<HttpClient> Login(string email = "operator@example.com")
    {
        var client = factory.CreateApiClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<(int farm, int sensor, int uav, int gateway)> Setup()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm = new Farm { Name = "Mission farm", CreatedAt = DateTimeOffset.UtcNow };
        var node = new SensorNode { DeviceCode = Guid.NewGuid().ToString("N"), Name = "Mission sensor", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow,
            Zone = new Zone { Name = "Mission zone", Farm = farm, CreatedAt = DateTimeOffset.UtcNow } };
        var uav = new Uav { Code = Guid.NewGuid().ToString("N"), Name = "Mission UAV", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow };
        var gateway = new Gateway { Code = Guid.NewGuid().ToString("N"), Name = "Mission gateway", GatewayType = "ESP32", Uav = uav, Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow };
        db.SensorNodes.Add(node); db.Gateways.Add(gateway);
        await db.SaveChangesAsync();
        return (farm.Id, node.Id, uav.Id, gateway.Id);
    }

    private static object Plan((int farm, int sensor, int uav, int gateway) data) => new
    {
        name = "Morning collection", farmId = data.farm, uavId = data.uav, gatewayId = data.gateway,
        sensorNodeIds = new[] { data.sensor }, waypoints = new[] { new { sequenceNo = 1, latitude = 10.1m, longitude = 106.2m, altitudeM = 10m } }
    };
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    [Fact]
    public async Task Completion_requires_all_outcomes_and_reports_collected_failed_and_skipped_targets()
    {
        var setup = await Setup();
        using var op = await Login();
        var sensorIds = new List<int> { setup.sensor };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sensor = await db.SensorNodes.FindAsync(setup.sensor);
            for (var i = 0; i < 2; i++)
            {
                var node = new SensorNode { ZoneId = sensor!.ZoneId, DeviceCode = Guid.NewGuid().ToString("N"), Name = "Additional target", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow };
                db.SensorNodes.Add(node);
                await db.SaveChangesAsync();
                sensorIds.Add(node.Id);
            }
        }
        var created = await Data(await op.PostAsJsonAsync("/api/missions", new
        {
            name = "Mixed outcomes", farmId = setup.farm, uavId = setup.uav, gatewayId = setup.gateway, sensorNodeIds = sensorIds,
            waypoints = new[] { new { sequenceNo = 1, localX = 0, localY = 0 } }
        }));
        var id = created.GetProperty("id").GetInt32();
        var start = DateTimeOffset.UtcNow.AddHours(1);
        (await op.PostAsJsonAsync($"/api/missions/{id}/schedule", new { scheduledStartAt = start, scheduledEndAt = start.AddHours(1) })).EnsureSuccessStatusCode();
        (await op.PostAsJsonAsync($"/api/missions/{id}/start", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await op.PostAsJsonAsync($"/api/missions/{id}/complete", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await op.PatchAsJsonAsync($"/api/missions/{id}/status", new { status = "COMPLETED" })).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var targets = await db.MissionTargets.Where(x => x.MissionId == id).OrderBy(x => x.SequenceNo).ToArrayAsync();
            targets[0].Status = MissionTargetStatus.COLLECTED;
            targets[1].Status = MissionTargetStatus.FAILED;
            targets[2].Status = MissionTargetStatus.SKIPPED;
            await db.SaveChangesAsync();
        }
        var completed = await Data(await op.PostAsJsonAsync($"/api/missions/{id}/complete", new { operatorNotes = "All targets processed" }));
        Assert.Equal("COMPLETED", completed.GetProperty("status").GetString());
        var result = await Data(await op.GetAsync($"/api/missions/{id}/results"));
        Assert.Equal(3, result.GetProperty("totalTargets").GetInt32());
        foreach (var count in new[] { "successfulTargets", "failedTargets", "skippedTargets" }) Assert.Equal(1, result.GetProperty(count).GetInt32());
        Assert.Equal(0, result.GetProperty("pendingTargets").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await op.PatchAsJsonAsync($"/api/missions/{id}/status", new { status = "RUNNING" })).StatusCode);
    }

    [Fact]
    public async Task Updating_a_draft_replaces_plan_and_invalid_updates_roll_back()
    {
        var setup = await Setup();
        using var op = await Login();
        var id = (await Data(await op.PostAsJsonAsync("/api/missions", Plan(setup)))).GetProperty("id").GetInt32();
        var updated = await Data(await op.PutAsJsonAsync($"/api/missions/{id}", Plan(setup)));
        Assert.Single(updated.GetProperty("targets").EnumerateArray());
        Assert.Single(updated.GetProperty("waypoints").EnumerateArray());
        var other = await Setup();
        foreach (var invalid in new[] { setup with { farm = int.MaxValue }, setup with { uav = int.MaxValue }, setup with { gateway = int.MaxValue } })
            Assert.Equal(HttpStatusCode.NotFound, (await op.PutAsJsonAsync($"/api/missions/{id}", Plan(invalid))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await op.PutAsJsonAsync($"/api/missions/{id}", Plan(setup with { sensor = other.sensor }))).StatusCode);
        var persisted = await Data(await op.GetAsync($"/api/missions/{id}"));
        Assert.Equal(setup.sensor, persisted.GetProperty("targets")[0].GetProperty("sensorNodeId").GetInt32());
        var start = DateTimeOffset.UtcNow.AddHours(1);
        (await op.PostAsJsonAsync($"/api/missions/{id}/schedule", new { scheduledStartAt = start, scheduledEndAt = start.AddHours(1) })).EnsureSuccessStatusCode();
        var pending = await Data(await op.PatchAsJsonAsync($"/api/missions/{id}/status", new { status = "PENDING", note = "Replan" }));
        Assert.Equal(JsonValueKind.Null, pending.GetProperty("scheduledEndAt").ValueKind);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await op.PatchAsJsonAsync($"/api/missions/{id}/status", new { status = "SCHEDULED" })).StatusCode);
    }

    [Fact]
    public async Task Mission_plan_schedule_start_and_failure_preserve_logs_and_terminal_state()
    {
        var setup = await Setup();
        using var op = await Login();
        var created = await op.PostAsJsonAsync("/api/missions", Plan(setup));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var mission = await Data(created);
        var id = mission.GetProperty("id").GetInt32();
        Assert.Equal("PENDING", mission.GetProperty("status").GetString());
        Assert.Single(mission.GetProperty("targets").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await op.PostAsJsonAsync($"/api/missions/{id}/start", new { })).StatusCode);
        var start = DateTimeOffset.UtcNow.AddHours(1);
        var schedule = await Data(await op.PostAsJsonAsync($"/api/missions/{id}/schedule", new { scheduledStartAt = start, scheduledEndAt = start.AddHours(1) }));
        Assert.Equal("SCHEDULED", schedule.GetProperty("status").GetString());
        Assert.Equal("RUNNING", (await Data(await op.PostAsJsonAsync($"/api/missions/{id}/start", new { }))).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await op.PutAsJsonAsync($"/api/missions/{id}", Plan(setup))).StatusCode);
        var failed = await Data(await op.PostAsJsonAsync($"/api/missions/{id}/fail", new { failureReason = "Gateway communication lost", operatorNotes = "Landed manually" }));
        Assert.Equal("FAILED", failed.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, failed.GetProperty("completedAt").ValueKind);
        var failureAlerts = (await op.GetFromJsonAsync<JsonElement>($"/api/alerts?missionId={id}&alertType=MISSION_ERROR")).GetProperty("data");
        Assert.Single(failureAlerts.EnumerateArray());
        Assert.Equal("CRITICAL", failureAlerts[0].GetProperty("severity").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await op.PostAsJsonAsync($"/api/missions/{id}/cancel", new { note = "Too late" })).StatusCode);
        var logs = await op.GetFromJsonAsync<JsonElement>($"/api/missions/{id}/logs?pageSize=100");
        Assert.Equal(4, logs.GetProperty("data").GetArrayLength());
        Assert.Single((await op.GetFromJsonAsync<JsonElement>($"/api/missions/{id}/waypoints")).GetProperty("data").EnumerateArray());
        Assert.Equal(1, (await Data(await op.GetAsync($"/api/missions/{id}/results"))).GetProperty("totalTargets").GetInt32());
        var list = await op.GetFromJsonAsync<JsonElement>($"/api/missions?farmId={setup.farm}&status=FAILED");
        Assert.Single(list.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task Concurrent_overlapping_schedules_reserve_equipment_once()
    {
        var setup = await Setup();
        using var op = await Login();
        var ids = new List<int>();
        for (var i = 0; i < 2; i++) ids.Add((await Data(await op.PostAsJsonAsync("/api/missions", Plan(setup)))).GetProperty("id").GetInt32());
        var start = DateTimeOffset.UtcNow.AddHours(2);
        var outcomes = await Task.WhenAll(ids.Select(id => op.PostAsJsonAsync($"/api/missions/{id}/schedule", new { scheduledStartAt = start, scheduledEndAt = start.AddHours(1) })));
        Assert.Single(outcomes, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(outcomes, x => x.StatusCode == HttpStatusCode.Conflict);
        var successful = await Data(outcomes.Single(x => x.StatusCode == HttpStatusCode.OK));
        (await op.PostAsJsonAsync($"/api/missions/{successful.GetProperty("id").GetInt32()}/cancel", new { note = "Release reservation" })).EnsureSuccessStatusCode();
        var other = ids.Single(x => x != successful.GetProperty("id").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await op.PostAsJsonAsync($"/api/missions/{other}/schedule", new { scheduledStartAt = start, scheduledEndAt = start.AddHours(1) })).StatusCode);
    }

    [Fact]
    public async Task Planning_requires_correct_farm_and_ready_equipment_and_operator()
    {
        var setup = await Setup();
        var other = await Setup();
        using var op = await Login();
        var invalid = setup with { sensor = other.sensor };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await op.PostAsJsonAsync("/api/missions", Plan(invalid))).StatusCode);
        var draft = await Data(await op.PostAsJsonAsync("/api/missions", new { name = "Draft", farmId = setup.farm, sensorNodeIds = new[] { setup.sensor }, waypoints = Array.Empty<object>() }));
        var start = DateTimeOffset.UtcNow.AddHours(1);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await op.PostAsJsonAsync($"/api/missions/{draft.GetProperty("id").GetInt32()}/schedule", new { scheduledStartAt = start, scheduledEndAt = start.AddHours(1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await op.PostAsJsonAsync("/api/missions", new { name = "Duplicate", farmId = setup.farm, sensorNodeIds = new[] { setup.sensor, setup.sensor }, waypoints = Array.Empty<object>() })).StatusCode);
        using var admin = await Login("admin@example.com");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/missions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/missions", Plan(setup))).StatusCode);
    }
}
