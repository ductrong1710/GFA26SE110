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
public sealed class CropSeasonApiTests(ApiFactory factory)
{
    [Fact]
    public async Task Support_intake_requires_matching_owned_season_and_explicit_observation_offset()
    {
        using var owner = await Login("owner@example.com");
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerId = await db.Users.Where(x => x.Email == "owner@example.com").Select(x => x.Id).SingleAsync();
        var otherOwner = await db.Users.Where(x => x.Email == "admin@example.com").Select(x => x.Id).SingleAsync();
        var farm = new Farm { Name = "Season intake scope" }; var second = new Farm { Name = "Second season farm" };
        db.Farms.AddRange(farm, second); await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, "owner@example.com"); await factory.AssignFarmAsync(second.Id, "owner@example.com");
        var zone = new Zone { FarmId = farm.Id, Name = "North" }; db.Zones.Add(zone); await db.SaveChangesAsync();
        var season = new CropSeason { FarmId = farm.Id, ZoneId = zone.Id, OwnerId = ownerId, Name = "Rice season", Crop = "Rice" };
        var privateSeason = new CropSeason { FarmId = farm.Id, ZoneId = zone.Id, OwnerId = otherOwner, Name = "Private season", Crop = "Rice" };
        db.CropSeasons.AddRange(season, privateSeason); await db.SaveChangesAsync();
        const string path = "/api/crop-condition-reports";
        object Body(int farmId, int? zoneId, int seasonId, string crop = "Rice", string? observedAt = null)
            => new { farmId, zoneId, cropSeasonId = seasonId, crop, observedAt, description = "Spots" };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync(path, Body(second.Id, zone.Id, season.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync(path, Body(farm.Id, null, season.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync(path, Body(farm.Id, zone.Id, season.Id, "Corn"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(path, Body(farm.Id, zone.Id, privateSeason.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path, Body(farm.Id, zone.Id, season.Id, observedAt: "2026-01-01T10:00:00"))).StatusCode);
        var response = await owner.PostAsJsonAsync(path, Body(farm.Id, zone.Id, season.Id)); response.EnsureSuccessStatusCode();
        var detail = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("observedAt").ValueKind);
        var reportId = detail.GetProperty("id").GetInt32();
        await db.CropSeasons.Where(x => x.Id == season.Id).ExecuteUpdateAsync(x => x.SetProperty(s => s.Crop, "Updated crop"));
        Assert.Equal("Rice", (await db.CropConditionReports.SingleAsync(x => x.Id == reportId)).Crop);
        var filtered = await owner.GetFromJsonAsync<JsonElement>($"/api/crop-seasons?farmId={farm.Id}&zoneId={zone.Id}&pageSize=1");
        Assert.Equal(season.Id, Assert.Single(filtered.GetProperty("data").EnumerateArray()).GetProperty("id").GetInt32());
        var whole = await owner.GetFromJsonAsync<JsonElement>($"/api/crop-seasons?farmId={farm.Id}&wholeFarmOnly=true");
        Assert.Empty(whole.GetProperty("data").EnumerateArray());
        await db.UserFarms.Where(x => x.FarmId == farm.Id).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(path, Body(farm.Id, zone.Id, season.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/crop-seasons?zoneId={zone.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/api/crop-seasons?zoneId=2147483647")).StatusCode);
    }
    [Fact]
    public async Task Comparison_preserves_unknown_values_and_requires_access_to_every_selected_season()
    {
        using var owner = await Login("owner@example.com");
        using var admin = await Login("admin@example.com");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == "owner@example.com");
        var farms = new[] { new Farm { Name = "Season comparison A" }, new Farm { Name = "Season comparison B" } };
        db.Farms.AddRange(farms);
        await db.SaveChangesAsync();
        foreach (var farm in farms) await factory.AssignFarmAsync(farm.Id, user.Email);
        var known = new CropSeason { FarmId = farms[0].Id, OwnerId = user.Id, Name = "Spring", Crop = "Rice",
            StartDate = new(2026, 3, 1), EndDate = new(2026, 3, 11), Status = CropSeasonStatus.COMPLETED,
            CompletedAt = DateTimeOffset.UtcNow, TotalCostVnd = 200, RevenueVnd = 0, YieldKg = 0 };
        var unknown = new CropSeason { FarmId = farms[1].Id, OwnerId = user.Id, Name = "Autumn", Crop = "Rice" };
        unknown.Activities.Add(new() { Date = new(2026, 11, 1), Type = CultivationActivityType.WATERING,
            Description = "Watered field", UserId = user.Id, CostVnd = 100 });
        db.CropSeasons.AddRange(known, unknown);
        await db.SaveChangesAsync();
        const string path = "/api/crop-seasons/compare";
        var url = $"{path}?ids={unknown.Id}&ids={known.Id}";
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync($"{path}?ids={known.Id}&ids={known.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(url)).StatusCode);
        var rows = (await owner.GetFromJsonAsync<JsonElement>(url)).GetProperty("data");
        Assert.Equal(unknown.Id, rows[0].GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, rows[0].GetProperty("profitVnd").ValueKind);
        Assert.Equal(JsonValueKind.Null, rows[0].GetProperty("durationDays").ValueKind);
        Assert.Equal(JsonValueKind.Null, rows[0].GetProperty("totalCostVnd").ValueKind);
        var watering = Assert.Single(rows[0].GetProperty("activities").EnumerateArray());
        Assert.Equal("WATERING", watering.GetProperty("type").GetString());
        Assert.Equal(1, watering.GetProperty("count").GetInt32());
        Assert.Equal(-200m, rows[1].GetProperty("profitVnd").GetDecimal());
        Assert.Equal(10, rows[1].GetProperty("durationDays").GetInt32());
        await db.UserFarms.Where(x => x.FarmId == farms[0].Id).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(url)).StatusCode);
    }
    [Fact]
    public async Task Activity_and_images_follow_current_season_access_and_revision()
    {
        using var owner = await Login("owner@example.com");
        using var admin = await Login("admin@example.com");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm = new Farm { Name = "Activity farm", CreatedAt = DateTimeOffset.UtcNow };
        db.Farms.Add(farm);
        await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, "owner@example.com");
        var created = await owner.PostAsJsonAsync("/api/crop-seasons", new { farmId = farm.Id, crop = "Rice", name = "Autumn" });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        var url = $"/api/crop-seasons/{id}/activities";
        object Note(int revision, string image) => new { expectedRevision = revision, date = "2026-11-02", type = "INSPECTION", description = "Checked leaves",
            images = new[] { new { contentType = "image/jpeg", dataBase64 = image } } };
        var note = Note(1, "/9j/2Q==");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync(url, note)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(url, Note(1, "bad"))).StatusCode);
        var writes = await Task.WhenAll(owner.PostAsJsonAsync(url, note), owner.PostAsJsonAsync(url, note));
        Assert.Single(writes, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(writes, x => x.StatusCode == HttpStatusCode.Conflict);
        var list = await admin.GetFromJsonAsync<JsonElement>(url);
        var item = Assert.Single(list.GetProperty("data").EnumerateArray());
        Assert.Equal(1, item.GetProperty("imageCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("costVnd").ValueKind);
        var imageUrl = $"{url}/{item.GetProperty("id").GetInt32()}/images";
        var evidence = await owner.GetFromJsonAsync<JsonElement>(imageUrl);
        Assert.Equal("/9j/2Q==", Assert.Single(evidence.GetProperty("data").EnumerateArray()).GetProperty("dataBase64").GetString());
        (await owner.PostAsJsonAsync($"/api/crop-seasons/{id}/complete", new { expectedRevision = 2 })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(url, Note(3, "/9j/2Q=="))).StatusCode);
        await db.UserFarms.Where(x => x.FarmId == farm.Id).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(imageUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(url, note)).StatusCode);
    }
    [Fact]
    public async Task Only_current_owner_writes_and_concurrent_edits_have_one_winner()
    {
        using var owner = await Login("owner@example.com");
        using var admin = await Login("admin@example.com");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm = new Farm { Name = "Season API", CreatedAt = DateTimeOffset.UtcNow };
        db.Farms.Add(farm);
        await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, "owner@example.com");
        const string path = "/api/crop-seasons";
        object Body(int revision, string crop) => new { expectedRevision = revision, farmId = farm.Id, name = "Spring", crop };
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync(path, Body(0, "Rice"))).StatusCode);
        var response = await owner.PostAsJsonAsync(path, Body(0, "Rice"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var season = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(JsonValueKind.Null, season.GetProperty("startDate").ValueKind);
        var id = season.GetProperty("id").GetInt32();
        var url = $"{path}/{id}";
        var edits = await Task.WhenAll(owner.PutAsJsonAsync(url, Body(1, "Corn")), owner.PutAsJsonAsync(url, Body(1, "Peas")));
        Assert.Single(edits, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(edits, x => x.StatusCode == HttpStatusCode.Conflict);
        var detail = (await admin.GetFromJsonAsync<JsonElement>(url)).GetProperty("data");
        Assert.Equal(2, detail.GetProperty("revision").GetInt32());
        var list = await owner.GetFromJsonAsync<JsonElement>(path);
        Assert.Contains(list.GetProperty("data").EnumerateArray(), x => x.GetProperty("id").GetInt32() == id);
        var withStart = new { expectedRevision = 2, farmId = farm.Id, name = "Spring", crop = "Rice", startDate = "2026-03-01" };
        (await owner.PutAsJsonAsync(url, withStart)).EnsureSuccessStatusCode();
        var outcome = new { expectedRevision = 3, yieldKg = 0, totalCostVnd = 0, notes = "No harvest" };
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"{url}/complete", outcome)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync($"{url}/complete",
            new { expectedRevision = 3, endDate = "2026-02-28" })).StatusCode);
        var completions = await Task.WhenAll(owner.PostAsJsonAsync($"{url}/complete", outcome), owner.PostAsJsonAsync($"{url}/complete", outcome));
        var completed = Assert.Single(completions, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(completions, x => x.StatusCode == HttpStatusCode.Conflict);
        var result = (await completed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("COMPLETED", result.GetProperty("status").GetString());
        Assert.Equal(4, result.GetProperty("revision").GetInt32());
        Assert.Equal(0m, result.GetProperty("yieldKg").GetDecimal());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("revenueVnd").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("endDate").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync(url, Body(4, "Rice"))).StatusCode);
        await db.UserFarms.Where(x => x.FarmId == farm.Id).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PutAsJsonAsync(url, Body(2, "Rice"))).StatusCode);
    }
    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" }); response.EnsureSuccessStatusCode();
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString()); return client;
    }
}
