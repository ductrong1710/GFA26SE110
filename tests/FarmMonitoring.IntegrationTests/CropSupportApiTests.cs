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
    public async Task Evidence_window_pages_valid_and_invalid_readings_and_alerts_without_fabricating_missing_data()
    {
        using var owner = await Login("owner@example.com"); using var engineer = await Login("engineer@example.com");
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm = new Farm { Name = "Window farm" }; var zone = new Zone { Farm = farm, Name = "Window zone" };
        var type = new SensorType { Code = $"window-{Guid.NewGuid():N}", Name = "Temperature" };
        var channel = new SensorChannel { ChannelCode = "TEMP", SensorType = type,
            SensorNode = new SensorNode { Zone = zone, Name = "Window node", DeviceCode = $"window-{Guid.NewGuid():N}", Status = "ACTIVE" } };
        db.SensorChannels.Add(channel); await db.SaveChangesAsync(); await factory.AssignFarmAsync(farm.Id, "owner@example.com");
        var now = DateTimeOffset.UtcNow.AddHours(-2);
        var created = await owner.PostAsJsonAsync("/api/crop-condition-reports", new { farmId = farm.Id, zoneId = zone.Id, crop = "Rice", description = "Spots", observedAt = now.ToString("O"), sensorChannelIds = new[] { channel.Id } }); created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32(); var path = $"/api/crop-condition-reports/{id}";
        (await engineer.PostAsync($"{path}/claim", null)).EnsureSuccessStatusCode();
        foreach (var days in new[] { -10, -2, -1, 2 }) db.SensorReadings.Add(new SensorReading { SensorChannelId = channel.Id, SourceRecordKey = Guid.NewGuid().ToString(),
            Value = days == -1 ? 0 : 25, IsValid = days != -2, QualityStatus = days == -2 ? "ERROR" : null, MeasuredAt = now.AddDays(days), CollectedAt = now.AddDays(days).AddMinutes(1), ReceivedAt = now.AddDays(days).AddMinutes(2) });
        db.Alerts.AddRange(new Alert { SensorChannelId = channel.Id, Message = "Within window", OpenedAt = now, AlertType = AlertType.SENSOR_THRESHOLD },
            new Alert { SensorChannelId = channel.Id, Message = "Outside window", OpenedAt = now.AddDays(-10), AlertType = AlertType.SENSOR_THRESHOLD });
        await db.SaveChangesAsync();
        var first = await engineer.GetFromJsonAsync<JsonElement>($"{path}/sensor-readings?pageSize=1");
        var reading = Assert.Single(first.GetProperty("data").EnumerateArray()); Assert.Equal(0, reading.GetProperty("value").GetDecimal()); Assert.True(reading.GetProperty("isValid").GetBoolean());
        var second = await engineer.GetFromJsonAsync<JsonElement>($"{path}/sensor-readings?pageSize=1&page=2");
        Assert.False(Assert.Single(second.GetProperty("data").EnumerateArray()).GetProperty("isValid").GetBoolean());
        Assert.Empty((await engineer.GetFromJsonAsync<JsonElement>($"{path}/sensor-readings?sensorChannelId=2147483647")).GetProperty("data").EnumerateArray());
        Assert.Single((await engineer.GetFromJsonAsync<JsonElement>($"{path}/alerts")).GetProperty("data").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await engineer.GetAsync($"{path}/sensor-readings?from=2026-01-01T00:00:00Z&to=2026-03-01T00:00:00Z")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await engineer.GetAsync($"{path}/sensor-readings?from=2026-01-01T00:00:00&to=2026-01-02T00:00:00Z")).StatusCode);
        var explicitRange = $"from={Uri.EscapeDataString(now.AddDays(-11).ToString("O"))}&to={Uri.EscapeDataString(now.AddDays(-9).ToString("O"))}";
        Assert.Single((await engineer.GetFromJsonAsync<JsonElement>($"{path}/sensor-readings?{explicitRange}")).GetProperty("data").EnumerateArray());
    }
    [Fact]
    public async Task Season_report_assignment_followup_review_and_reopen_preserve_season_and_current_permissions()
    {
        using var owner = await Login("owner@example.com"); using var engineer = await Login("engineer@example.com"); using var admin = await Login("admin@example.com");
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm = new Farm { Name = "MF04 end-to-end" }; db.Farms.Add(farm); await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, "owner@example.com");
        var engineerId = await db.Users.Where(x => x.Email == "engineer@example.com").Select(x => x.Id).SingleAsync();
        async Task<JsonElement> Data(HttpResponseMessage response) { response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data"); }
        var season = await Data(await owner.PostAsJsonAsync("/api/crop-seasons", new { farmId = farm.Id, name = "Autumn", crop = "Rice" }));
        var seasonId = season.GetProperty("id").GetInt32();
        var images = new[] { new { contentType = "image/jpeg", dataBase64 = "/9j/2Q==" } };
        var day = DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        await Data(await owner.PostAsJsonAsync($"/api/crop-seasons/{seasonId}/activities", new { expectedRevision = 1, date = day, type = "INSPECTION", description = "Leaves inspected", images }));
        var activityId = await db.CultivationActivities.Where(x => x.SeasonId == seasonId).Select(x => x.Id).SingleAsync();
        var observed = DateTimeOffset.UtcNow.AddHours(-1).ToOffset(TimeSpan.FromHours(7));
        var report = await Data(await owner.PostAsJsonAsync("/api/crop-condition-reports", new { farmId = farm.Id, cropSeasonId = seasonId, crop = "Rice", description = "Leaf spots", observedAt = observed.ToString("O"), images }));
        var id = report.GetProperty("id").GetInt32(); var path = $"/api/crop-condition-reports/{id}";
        Assert.Equal(TimeSpan.Zero, report.GetProperty("observedAt").GetDateTimeOffset().Offset);
        Assert.Equal(seasonId, report.GetProperty("linkedSeason").GetProperty("id").GetInt32());
        var inbox = await Data(await engineer.GetAsync(path));
        Assert.Empty(inbox.GetProperty("images").EnumerateArray()); Assert.Empty(inbox.GetProperty("updates").EnumerateArray());
        Assert.False(inbox.GetProperty("canReadEvidence").GetBoolean());
        foreach (var child in new[] { "sensor-readings", "alerts", "history", "care-activities", $"care-activities/{activityId}/images" })
            Assert.Equal(HttpStatusCode.Forbidden, (await engineer.GetAsync($"{path}/{child}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"{path}/assign", new { expectedRevision = 1, engineerId })).StatusCode);
        var race = await Task.WhenAll(admin.PostAsJsonAsync($"{path}/assign", new { expectedRevision = 1, engineerId }), engineer.PostAsync($"{path}/claim", null));
        Assert.Single(race, x => x.IsSuccessStatusCode); Assert.Single(race, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(2, await db.CropConsultationUpdates.CountAsync(x => x.ReportId == id));
        var activities = await Data(await engineer.GetAsync($"{path}/care-activities"));
        Assert.Equal(activityId, Assert.Single(activities.EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Single((await Data(await engineer.GetAsync($"{path}/care-activities/{activityId}/images"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await engineer.GetAsync($"{path}/care-activities/2147483647/images")).StatusCode);
        foreach (var url in new[] { $"/api/crop-seasons/{seasonId}", $"/api/crop-seasons/{seasonId}/activities", $"/api/farms/{farm.Id}", "/api/alerts" })
            Assert.Equal(HttpStatusCode.Forbidden, (await engineer.GetAsync(url)).StatusCode);
        object Note(int revision) => new { expectedRevision = revision, content = "Manual consultation" };
        await Data(await engineer.PostAsJsonAsync($"{path}/request-information", Note(2)));
        await Data(await owner.PostAsJsonAsync($"{path}/reply", Note(3)));
        await Data(await engineer.PostAsJsonAsync($"{path}/recommend", Note(4)));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync($"{path}/follow-ups", new { expectedRevision = 5, type = "ACTION_TAKEN", content = "Care", cultivationActivityId = int.MaxValue })).StatusCode);
        var followup = new { expectedRevision = 5, type = "ACTION_TAKEN", content = "Applied care", cultivationActivityId = activityId, images };
        await Data(await owner.PostAsJsonAsync($"{path}/follow-ups", followup));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"{path}/follow-ups", followup)).StatusCode);
        await Data(await engineer.PostAsJsonAsync($"{path}/resolve", Note(6)));
        var unchangedSeason = await db.CropSeasons.AsNoTracking().SingleAsync(x => x.Id == seasonId);
        Assert.Equal(CropSeasonStatus.ACTIVE, unchangedSeason.Status); Assert.Equal(2, unchangedSeason.Revision);
        var review = new { expectedRevision = 7, type = "NOT_IMPROVED", content = "Still spotted" };
        var closed = await Data(await owner.PostAsJsonAsync($"{path}/follow-ups", review));
        Assert.Equal("RESOLVED", closed.GetProperty("status").GetString());
        Assert.Equal("REVIEW_REQUESTED", closed.GetProperty("updates")[7].GetProperty("action").GetString());
        Assert.True(await db.Notifications.AnyAsync(x => x.UserId == engineerId && x.Message.Contains($"#{id}:") && x.Message.Contains("REVIEW_REQUESTED")));
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"{path}/reopen", Note(8))).StatusCode);
        await Data(await engineer.PostAsJsonAsync($"{path}/reopen", Note(8)));
        Assert.Equal(2, (await db.CropSeasons.AsNoTracking().SingleAsync(x => x.Id == seasonId)).Revision);
        await Data(await owner.PostAsJsonAsync($"/api/crop-seasons/{seasonId}/complete", new { expectedRevision = 2 }));
        await Data(await owner.PostAsJsonAsync($"{path}/follow-ups", new { expectedRevision = 9, type = "QUESTION", content = "Follow-up on completed season", cultivationActivityId = activityId }));
        Assert.Equal(CropSeasonStatus.COMPLETED, (await db.CropSeasons.AsNoTracking().SingleAsync(x => x.Id == seasonId)).Status);
        await db.UserFarms.Where(x => x.FarmId == farm.Id).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"{path}/follow-ups", new { expectedRevision = 10, type = "AGREED", content = "Agreed" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"{path}/care-activities")).StatusCode);
    }

    [Fact]
    public async Task Assignment_and_referrals_validate_targets_and_never_grant_incident_access_to_engineer()
    {
        using var owner = await Login("owner@example.com"); using var engineer = await Login("engineer@example.com"); using var admin = await Login("admin@example.com");
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm = new Farm { Name = "Referral scope" }; var other = new Farm { Name = "Other referral scope" };
        db.Farms.AddRange(farm, other); await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, "owner@example.com"); await factory.AssignFarmAsync(other.Id, "owner@example.com");
        var engineerId = await db.Users.Where(x => x.Email == "engineer@example.com").Select(x => x.Id).SingleAsync();
        var ownerId = await db.Users.Where(x => x.Email == "owner@example.com").Select(x => x.Id).SingleAsync();
        var created = await owner.PostAsJsonAsync("/api/crop-condition-reports", new { farmId = farm.Id, crop = "Rice", description = "Sensor suspected" }); created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32(); var path = $"/api/crop-condition-reports/{id}";
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync($"{path}/assign", new { expectedRevision = 1, engineerId = ownerId })).StatusCode);
        (await admin.PostAsJsonAsync($"{path}/assign", new { expectedRevision = 1, engineerId })).EnsureSuccessStatusCode();
        Assert.Equal(engineerId, (await db.CropConsultationUpdates.SingleAsync(x => x.ReportId == id && x.Action == CropConsultationAction.ASSIGNED)).AssignedEngineerId);
        (await engineer.PostAsJsonAsync($"{path}/referrals", new { expectedRevision = 2, type = "DEVICE_INCIDENT", content = "Check the device using technical support" })).EnsureSuccessStatusCode();
        async Task<int> Incident(int farmId)
        {
            var response = await owner.PostAsJsonAsync("/api/device-incidents", new { farmId, description = "Possible data fault" }); response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        }
        var wrong = await Incident(other.Id); var valid = await Incident(farm.Id);
        object Link(int incidentId) => new { expectedRevision = 3, type = "DEVICE_INCIDENT", content = "Created incident", deviceIncidentId = incidentId };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync($"{path}/referrals", Link(wrong))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await engineer.PostAsJsonAsync($"{path}/referrals", Link(valid))).StatusCode);
        (await owner.PostAsJsonAsync($"{path}/referrals", Link(valid))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await engineer.GetAsync($"/api/device-incidents/{valid}")).StatusCode);
        (await engineer.PostAsJsonAsync($"{path}/referrals", new { expectedRevision = 4, type = "UAV_VIDEO", content = "Need footage; pending MF02 integration" })).EnsureSuccessStatusCode();
        Assert.False(await db.ServiceJobs.AnyAsync(x => x.IncidentId == valid));
        Assert.Equal(5, await db.CropConsultationUpdates.CountAsync(x => x.ReportId == id));
        // Legacy resolved cases do not require a structured recommendation to request review.
        await db.CropConditionReports.Where(x => x.Id == id).ExecuteUpdateAsync(x => x.SetProperty(r => r.Status, CropConditionStatus.RESOLVED));
        (await owner.PostAsJsonAsync($"{path}/follow-ups", new { expectedRevision = 5, type = "NOT_IMPROVED", content = "Please review" })).EnsureSuccessStatusCode();
    }
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
        Assert.Equal(HttpStatusCode.Forbidden, (await engineer.GetAsync($"{path}/{id}/sensor-readings")).StatusCode);
        (await engineer.PostAsync($"{path}/{id}/claim", null)).EnsureSuccessStatusCode();
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
