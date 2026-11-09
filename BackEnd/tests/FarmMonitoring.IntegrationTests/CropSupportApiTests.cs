using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;
[Collection("PostgreSQL Auth")]
public sealed class CropSupportApiTests(ApiFactory factory)
{
    [Fact]
    public async Task Processing_checks_current_assignment_roles_membership_and_rolls_back_failed_notifications()
    {
        using var owner = await Login("owner@example.com"); using var unassigned = await Login("engineer@example.com");
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User { Email = $"crop-{Guid.NewGuid():N}@example.com", FullName = "Crop engineer", CreatedAt = DateTimeOffset.UtcNow };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(user, "Test-password-123!");
        var roleId = await db.Roles.Where(x => x.Name == RoleNames.FarmEngineer).Select(x => x.Id).SingleAsync();
        user.UserRoles.Add(new UserRole { RoleId = roleId }); db.Users.Add(user);
        var farm = new Farm { Name = "Crop permission checks", CreatedAt = DateTimeOffset.UtcNow }; db.Farms.Add(farm); await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, "owner@example.com"); using var engineer = await Login(user.Email);
        var response = await owner.PostAsJsonAsync("/api/crop-condition-reports", new { farmId = farm.Id, crop = "Rice", description = "Yellow leaves" }); response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        var path = $"/api/crop-condition-reports/{id}";
        (await engineer.PostAsync($"{path}/claim", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await unassigned.GetAsync($"{path}/crop-seasons")).StatusCode);
        var ownerId = await db.Users.Where(x => x.Email == "owner@example.com").Select(x => x.Id).SingleAsync();
        db.CropSeasons.AddRange(new CropSeason { OwnerId = ownerId, FarmId = farm.Id, Name = "Spring rice", Crop = "Rice", CreatedAt = DateTimeOffset.UtcNow },
            new CropSeason { OwnerId = user.Id, FarmId = farm.Id, Name = "Private", Crop = "Corn", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var seasons = await engineer.GetFromJsonAsync<JsonElement>($"{path}/crop-seasons");
        var season = Assert.Single(seasons.GetProperty("data").EnumerateArray());
        Assert.Equal("Spring rice", season.GetProperty("name").GetString());
        Assert.False(season.TryGetProperty("revenueVnd", out _));
        var request = new { expectedRevision = 2, content = "Please send more photos" };
        Assert.Equal(HttpStatusCode.Forbidden, (await unassigned.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await unassigned.PostAsJsonAsync($"{path}/request-information", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await engineer.PostAsJsonAsync($"{path}/recommend", new { expectedRevision = 2, content = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await engineer.PostAsJsonAsync($"{path}/request-information", new { expectedRevision = 2, content = "More photos", recommendSiteSurvey = true })).StatusCode);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_reject_crop_notification() RETURNS trigger LANGUAGE plpgsql AS
            $$ BEGIN IF NEW.subject = 'Crop support updated' THEN RAISE EXCEPTION 'crop-test-failure'; END IF; RETURN NEW; END; $$;
            CREATE TRIGGER test_reject_crop_notification BEFORE INSERT ON notifications
            FOR EACH ROW EXECUTE FUNCTION test_reject_crop_notification();
            """);
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await engineer.PostAsJsonAsync($"{path}/request-information", request)).StatusCode);
            var unchanged = await db.CropConditionReports.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(2, unchanged.Revision); Assert.Equal(CropConditionStatus.IN_REVIEW, unchanged.Status);
            Assert.Equal(2, await db.CropConsultationUpdates.CountAsync(x => x.ReportId == id));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_reject_crop_notification ON notifications; DROP FUNCTION test_reject_crop_notification();");
        }
        await db.UserRoles.Where(x => x.UserId == user.Id).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await engineer.PostAsJsonAsync($"{path}/request-information", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await engineer.GetAsync(path)).StatusCode);
        db.ChangeTracker.Clear(); db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId }); await db.SaveChangesAsync();
        (await engineer.PostAsJsonAsync($"{path}/request-information", request)).EnsureSuccessStatusCode();
        await db.UserFarms.Where(x => x.FarmId == farm.Id).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"{path}/reply", new { expectedRevision = 3, content = "New photos" })).StatusCode);
        var final = await db.CropConditionReports.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(3, final.Revision); Assert.Equal(CropConditionStatus.AWAITING_INFORMATION, final.Status);
    }
    [Fact]
    public async Task Engineer_workflow_preserves_history_and_rejects_stale_or_invalid_transitions()
    {
        using var owner = await Login("owner@example.com"); using var engineer = await Login("engineer@example.com"); using var admin = await Login("admin@example.com");
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm = new Farm { Name = "Crop workflow", CreatedAt = DateTimeOffset.UtcNow };
        db.Farms.Add(farm); await db.SaveChangesAsync(); await factory.AssignFarmAsync(farm.Id, "owner@example.com");
        var created = await owner.PostAsJsonAsync("/api/crop-condition-reports", new { farmId = farm.Id, crop = "Rice", description = "Leaf spots" }); created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        var path = $"/api/crop-condition-reports/{id}";
        object Note(int revision, bool survey = false) => new { expectedRevision = revision, content = "Recorded consultation", recommendSiteSurvey = survey,
            images = new[] { new { contentType = "image/jpeg", dataBase64 = "/9j/2Q==" } } };
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync($"{path}/claim", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsync($"{path}/claim", null)).StatusCode);
        var claims = await Task.WhenAll(engineer.PostAsync($"{path}/claim", null), engineer.PostAsync($"{path}/claim", null));
        Assert.Single(claims, x => x.IsSuccessStatusCode); Assert.Single(claims, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"{path}/reply", Note(2))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"{path}/recommend", Note(2))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await engineer.PostAsJsonAsync($"{path}/request-information", Note(2))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"{path}/reply", Note(2))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync($"{path}/reply", Note(3))).StatusCode);
        var recommendations = await Task.WhenAll(engineer.PostAsJsonAsync($"{path}/recommend", Note(4, true)), engineer.PostAsJsonAsync($"{path}/recommend", Note(4, true)));
        Assert.Single(recommendations, x => x.IsSuccessStatusCode); Assert.Single(recommendations, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.OK, (await engineer.PostAsJsonAsync($"{path}/resolve", Note(5))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await engineer.PostAsJsonAsync($"{path}/recommend", Note(6))).StatusCode);
        var detail = (await owner.GetFromJsonAsync<JsonElement>(path)).GetProperty("data");
        Assert.Equal("RESOLVED", detail.GetProperty("status").GetString()); Assert.Equal(6, detail.GetProperty("revision").GetInt32());
        var updates = detail.GetProperty("updates").EnumerateArray().ToArray();
        Assert.Equal(new[] { "SUBMITTED", "CLAIMED", "INFORMATION_REQUESTED", "OWNER_REPLIED", "RECOMMENDED", "RESOLVED" }, updates.Select(x => x.GetProperty("action").GetString()));
        Assert.True(updates[4].GetProperty("recommendSiteSurvey").GetBoolean()); Assert.Single(updates[3].GetProperty("images").EnumerateArray());
        Assert.Equal(10, await db.Notifications.CountAsync(x => x.Subject == "Crop support updated" && x.Message.Contains($"#{id}:")));
    }
    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" }); response.EnsureSuccessStatusCode();
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString()); return client;
    }
    [Fact]
    public async Task Owner_intake_validates_scope_and_preserves_private_evidence_and_notifications()
    {
        using var owner = await Login("owner@example.com"); using var admin = await Login("admin@example.com"); using var engineer = await Login("engineer@example.com");
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm = new Farm { Name = "Crop intake", CreatedAt = DateTimeOffset.UtcNow }; var other = new Farm { Name = "Other crop farm", CreatedAt = DateTimeOffset.UtcNow };
        db.Farms.AddRange(farm, other); await db.SaveChangesAsync(); await factory.AssignFarmAsync(farm.Id, "owner@example.com");
        const string path = "/api/crop-condition-reports";
        object Body(int farmId, int? zoneId = null, int[]? sensors = null) => new { farmId, zoneId, crop = "Tomato", description = "Yellow leaves", sensorChannelIds = sensors,
            images = new[] { new { contentType = "image/jpeg", dataBase64 = "/9j/2Q==" } } };
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync(path, Body(farm.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(path, Body(other.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync(path, Body(farm.Id, int.MaxValue))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync(path, Body(farm.Id, sensors: [int.MaxValue]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path, new { farmId = farm.Id, crop = "Tomato", description = "Yellow", images = new[] { new { contentType = "image/jpeg", dataBase64 = "bad" } } })).StatusCode);
        var zone = new Zone { FarmId = farm.Id, Name = "Crop zone", CreatedAt = DateTimeOffset.UtcNow };
        var otherZone = new Zone { FarmId = other.Id, Name = "Private zone", CreatedAt = DateTimeOffset.UtcNow };
        db.Zones.AddRange(zone, otherZone); await db.SaveChangesAsync();
        var type = new SensorType { Code = $"crop-{Guid.NewGuid():N}", Name = "Crop test" };
        var channel = new SensorChannel { ChannelCode = "crop", SensorType = type, CreatedAt = DateTimeOffset.UtcNow,
            SensorNode = new SensorNode { ZoneId = zone.Id, DeviceCode = $"crop-{Guid.NewGuid():N}", Name = "Crop sensor", Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow } };
        db.SensorChannels.Add(channel); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync(path, Body(farm.Id, otherZone.Id))).StatusCode);
        var sameFarmZone = new Zone { FarmId = farm.Id, Name = "Other zone", CreatedAt = DateTimeOffset.UtcNow };
        db.Zones.Add(sameFarmZone); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync(path, Body(farm.Id, sameFarmZone.Id, [channel.Id]))).StatusCode);
        var response = await owner.PostAsJsonAsync(path, Body(farm.Id, zone.Id, [channel.Id])); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var detail = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data"); var id = detail.GetProperty("id").GetInt32();
        Assert.Equal("NEW", detail.GetProperty("status").GetString()); Assert.Single(detail.GetProperty("images").EnumerateArray());
        Assert.Equal(channel.Id, Assert.Single(detail.GetProperty("sensorChannelIds").EnumerateArray()).GetInt32());
        Assert.Equal("SUBMITTED", Assert.Single(detail.GetProperty("updates").EnumerateArray()).GetProperty("action").GetString());
        db.SensorReadings.Add(new SensorReading { SensorChannelId = channel.Id, SourceRecordKey = $"crop-{Guid.NewGuid():N}", Value = 27,
            MeasuredAt = DateTimeOffset.UtcNow, CollectedAt = DateTimeOffset.UtcNow, ReceivedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var context = await engineer.GetFromJsonAsync<JsonElement>($"{path}/{id}/sensor-readings");
        Assert.Equal(27, Assert.Single(context.GetProperty("data").EnumerateArray()).GetProperty("value").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, (await engineer.GetAsync($"{path}/{id}/sensor-readings?pageSize=0")).StatusCode);
        channel.SensorNode.ZoneId = otherZone.Id; await db.SaveChangesAsync();
        var moved = await engineer.GetFromJsonAsync<JsonElement>($"{path}/{id}/sensor-readings");
        Assert.Empty(moved.GetProperty("data").EnumerateArray());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync(path, Body(farm.Id, sensors: [channel.Id]))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await engineer.GetAsync($"{path}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"{path}/{id}")).StatusCode);
        var list = await owner.GetFromJsonAsync<JsonElement>(path);
        Assert.Contains(list.GetProperty("data").EnumerateArray(), x => x.GetProperty("id").GetInt32() == id);
        Assert.False(list.GetProperty("data")[0].TryGetProperty("images", out _));
        Assert.True(await db.Notifications.AnyAsync(x => x.Subject == "Crop condition reported" && x.Message.Contains($"#{id}")));
        await db.UserFarms.Where(x => x.FarmId == farm.Id).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"{path}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"{path}/{id}/sensor-readings")).StatusCode);
        var after = await owner.GetFromJsonAsync<JsonElement>(path); Assert.DoesNotContain(after.GetProperty("data").EnumerateArray(), x => x.GetProperty("id").GetInt32() == id);
    }
}
