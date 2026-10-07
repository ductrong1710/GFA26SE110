using FarmMonitoring.Application.Features.Alerts;
using FarmMonitoring.Application.Interfaces;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

public sealed class FarmScopeSecurityTests : IAsyncLifetime
{
    private readonly ApiFactory factory = new();
    private HttpClient admin = null!, owner = null!, engineer = null!;
    private readonly Farm[] farms = new Farm[2];
    private readonly SensorChannel[] channels = new SensorChannel[2];
    private readonly Mission[] missions = new Mission[2];
    private readonly Alert[] alerts = new Alert[2];
    private int ownerId, engineerId, globalAlertId;

    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateApiClient();
        var data = await Data(await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        return client;
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{response.RequestMessage?.RequestUri}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }
    private static async Task Status(HttpStatusCode expected, HttpResponseMessage response) =>
        Assert.True(expected == response.StatusCode, $"{response.RequestMessage?.RequestUri}: expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    public async Task InitializeAsync()
    {
        await factory.InitializeAsync();
        admin = await Login("admin@example.com"); owner = await Login("owner@example.com"); engineer = await Login("engineer@example.com");
        ownerId = (await Data(await owner.GetAsync("/api/auth/me"))).GetProperty("id").GetInt32();
        engineerId = (await Data(await engineer.GetAsync("/api/auth/me"))).GetProperty("id").GetInt32();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 2; i++)
        {
            farms[i] = new Farm { Name = "Scope farm " + i, CreatedAt = now };
            var zone = new Zone { Farm = farms[i], Name = "Zone " + i, CreatedAt = now };
            var node = new SensorNode { Zone = zone, DeviceCode = "scope-node-" + i, Name = "Node " + i, Status = "ONLINE", CreatedAt = now };
            channels[i] = new SensorChannel { SensorNode = node, ChannelCode = "temp", SensorType = new SensorType { Code = "scope-type-" + i, Name = "Temperature" }, CreatedAt = now };
            var gateway = new Gateway { Code = i == 0 ? "integration-gateway" : "scope-other-gateway", Name = "Gateway " + i, GatewayType = "ESP32", Status = "ONLINE", CreatedAt = now };
            var uav = new Uav { Code = "scope-uav-" + i, Name = "UAV " + i, Status = "ONLINE", CreatedAt = now };
            missions[i] = new Mission { Farm = farms[i], Name = "Mission " + i, Gateway = gateway, Uav = uav, CreatedByUserId = ownerId,
                Status = MissionStatus.PENDING, CreatedAt = now, Targets = [new MissionTarget { SensorNode = node, SequenceNo = 1 }],
                Waypoints = [new MissionWaypoint { SequenceNo = 1, LocalX = 0, LocalY = 0 }] };
            db.Missions.Add(missions[i]);
            db.SensorReadings.Add(new SensorReading { SensorChannel = channels[i], Mission = missions[i], Gateway = gateway, SourceRecordKey = "scope-reading", Value = i == 0 ? 11 : 999,
                MeasuredAt = now, CollectedAt = now, ReceivedAt = now });
            db.SensorThresholds.Add(new SensorThreshold { SensorChannel = channels[i], MaxValue = 40, IsEnabled = true, CreatedAt = now });
            alerts[i] = new Alert { SensorChannel = channels[i], SensorNode = node, Mission = missions[i], AlertType = AlertType.SENSOR_THRESHOLD, Severity = AlertSeverity.WARNING, Message = "Alert " + i, OpenedAt = now };
            db.Alerts.Add(alerts[i]);
            db.TelemetryRecords.Add(new TelemetryRecord { Mission = missions[i], Gateway = gateway, Uav = uav, RecordedAt = now, BatteryPercent = 80 });
            db.MissionLogs.Add(new MissionLog { Mission = missions[i], LogType = "CREATED", Message = "Log " + i, CreatedAt = now });
        }
        var global = new Alert { AlertType = AlertType.UAV_LOW_BATTERY, Severity = AlertSeverity.WARNING, Message = "Global equipment", OpenedAt = now };
        db.Alerts.Add(global);
        await db.SaveChangesAsync(); globalAlertId = global.Id;
        await Status(HttpStatusCode.Created, await admin.PostAsJsonAsync($"/api/farms/{farms[0].Id}/members", new { userId = ownerId }));
        await Status(HttpStatusCode.Created, await admin.PostAsJsonAsync($"/api/farms/{farms[0].Id}/members", new { userId = engineerId }));
    }
    public async Task DisposeAsync() { admin?.Dispose(); owner?.Dispose(); engineer?.Dispose(); await factory.DisposeAsync(); }

    [Fact]
    public async Task Membership_management_checks_roles_eligibility_duplicates_missing_and_immediate_removal()
    {
        var path = $"/api/farms/{farms[0].Id}/members";
        var list = await Data(await admin.GetAsync(path));
        Assert.Equal(2, list.GetArrayLength());
        Assert.All(list.EnumerateArray(), x => { Assert.True(x.GetProperty("roles").GetArrayLength() > 0); Assert.False(x.TryGetProperty("passwordHash", out _)); Assert.NotEmpty(x.GetProperty("assignedAt").GetString()!); });
        await Status(HttpStatusCode.Conflict, await admin.PostAsJsonAsync(path, new { userId = ownerId }));
        foreach (var client in new[] { owner, engineer })
        {
            await Status(HttpStatusCode.Forbidden, await client.GetAsync(path));
            await Status(HttpStatusCode.Forbidden, await client.PostAsJsonAsync(path, new { userId = ownerId }));
            await Status(HttpStatusCode.Forbidden, await client.DeleteAsync(path + "/" + ownerId));
        }
        await Status(HttpStatusCode.Created, await admin.PostAsJsonAsync($"/api/farms/{farms[1].Id}/members", new { userId = ownerId }));
        Assert.Equal(2, (await Data(await owner.GetAsync("/api/farms"))).GetArrayLength());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adminId = await db.Users.Where(x => x.Email == "admin@example.com").Select(x => x.Id).SingleAsync();
        var disabledId = await db.Users.Where(x => x.Email == "disabled@example.com").Select(x => x.Id).SingleAsync();
        var noRole = new User { Email = "unassigned-role@example.com", FullName = "No role", PasswordHash = "not-login-material", CreatedAt = DateTimeOffset.UtcNow };
        var inactiveOwner = new User { Email = "inactive-owner@example.com", FullName = "Inactive Owner", IsActive = false, PasswordHash = "not-login-material", CreatedAt = DateTimeOffset.UtcNow,
            UserRoles = [new UserRole { RoleId = await db.Roles.Where(x => x.Name == "FarmOwner").Select(x => x.Id).SingleAsync() }] };
        db.Users.AddRange(noRole, inactiveOwner); await db.SaveChangesAsync();
        foreach (var id in new[] { adminId, disabledId, noRole.Id, inactiveOwner.Id }) await Status(HttpStatusCode.UnprocessableEntity, await admin.PostAsJsonAsync(path, new { userId = id }));
        await Status(HttpStatusCode.BadRequest, await admin.PostAsJsonAsync(path, new { userId = 0 }));
        await Status(HttpStatusCode.NotFound, await admin.PostAsJsonAsync(path, new { userId = int.MaxValue }));
        await Status(HttpStatusCode.NotFound, await admin.PostAsJsonAsync("/api/farms/2147483647/members", new { userId = ownerId }));
        await Status(HttpStatusCode.NoContent, await admin.DeleteAsync(path + "/" + ownerId));
        await Status(HttpStatusCode.Forbidden, await owner.GetAsync($"/api/farms/{farms[0].Id}"));
        Assert.Equal("FarmOwner", Assert.Single((await Data(await owner.GetAsync("/api/auth/me"))).GetProperty("roles").EnumerateArray()).GetString());
        Assert.True(await db.Farms.AnyAsync(x => x.Id == farms[0].Id));
        await Status(HttpStatusCode.NotFound, await admin.DeleteAsync(path + "/" + ownerId));
        // Assignments alone cannot grant access once current roles are removed.
        await Data(await admin.PutAsJsonAsync($"/api/users/{engineerId}/roles", new { roles = Array.Empty<string>() }));
        await Status(HttpStatusCode.Forbidden, await engineer.GetAsync($"/api/farms/{farms[0].Id}"));
        Assert.True(await db.UserFarms.AnyAsync(x => x.UserId == engineerId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task All_farm_scoped_read_routes_deny_other_farm_and_lists_counts_exclude_it(bool asEngineer)
    {
        var human = asEngineer ? engineer : owner;
        var f = farms[1].Id; var z = channels[1].SensorNode.ZoneId; var n = channels[1].SensorNodeId; var c = channels[1].Id; var m = missions[1].Id;
        var forbidden = new[] {
            $"/api/farms/{f}", $"/api/farms/{f}/zones", $"/api/zones/{z}", $"/api/sensor-nodes/{n}", $"/api/sensor-nodes/{n}/channels", $"/api/sensor-nodes/{n}/latest",
            $"/api/sensor-channels/{c}/history", $"/api/sensor-channels/{c}/threshold", $"/api/sensor-nodes?farmId={f}", $"/api/sensor-nodes?zoneId={z}",
            $"/api/sensor-readings?farmId={f}", $"/api/sensor-readings?zoneId={z}", $"/api/sensor-readings?sensorNodeId={n}", $"/api/sensor-readings?sensorChannelId={c}", $"/api/sensor-readings?missionId={m}",
            $"/api/zones/compare?zoneIds={channels[0].SensorNode.ZoneId},{z}&sensorTypeId={channels[0].SensorTypeId}",
            $"/api/missions?farmId={f}", $"/api/missions/{m}", $"/api/missions/{m}/results", $"/api/missions/{m}/logs", $"/api/missions/{m}/waypoints", $"/api/missions/{m}/collection-attempts",
            $"/api/missions/{m}/telemetry", $"/api/missions/{m}/telemetry/latest", $"/api/alerts/{alerts[1].Id}", $"/api/alerts/{globalAlertId}", $"/api/alerts?farmId={f}", $"/api/alerts?missionId={m}",
            $"/api/reports/sensor-data?farmId={f}", $"/api/reports/missions?farmId={f}", $"/api/reports/alerts?farmId={f}", $"/api/reports/devices?farmId={f}", $"/api/dashboard/overview?farmId={f}" };
        foreach (var path in forbidden) await Status(HttpStatusCode.Forbidden, await human.GetAsync(path));
        foreach (var path in new[] { $"/api/farms/{farms[0].Id}", $"/api/zones/{channels[0].SensorNode.ZoneId}", $"/api/sensor-nodes/{channels[0].SensorNodeId}",
            $"/api/sensor-channels/{channels[0].Id}/history", $"/api/sensor-channels/{channels[0].Id}/threshold", $"/api/missions/{missions[0].Id}/results",
            $"/api/missions/{missions[0].Id}/logs", $"/api/missions/{missions[0].Id}/waypoints", $"/api/missions/{missions[0].Id}/collection-attempts", $"/api/missions/{missions[0].Id}/telemetry",
            $"/api/alerts/{alerts[0].Id}" }) await Status(HttpStatusCode.OK, await human.GetAsync(path));
        foreach (var (path, idName, allowedId) in new[] { ("/api/farms", "id", farms[0].Id), ("/api/sensor-nodes", "id", channels[0].SensorNodeId),
            ("/api/missions", "id", missions[0].Id), ("/api/alerts", "id", alerts[0].Id), ("/api/sensor-readings", "sensorChannelId", channels[0].Id) })
        {
            var response = await human.GetFromJsonAsync<JsonElement>(path + "?pageSize=1");
            Assert.Equal(1, response.GetProperty("pagination").GetProperty("totalItems").GetInt32());
            Assert.Equal(allowedId, Assert.Single(response.GetProperty("data").EnumerateArray()).GetProperty(idName).GetInt32());
        }
        Assert.Empty((await Data(await human.GetAsync("/api/farms?search=Scope%20farm%201"))).EnumerateArray());
        var dash = await Data(await human.GetAsync("/api/dashboard/overview"));
        Assert.Equal(1, dash.GetProperty("farms").GetInt32());
        Assert.Equal(1, dash.GetProperty("sensorNodes").GetProperty("total").GetInt32());
        Assert.Equal(1, dash.GetProperty("gateways").GetProperty("total").GetInt32());
        Assert.Equal(1, dash.GetProperty("alerts").GetProperty("open").GetInt32());
        Assert.Equal(11m, Assert.Single((await Data(await human.GetAsync("/api/reports/sensor-data"))).EnumerateArray()).GetProperty("average").GetDecimal());
        Assert.Equal(missions[0].Id, Assert.Single((await Data(await human.GetAsync("/api/reports/missions"))).EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Equal(1, Assert.Single((await Data(await human.GetAsync("/api/reports/alerts"))).EnumerateArray()).GetProperty("count").GetInt64());
        Assert.Equal(3, (await Data(await human.GetAsync("/api/reports/devices"))).GetArrayLength());
        await Status(HttpStatusCode.NotFound, await human.GetAsync("/api/farms/2147483647"));
        await Status(HttpStatusCode.NotFound, await human.GetAsync("/api/sensor-nodes/2147483647"));
        using var anonymous = factory.CreateApiClient();
        await Status(HttpStatusCode.Unauthorized, await anonymous.GetAsync("/api/farms"));
        var all = await Data(await admin.GetAsync("/api/dashboard/overview"));
        Assert.Equal(2, all.GetProperty("farms").GetInt32());
        Assert.Equal(3, all.GetProperty("alerts").GetProperty("open").GetInt32());
        await Status(HttpStatusCode.OK, await admin.GetAsync($"/api/farms/{f}"));
        await Status(HttpStatusCode.OK, await admin.GetAsync($"/api/alerts/{globalAlertId}"));
    }

    [Fact]
    public async Task Empty_memberships_return_empty_farm_scoped_data_but_preserve_global_equipment_catalog()
    {
        await Status(HttpStatusCode.NoContent, await admin.DeleteAsync($"/api/farms/{farms[0].Id}/members/{ownerId}"));
        foreach (var path in new[] { "/api/farms", "/api/sensor-nodes", "/api/sensor-readings", "/api/missions", "/api/alerts", "/api/reports/sensor-data", "/api/reports/alerts", "/api/reports/missions", "/api/reports/devices" })
            Assert.Empty((await Data(await owner.GetAsync(path))).EnumerateArray());
        Assert.Equal(0, (await Data(await owner.GetAsync("/api/dashboard/overview"))).GetProperty("farms").GetInt32());
        Assert.Equal(2, (await Data(await owner.GetAsync("/api/uavs"))).GetArrayLength());
        Assert.Equal(2, (await Data(await owner.GetAsync("/api/gateways"))).GetArrayLength());
    }

    [Fact]
    public async Task Farm_creation_assigns_owner_atomically_and_admin_requires_no_membership()
    {
        var created = await Data(await owner.PostAsJsonAsync("/api/farms", new { name = "Created by Owner" }));
        var id = created.GetProperty("id").GetInt32();
        await Status(HttpStatusCode.OK, await owner.GetAsync($"/api/farms/{id}"));
        var members = await Data(await admin.GetAsync($"/api/farms/{id}/members"));
        Assert.Equal(ownerId, Assert.Single(members.EnumerateArray()).GetProperty("userId").GetInt32());
        var adminFarm = await Data(await admin.PostAsJsonAsync("/api/farms", new { name = "Created by Admin" }));
        Assert.Empty((await Data(await admin.GetAsync($"/api/farms/{adminFarm.GetProperty("id").GetInt32()}/members"))).EnumerateArray());
        await Status(HttpStatusCode.Forbidden, await engineer.PostAsJsonAsync("/api/farms", new { name = "Forbidden" }));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Inject a real PostgreSQL failure after farm insert, at membership insert.
        await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION reject_test_assignment() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'test assignment failure'; END $$; CREATE TRIGGER reject_test_assignment BEFORE INSERT ON user_farms FOR EACH ROW EXECUTE FUNCTION reject_test_assignment();");
        await Status(HttpStatusCode.InternalServerError, await owner.PostAsJsonAsync("/api/farms", new { name = "Must roll back" }));
        Assert.False(await db.Farms.AnyAsync(x => x.Name == "Must roll back"));
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_test_assignment ON user_farms; DROP FUNCTION reject_test_assignment();");
    }

    private object MissionPlan(int index) => new { name = "Updated mission", farmId = farms[index].Id, uavId = missions[index].UavId,
        gatewayId = missions[index].GatewayId, sensorNodeIds = new[] { channels[index].SensorNodeId }, waypoints = new[] { new { sequenceNo = 1, localX = 0, localY = 0 } } };

    [Fact]
    public async Task Mutations_enforce_role_and_source_destination_farms()
    {
        foreach (var i in new[] { 0, 1 })
        {
            var status = i == 0 ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
            await Status(status, await owner.PutAsJsonAsync($"/api/farms/{farms[i].Id}", new { name = "Farm update" }));
            await Status(status, await owner.PatchAsJsonAsync($"/api/farms/{farms[i].Id}/status", new { isActive = true }));
            await Status(i == 0 ? HttpStatusCode.Created : HttpStatusCode.Forbidden, await owner.PostAsJsonAsync($"/api/farms/{farms[i].Id}/zones", new { name = "Added zone" }));
            await Status(status, await owner.PutAsJsonAsync($"/api/zones/{channels[i].SensorNode.ZoneId}", new { name = "Updated zone" }));
            await Status(status, await engineer.PutAsJsonAsync($"/api/sensor-channels/{channels[i].Id}/threshold", new { maxValue = 42, isEnabled = true }));
            await Status(HttpStatusCode.Forbidden, await owner.PutAsJsonAsync($"/api/sensor-channels/{channels[i].Id}/threshold", new { maxValue = 42 }));
            await Status(status, await owner.PutAsJsonAsync($"/api/missions/{missions[i].Id}", MissionPlan(i)));
            await Status(i == 0 ? HttpStatusCode.Created : HttpStatusCode.Forbidden, await owner.PostAsJsonAsync("/api/missions", MissionPlan(i)));
            await Status(HttpStatusCode.Forbidden, await engineer.PutAsJsonAsync($"/api/farms/{farms[i].Id}", new { name = "Denied" }));
            await Status(HttpStatusCode.Forbidden, await engineer.PutAsJsonAsync($"/api/zones/{channels[i].SensorNode.ZoneId}", new { name = "Denied" }));
            await Status(HttpStatusCode.Forbidden, await engineer.PostAsJsonAsync("/api/missions", MissionPlan(i)));
        }
        await Status(HttpStatusCode.Forbidden, await owner.PutAsJsonAsync($"/api/missions/{missions[0].Id}", MissionPlan(1)));
        await Status(HttpStatusCode.Forbidden, await owner.PutAsJsonAsync($"/api/missions/{missions[1].Id}", MissionPlan(0)));
        foreach (var (from, to) in new[] { (0, 1), (1, 0) })
            await Status(HttpStatusCode.Forbidden, await owner.PutAsJsonAsync($"/api/sensor-nodes/{channels[from].SensorNodeId}", new { zoneId = channels[to].SensorNode.ZoneId, deviceCode = "moved", name = "Moved", status = "ONLINE" }));
        await Status(HttpStatusCode.Forbidden, await owner.PostAsJsonAsync("/api/sensor-nodes", new { zoneId = channels[1].SensorNode.ZoneId, deviceCode = "new", name = "Denied", status = "ONLINE" }));
        await Status(HttpStatusCode.Forbidden, await owner.PatchAsJsonAsync($"/api/sensor-nodes/{channels[1].SensorNodeId}/status", new { status = "OFFLINE", isActive = false }));
        await Status(HttpStatusCode.Forbidden, await owner.PostAsJsonAsync($"/api/sensor-nodes/{channels[1].SensorNodeId}/channels", new { sensorTypeId = channels[1].SensorTypeId, channelCode = "denied" }));
        await Status(HttpStatusCode.Forbidden, await owner.PutAsJsonAsync($"/api/sensor-channels/{channels[1].Id}", new { sensorTypeId = channels[1].SensorTypeId, channelCode = "denied" }));
        foreach (var client in new[] { owner, engineer })
        {
            foreach (var action in new[] { "schedule", "start", "complete", "fail", "cancel" })
                await Status(HttpStatusCode.Forbidden, await client.PostAsJsonAsync($"/api/missions/{missions[1].Id}/{action}", new { scheduledStartAt = DateTimeOffset.UtcNow.AddHours(1), scheduledEndAt = DateTimeOffset.UtcNow.AddHours(2), failureReason = "Denied" }));
            await Status(HttpStatusCode.Forbidden, await client.PatchAsJsonAsync($"/api/missions/{missions[1].Id}/status", new { status = "CANCELLED" }));
            foreach (var action in new[] { "acknowledge", "notes", "close" })
                await Status(HttpStatusCode.Forbidden, await client.PostAsJsonAsync($"/api/alerts/{alerts[1].Id}/{action}", new { note = "Denied" }));
        }
        foreach (var action in new[] { "acknowledge", "notes", "close" })
            await Status(HttpStatusCode.OK, await engineer.PostAsJsonAsync($"/api/alerts/{alerts[0].Id}/{action}", new { note = "Allowed" }));
        await Status(HttpStatusCode.OK, await owner.PostAsJsonAsync($"/api/missions/{missions[0].Id}/cancel", new { note = "Allowed" }));
    }

    [Fact]
    public async Task Concurrent_membership_assignment_creates_one_row_and_preserves_global_roles()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            admin.PostAsJsonAsync($"/api/farms/{farms[1].Id}/members", new { userId = engineerId })));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("FarmEngineer", Assert.Single((await Data(await engineer.GetAsync("/api/auth/me"))).GetProperty("roles").EnumerateArray()).GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.UserFarms.CountAsync(x => x.UserId == engineerId));
        await Data(await admin.PutAsJsonAsync($"/api/users/{engineerId}/roles", new { roles = new[] { "FarmAdministrator", "FarmEngineer" } }));
        await Status(HttpStatusCode.UnprocessableEntity, await admin.PostAsJsonAsync($"/api/farms/{farms[0].Id}/members", new { userId = engineerId }));
        // Admin status now permits all farms independently of its old memberships.
        await Status(HttpStatusCode.NoContent, await admin.DeleteAsync($"/api/farms/{farms[1].Id}/members/{engineerId}"));
        await Status(HttpStatusCode.OK, await engineer.GetAsync($"/api/farms/{farms[1].Id}"));
    }

    [Fact]
    public async Task New_alert_notifications_only_target_assigned_users_and_private_ownership_remains_enforced()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<AlertService>();
        var repository = scope.ServiceProvider.GetRequiredService<IAlertRepository>();
        for (var i = 0; i < 2; i++)
        {
            var index = i;
            await repository.InTransactionAsync(async () =>
            {
                await service.RaiseAsync(new(AlertType.SENSOR_DATA_TIMEOUT, AlertSeverity.WARNING, channels[index].SensorNodeId, channels[index].Id,
                    null, null, null, "Scoped notification " + index, null), default);
                await db.SaveChangesAsync();
                return true;
            }, default);
        }
        var ownerNotifications = await Data(await owner.GetAsync("/api/notifications"));
        Assert.Equal("Scoped notification 0", Assert.Single(ownerNotifications.EnumerateArray()).GetProperty("message").GetString());
        Assert.Single((await Data(await engineer.GetAsync("/api/notifications"))).EnumerateArray());
        Assert.Equal(2, (await Data(await admin.GetAsync("/api/notifications"))).GetArrayLength());
        var foreignNotificationId = (await Data(await engineer.GetAsync("/api/notifications")))[0].GetProperty("id").GetInt32();
        await Status(HttpStatusCode.NotFound, await owner.PatchAsJsonAsync($"/api/notifications/{foreignNotificationId}/read", new { }));
        // A notification already delivered to this user remains private historical correspondence.
        await Status(HttpStatusCode.NoContent, await admin.DeleteAsync($"/api/farms/{farms[0].Id}/members/{ownerId}"));
        Assert.Single((await Data(await owner.GetAsync("/api/notifications"))).EnumerateArray());
    }

    [Fact]
    public async Task Historical_mission_targets_do_not_leak_live_metadata_after_sensor_moves_farms()
    {
        await Data(await owner.PostAsJsonAsync($"/api/missions/{missions[0].Id}/cancel", new { note = "Finished" }));
        await Data(await admin.PutAsJsonAsync($"/api/sensor-nodes/{channels[0].SensorNodeId}", new { zoneId = channels[1].SensorNode.ZoneId,
            deviceCode = channels[0].SensorNode.DeviceCode, name = "Private Farm2 sensor name", status = "ONLINE" }));
        await Status(HttpStatusCode.Forbidden, await owner.GetAsync($"/api/sensor-nodes/{channels[0].SensorNodeId}"));
        foreach (var suffix in new[] { "", "/results" })
        {
            var history = await Data(await owner.GetAsync($"/api/missions/{missions[0].Id}{suffix}"));
            var target = Assert.Single(history.GetProperty("targets").EnumerateArray());
            Assert.Equal(channels[0].SensorNodeId, target.GetProperty("sensorNodeId").GetInt32());
            Assert.Equal(JsonValueKind.Null, target.GetProperty("sensorName").ValueKind);
            Assert.DoesNotContain("Private Farm2 sensor name", history.ToString());
            var all = await Data(await admin.GetAsync($"/api/missions/{missions[0].Id}{suffix}"));
            Assert.Equal("Private Farm2 sensor name", all.GetProperty("targets")[0].GetProperty("sensorName").GetString());
        }
        await Status(HttpStatusCode.Created, await admin.PostAsJsonAsync($"/api/farms/{farms[1].Id}/members", new { userId = ownerId }));
        var visible = await Data(await owner.GetAsync($"/api/missions/{missions[0].Id}/results"));
        Assert.Equal("Private Farm2 sensor name", visible.GetProperty("targets")[0].GetProperty("sensorName").GetString());
    }

    [Fact]
    public async Task Devices_work_without_human_memberships_and_jwt_cannot_replace_headers()
    {
        await Status(HttpStatusCode.NoContent, await admin.DeleteAsync($"/api/farms/{farms[0].Id}/members/{ownerId}"));
        await Status(HttpStatusCode.NoContent, await admin.DeleteAsync($"/api/farms/{farms[0].Id}/members/{engineerId}"));
        using var device = factory.CreateApiClient();
        device.DefaultRequestHeaders.Add("X-Gateway-Code", "integration-gateway"); device.DefaultRequestHeaders.Add("X-Api-Key", factory.DeviceKey);
        var telemetry = new { missionId = missions[0].Id, gatewayId = missions[0].GatewayId, recordedAt = DateTimeOffset.UtcNow, batteryPercent = 50 };
        await Status(HttpStatusCode.Created, await device.PostAsJsonAsync("/api/device/telemetry", telemetry));
        var sync = new { batchKey = Guid.NewGuid().ToString("N"), records = Array.Empty<object>() };
        (await device.PostAsJsonAsync($"/api/device/gateways/{missions[0].GatewayId}/sync", sync)).EnsureSuccessStatusCode();
        foreach (var human in new[] { admin, owner, engineer })
        {
            await Status(HttpStatusCode.Unauthorized, await human.PostAsJsonAsync("/api/device/telemetry", telemetry));
            await Status(HttpStatusCode.Unauthorized, await human.PostAsJsonAsync($"/api/device/gateways/{missions[0].GatewayId}/sync", sync));
        }
        await Status(HttpStatusCode.Unauthorized, await device.GetAsync("/api/farms"));
        await Status(HttpStatusCode.Forbidden, await owner.GetAsync($"/api/missions/{missions[0].Id}/telemetry"));
    }
}
