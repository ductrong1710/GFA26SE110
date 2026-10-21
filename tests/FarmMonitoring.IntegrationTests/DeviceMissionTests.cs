using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FarmMonitoring.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace FarmMonitoring.IntegrationTests;

public sealed class DeviceMissionTests : IAsyncLifetime
{
    private readonly ApiFactory factory = new();
    private int gatewayId, otherGatewayId, farmId, sensorId, uavId;
    private HttpClient human = null!, device = null!;
    public async Task InitializeAsync()
    {
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm = new Farm { Name = "Device mission farm", CreatedAt = DateTimeOffset.UtcNow };
        var node = new SensorNode { DeviceCode = "SENSOR-001", Name = "Sensor", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow,
            Zone = new Zone { Name = "Zone", Farm = farm, CreatedAt = DateTimeOffset.UtcNow } };
        var uav = new Uav { Code = "mission-uav", Name = "UAV", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow };
        var gateway = new Gateway { Code = "integration-gateway", Name = "Gateway", GatewayType = "ESP32", Uav = uav, Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow };
        var other = new Gateway { Code = "sync-gateway-2", Name = "Other", GatewayType = "ESP32", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow };
        db.SensorChannels.Add(new SensorChannel { SensorNode = node, ChannelCode = "temperature", CreatedAt = DateTimeOffset.UtcNow,
            SensorType = new SensorType { Code = "temperature", Name = "Temperature" } });
        db.Gateways.AddRange(gateway, other);
        await db.SaveChangesAsync();
        gatewayId = gateway.Id; otherGatewayId = other.Id; farmId = farm.Id; sensorId = node.Id; uavId = uav.Id;
        await factory.SeedDeviceCredentialAsync(gatewayId);
        await factory.SeedDeviceCredentialAsync(otherGatewayId);
        human = factory.CreateApiClient();
        var login = await Data(await human.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "Test-password-123!" }));
        human.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        device = factory.CreateApiClient();
        device.DefaultRequestHeaders.Add("X-Gateway-Code", "integration-gateway");
        device.DefaultRequestHeaders.Add("X-Api-Key", factory.DeviceKey);
    }
    public async Task DisposeAsync() { human?.Dispose(); device?.Dispose(); await factory.DisposeAsync(); }
    private string Root => $"/api/device/gateways/{gatewayId}/missions";
    private object Plan(int waypointSequence = 2) => new {
        name = "Autonomous mission", farmId, gatewayId, uavId, sensorNodeIds = new[] { sensorId },
        targetAssignments = new[] { new { sensorNodeId = sensorId, waypointSequenceNo = waypointSequence, sequenceNo = 1 } },
        waypoints = new[] { new { sequenceNo = 2, localX = 0.5m, localY = 0m, altitudeM = 0.35m, actionType = "COLLECT", plannedHoldSeconds = 10 } }
    };
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {json}");
        Assert.True(json.GetProperty("success").GetBoolean());
        return json.GetProperty("data");
    }
    private async Task<int> Scheduled()
    {
        var created = await Data(await human.PostAsJsonAsync("/api/missions", Plan()));
        var id = created.GetProperty("id").GetInt32();
        var start = DateTimeOffset.UtcNow.AddHours(1);
        await Data(await human.PostAsJsonAsync($"/api/missions/{id}/schedule", new { scheduledStartAt = start, scheduledEndAt = start.AddHours(1) }));
        return id;
    }

    [Fact]
    public async Task Flight_test_reservation_and_mission_schedule_exclude_each_other()
    {
        var missionId=(await Data(await human.PostAsJsonAsync("/api/missions",Plan()))).GetProperty("id").GetInt32();
        var testId=(await Data(await human.PostAsJsonAsync($"/api/gateways/{gatewayId}/flight-tests",new {requestKey="mission-exclusion"}))).GetProperty("id").GetInt32();
        var start=DateTimeOffset.UtcNow.AddHours(1);
        var schedule=new {scheduledStartAt=start,scheduledEndAt=start.AddHours(1)};
        Assert.Equal(HttpStatusCode.Conflict,(await human.PostAsJsonAsync($"/api/missions/{missionId}/schedule",schedule)).StatusCode);
        await using(var scope=factory.Services.CreateAsyncScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.FlightTests.Where(x=>x.Id==testId).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.ExpiresAt,DateTimeOffset.UtcNow.AddSeconds(-1)));
        }
        await Data(await human.PostAsJsonAsync($"/api/missions/{missionId}/schedule",schedule));
        Assert.Equal(HttpStatusCode.Conflict,(await human.PostAsJsonAsync($"/api/gateways/{gatewayId}/flight-tests",new {requestKey="mission-busy"})).StatusCode);
    }

    [Fact]
    public async Task Tello_schedule_and_start_validate_profile_and_download_retry_survives_start()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var uav = (await db.Uavs.FindAsync(uavId))!;
        uav.Model = "DJI Tello EDU";
        await db.SaveChangesAsync();
        var plan = JsonSerializer.SerializeToNode(Plan())!.AsObject();
        var id = (await Data(await human.PostAsJsonAsync("/api/missions", plan))).GetProperty("id").GetInt32();
        var start = DateTimeOffset.UtcNow.AddHours(1);
        var schedule = new { scheduledStartAt = start, scheduledEndAt = start.AddHours(1) };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await human.PostAsJsonAsync($"/api/missions/{id}/schedule", schedule)).StatusCode);
        plan["waypoints"]![0]!["altitudeM"] = 1m;
        plan["waypoints"]![0]!["actionType"] = "COLLECT_SENSOR";
        await Data(await human.PutAsJsonAsync($"/api/missions/{id}", plan));
        await Data(await human.PostAsJsonAsync($"/api/missions/{id}/schedule", schedule));
        Assert.Equal(HttpStatusCode.Conflict, (await human.PostAsJsonAsync($"/api/missions/{id}/start", new { })).StatusCode);
        var pulled = await Data(await device.GetAsync(Root + "/next"));
        Assert.Equal("COLLECT", pulled.GetProperty("waypoints")[0].GetProperty("actionType").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await device.PostAsJsonAsync($"{Root}/{id}/start", new { })).StatusCode);
        await Data(await device.PostAsJsonAsync($"{Root}/{id}/downloaded", new { }));
        // Revalidate persisted plans at start, including plans created before these rules.
        var waypoint = await db.MissionWaypoints.SingleAsync(w => w.MissionId == id);
        waypoint.LocalX = .1m;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await device.PostAsJsonAsync($"{Root}/{id}/start", new { })).StatusCode);
        waypoint.LocalX = .5m;
        await db.SaveChangesAsync();
        var first = await Data(await device.PostAsJsonAsync($"{Root}/{id}/start", new { }));
        var retry = await Data(await device.PostAsJsonAsync($"{Root}/{id}/downloaded", new { }));
        Assert.Equal("RUNNING", retry.GetProperty("status").GetString());
        Assert.Equal(first.GetProperty("startedAt").GetString(), retry.GetProperty("startedAt").GetString());
        await Data(await device.PostAsJsonAsync($"{Root}/{id}/start", new { }));
        Assert.Equal(1, await db.MissionLogs.CountAsync(l => l.MissionId == id && l.LogType == "DOWNLOADED"));
        Assert.Equal(1, await db.MissionLogs.CountAsync(l => l.MissionId == id && l.LogType == "STARTED"));
        await Data(await device.PostAsJsonAsync($"{Root}/{id}/cancel", new { reason = "Done" }));
        Assert.Equal(HttpStatusCode.Conflict, (await device.PostAsJsonAsync($"{Root}/{id}/downloaded", new { })).StatusCode);
    }

    [Fact]
    public async Task Running_mission_without_download_log_cannot_create_late_download_ack()
    {
        var id = await Scheduled();
        await Data(await device.PostAsJsonAsync($"{Root}/{id}/start", new { }));
        Assert.Equal(HttpStatusCode.Conflict, (await device.PostAsJsonAsync($"{Root}/{id}/downloaded", new { })).StatusCode);
    }

    [Fact]
    public async Task Mapping_is_explicit_persisted_and_unknown_waypoint_is_rejected()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await human.PostAsJsonAsync("/api/missions", Plan(99))).StatusCode);
        var result = await Data(await human.PostAsJsonAsync("/api/missions", Plan()));
        var id = result.GetProperty("id").GetInt32();
        Assert.Equal(result.GetProperty("waypoints")[0].GetProperty("id").GetInt32(), result.GetProperty("targets")[0].GetProperty("waypointId").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await human.PutAsJsonAsync($"/api/missions/{id}", Plan(99))).StatusCode);
        var persisted = await Data(await human.GetAsync($"/api/missions/{id}"));
        Assert.Equal(result.GetProperty("targets")[0].GetProperty("waypointId").GetInt32(), persisted.GetProperty("targets")[0].GetProperty("waypointId").GetInt32());
    }

    [Fact]
    public async Task Device_pull_requires_device_credentials_and_returns_only_assigned_plan()
    {
        using var anonymous = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Root + "/next")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await human.GetAsync(Root + "/next")).StatusCode);
        anonymous.DefaultRequestHeaders.Add("X-Gateway-Code", "integration-gateway");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Root + "/next")).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await Data(await device.GetAsync(Root + "/next"))).ValueKind);
        var id = await Scheduled();
        Assert.Equal(HttpStatusCode.Forbidden, (await device.GetAsync($"/api/device/gateways/{otherGatewayId}/missions/next")).StatusCode);
        var plan = await Data(await device.GetAsync(Root + "/next"));
        Assert.Equal(id, plan.GetProperty("id").GetInt32());
        Assert.Equal("SENSOR-001", plan.GetProperty("targets")[0].GetProperty("deviceCode").GetString());
        Assert.Equal(2, plan.GetProperty("targets")[0].GetProperty("waypointSequenceNo").GetInt32());
        Assert.Equal(plan.GetProperty("waypoints")[0].GetProperty("id").GetInt32(), plan.GetProperty("targets")[0].GetProperty("waypointId").GetInt32());
        Assert.False(plan.TryGetProperty("apiKey", out _));
        using var other = factory.CreateApiClient();
        other.DefaultRequestHeaders.Add("X-Gateway-Code", "sync-gateway-2"); other.DefaultRequestHeaders.Add("X-Api-Key", factory.DeviceKey);
        Assert.Equal(JsonValueKind.Null, (await Data(await other.GetAsync($"/api/device/gateways/{otherGatewayId}/missions/next"))).ValueKind);
        foreach (var action in new[] { "downloaded", "start", "complete", "fail", "cancel" })
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync($"/api/device/gateways/{otherGatewayId}/missions/{id}/{action}", new { failureReason = "TIMEOUT", reason = "Abort" })).StatusCode);
    }

    [Fact]
    public async Task Download_start_sync_complete_are_idempotent_and_preserve_actor_and_outcomes()
    {
        var id = await Scheduled();
        foreach (var action in new[] { "downloaded", "start" })
        {
            var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => device.PostAsJsonAsync($"{Root}/{id}/{action}", new { })));
            foreach (var response in responses) await Data(response);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mission = await db.Missions.FindAsync(id);
            Assert.Equal(action == "downloaded" ? MissionStatus.SCHEDULED : MissionStatus.RUNNING, mission!.Status);
            var log = Assert.Single(await db.MissionLogs.Where(x => x.MissionId == id && x.LogType == (action == "downloaded" ? "DOWNLOADED" : "STARTED")).ToListAsync());
            Assert.Equal(gatewayId, log.GatewayId); Assert.Null(log.UserId);
            if (action == "start") Assert.NotNull(mission.StartedAt);
        }
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await device.PostAsJsonAsync($"{Root}/{id}/complete", new { })).StatusCode);
        var measured = DateTimeOffset.UtcNow.AddMinutes(-1);
        var sync = new { batchKey = "mission-batch", missionId = id,
            records = new[] { new { sensorNodeCode = "SENSOR-001", channelCode = "temperature", sourceRecordKey = "reading-1", value = 25, measuredAt = measured, collectedAt = measured.AddSeconds(1) } },
            collectionResults = new[] { new { sensorNodeId = sensorId, status = "SUCCESS", attemptNo = 1, recordsReceived = 1 } } };
        for (var i = 0; i < 2; i++) Assert.Equal(1, (await Data(await device.PostAsJsonAsync($"/api/device/gateways/{gatewayId}/sync", sync))).GetProperty("accepted").GetInt32());
        foreach (var response in await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => device.PostAsJsonAsync($"{Root}/{id}/complete", new { })))) await Data(response);
        await using var finalScope = factory.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var final = await finalDb.Missions.Include(x => x.Targets).SingleAsync(x => x.Id == id);
        Assert.Equal(MissionStatus.COMPLETED, final.Status); Assert.NotNull(final.CompletedAt);
        Assert.Equal(MissionTargetStatus.COLLECTED, Assert.Single(final.Targets).Status);
        Assert.Single(await finalDb.SensorReadings.Where(x => x.MissionId == id).ToListAsync());
        Assert.Single(await finalDb.CollectionAttempts.Where(x => x.MissionId == id).ToListAsync());
        Assert.Single(await finalDb.MissionLogs.Where(x => x.MissionId == id && x.LogType == "COMPLETED").ToListAsync());
    }

    [Fact]
    public async Task Failure_requires_reason_and_retry_does_not_duplicate_alert_or_rewrite_result()
    {
        var id = await Scheduled();
        Assert.Equal(HttpStatusCode.Conflict, (await device.PostAsJsonAsync($"{Root}/{id}/fail", new { failureReason = "TIMEOUT" })).StatusCode);
        await Data(await device.PostAsJsonAsync($"{Root}/{id}/start", new { }));
        Assert.Equal(HttpStatusCode.BadRequest, (await device.PostAsJsonAsync($"{Root}/{id}/fail", new { note = "Not a reason" })).StatusCode);
        var first = await Data(await device.PostAsJsonAsync($"{Root}/{id}/fail", new { failureReason = " SENSOR_TIMEOUT ", note = "Sensor not discovered" }));
        var retry = await Data(await device.PostAsJsonAsync($"{Root}/{id}/fail", new { failureReason = "SENSOR_TIMEOUT", note = "retry" }));
        Assert.Equal("FAILED", retry.GetProperty("status").GetString());
        Assert.Equal("SENSOR_TIMEOUT", retry.GetProperty("failureReason").GetString());
        Assert.Equal(first.GetProperty("completedAt").GetString(), retry.GetProperty("completedAt").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await device.PostAsJsonAsync($"{Root}/{id}/fail", new { failureReason = "DIFFERENT" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await device.PostAsJsonAsync($"{Root}/{id}/complete", new { })).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = Assert.Single(await db.MissionLogs.Where(x => x.MissionId == id && x.LogType == "FAILED").ToListAsync());
        Assert.Null(log.UserId); Assert.Equal(gatewayId, log.GatewayId);
        var alert = Assert.Single(await db.Alerts.Where(x => x.MissionId == id && x.AlertType == AlertType.MISSION_ERROR).ToListAsync());
        Assert.Equal(AlertSeverity.CRITICAL, alert.Severity);
    }

    [Fact]
    public async Task Concurrent_web_cancel_and_device_start_cannot_resurrect_cancelled_mission()
    {
        var id = await Scheduled();
        var responses = await Task.WhenAll(human.PostAsJsonAsync($"/api/missions/{id}/cancel", new { reason = "Web cancelled" }),
            device.PostAsJsonAsync($"{Root}/{id}/start", new { }));
        Assert.Equal(HttpStatusCode.OK, responses[0].StatusCode);
        Assert.Contains(responses[1].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        Assert.Equal(HttpStatusCode.Conflict, (await device.PostAsJsonAsync($"{Root}/{id}/start", new { })).StatusCode);
        await Data(await device.PostAsJsonAsync($"{Root}/{id}/cancel", new { reason = "Safe abort finished" }));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(MissionStatus.CANCELLED, (await db.Missions.FindAsync(id))!.Status);
        Assert.Single(await db.MissionLogs.Where(x => x.MissionId == id && x.LogType == "CANCELLED").ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Device_cancel_preserves_existing_scheduled_and_running_semantics(bool running)
    {
        var id = await Scheduled();
        if (running) await Data(await device.PostAsJsonAsync($"{Root}/{id}/start", new { }));
        for (var i = 0; i < 2; i++)
            Assert.Equal("CANCELLED", (await Data(await device.PostAsJsonAsync($"{Root}/{id}/cancel", new { reason = "Operator aborted" }))).GetProperty("status").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = Assert.Single(await db.MissionLogs.Where(x => x.MissionId == id && x.LogType == "CANCELLED").ToListAsync());
        Assert.Null(log.UserId); Assert.Equal(gatewayId, log.GatewayId); Assert.Equal("Operator aborted", log.Message);
    }

    [Fact]
    public async Task Legacy_draft_cannot_schedule_until_mapping_is_supplied()
    {
        var draft = await Data(await human.PostAsJsonAsync("/api/missions", new {
            name = "Legacy draft", farmId, gatewayId, uavId, sensorNodeIds = new[] { sensorId },
            waypoints = new[] { new { sequenceNo = 2, localX = 0m, localY = 0m } } }));
        var id = draft.GetProperty("id").GetInt32();
        var start = DateTimeOffset.UtcNow.AddHours(1);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await human.PostAsJsonAsync($"/api/missions/{id}/schedule", new { scheduledStartAt = start, scheduledEndAt = start.AddHours(1) })).StatusCode);
        await Data(await human.PutAsJsonAsync($"/api/missions/{id}", Plan()));
        await Data(await human.PostAsJsonAsync($"/api/missions/{id}/schedule", new { scheduledStartAt = start, scheduledEndAt = start.AddHours(1) }));
    }

    [Fact]
    public async Task Start_rechecks_active_resources_and_running_overlap_under_lock()
    {
        var id = await Scheduled();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var uav = (await db.Uavs.FindAsync(uavId))!;
        uav.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await device.PostAsJsonAsync($"{Root}/{id}/start", new { })).StatusCode);
        uav.IsActive = true;
        var sensor = (await db.SensorNodes.FindAsync(sensorId))!;
        sensor.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await device.PostAsJsonAsync($"{Root}/{id}/start", new { })).StatusCode);
        sensor.IsActive = true;
        var conflicting = new Mission { Name = "Already running", FarmId = farmId, UavId = uavId, GatewayId = gatewayId,
            Status = MissionStatus.RUNNING, CreatedAt = DateTimeOffset.UtcNow, CreatedByUserId = await db.Users.Where(x => x.Email == "admin@example.com").Select(x => x.Id).SingleAsync() };
        db.Missions.Add(conflicting); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await device.PostAsJsonAsync($"{Root}/{id}/start", new { })).StatusCode);
        Assert.Equal(MissionStatus.SCHEDULED, (await db.Missions.AsNoTracking().SingleAsync(x => x.Id == id)).Status);
    }

    [Fact]
    public async Task Next_filters_status_and_gateway_then_orders_schedule_and_id()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actor = await db.Users.Where(x => x.Email == "admin@example.com").Select(x => x.Id).SingleAsync();
        var earliest = DateTimeOffset.UtcNow.AddDays(1);
        var rows = new List<Mission>();
        foreach (var (gateway, state, time) in new[] {
            (gatewayId, MissionStatus.SCHEDULED, earliest.AddHours(2)),
            (otherGatewayId, MissionStatus.SCHEDULED, earliest.AddHours(-2)),
            (gatewayId, MissionStatus.PENDING, earliest.AddHours(-1)),
            (gatewayId, MissionStatus.SCHEDULED, earliest),
            (gatewayId, MissionStatus.SCHEDULED, earliest) })
        {
            var row = new Mission { Name = "Selection", FarmId = farmId, GatewayId = gateway, UavId = uavId, Status = state,
                ScheduledStartAt = time, ScheduledEndAt = time.AddMinutes(10), CreatedByUserId = actor, CreatedAt = DateTimeOffset.UtcNow };
            db.Missions.Add(row); await db.SaveChangesAsync(); rows.Add(row);
        }
        Assert.Equal(rows[3].Id, (await Data(await device.GetAsync(Root + "/next"))).GetProperty("id").GetInt32());
        rows[3].Status = MissionStatus.CANCELLED; await db.SaveChangesAsync();
        Assert.Equal(rows[4].Id, (await Data(await device.GetAsync(Root + "/next"))).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Every_device_action_rejects_missing_credentials_wrong_route_and_missing_mission()
    {
        using var anonymous = factory.CreateApiClient();
        foreach (var action in new[] { "downloaded", "start", "complete", "fail", "cancel" })
        {
            var body = new { failureReason = "TIMEOUT", reason = "Operator aborted" };
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"{Root}/2147483647/{action}", body)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await device.PostAsJsonAsync($"/api/device/gateways/{otherGatewayId}/missions/2147483647/{action}", body)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await device.PostAsJsonAsync($"{Root}/2147483647/{action}", body)).StatusCode);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pull_uses_one_snapshot_when_web_replaces_the_plan_between_selects(bool keepScheduled)
    {
        var id = await Scheduled();
        var old = await Data(await human.GetAsync($"/api/missions/{id}"));
        var waypointId = old.GetProperty("waypoints")[0].GetProperty("id").GetInt32();
        var interceptor = new InterleavedPlanUpdate(async () => {
            var plan = JsonSerializer.SerializeToNode(Plan())!.AsObject();
            if (keepScheduled)
            {
                plan["scheduledStartAt"] = old.GetProperty("scheduledStartAt").GetString();
                plan["scheduledEndAt"] = old.GetProperty("scheduledEndAt").GetString();
            }
            await Data(await human.PutAsJsonAsync($"/api/missions/{id}", plan));
        });
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(factory.ConnectionString).AddInterceptors(interceptor).Options;
        await using var db = new AppDbContext(options);
        var mission = await new MissionRepository(db).GetNextScheduledForGatewayAsync(gatewayId, CancellationToken.None);
        Assert.True(interceptor.Updated);
        Assert.NotNull(mission);
        Assert.Equal(MissionStatus.SCHEDULED, mission.Status);
        Assert.Equal(waypointId, Assert.Single(mission.Waypoints).Id);
        Assert.Equal(waypointId, Assert.Single(mission.Targets).WaypointId);
        Assert.Equal("SENSOR-001", mission.Targets[0].SensorNode.DeviceCode);
    }
    private sealed class InterleavedPlanUpdate(Func<Task> update) : DbCommandInterceptor
    {
        public bool Updated { get; private set; }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!Updated && command.CommandText.Contains("FROM missions AS", StringComparison.Ordinal))
            {
                Updated = true;
                await update();
            }
            return result;
        }
    }
}
