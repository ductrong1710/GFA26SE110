using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public sealed class PublicContentApiTests(ApiFactory factory)
{
    [Fact]
    public async Task Admin_manages_publication_with_revision_checks_and_owner_is_denied()
    {
        const string path = "/api/admin/public-devices";
        using var client = factory.CreateApiClient();
        var body = new { expectedRevision = 0, kind = "DEVICE", title = "Rental sensor", body = "For field monitoring", estimatedMonthlyRentVnd = 12000 };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, body)).StatusCode);
        async Task Login(string email)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" });
            response.EnsureSuccessStatusCode();
            var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString();
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        }
        await Login("owner@example.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, body)).StatusCode);
        await Login("admin@example.com");
        var created = await client.PostAsJsonAsync(path, body);
        created.EnsureSuccessStatusCode();
        var draft = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.False(draft.GetProperty("published").GetBoolean());
        var item = $"{path}/{draft.GetProperty("id").GetInt32()}";
        (await client.PostAsJsonAsync($"{item}/publish", new { expectedRevision = 1 })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{item}/unpublish", new { expectedRevision = 1 })).StatusCode);
        (await client.PostAsJsonAsync($"{item}/unpublish", new { expectedRevision = 2 })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Anonymous_reader_sees_only_published_entries_and_images_even_after_unpublishing()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var visible = new PublicContent { Kind = PublicContentKind.DEVICE, Title = "Soil monitor", Body = "Soil moisture", Published = true,
            EstimatedMonthlyRentVnd = 0, ImageType = "image/png", Image = [1, 2, 3] };
        var draft = new PublicContent { Kind = PublicContentKind.WARRANTY, Title = "Draft policy", Body = "Under review",
            ImageType = "image/jpeg", Image = [4] };
        db.PublicContent.AddRange(visible, draft);
        await db.SaveChangesAsync();
        using var visitor = factory.CreateApiClient();
        const string path = "/api/public-devices";
        var response = await visitor.GetFromJsonAsync<JsonElement>(path);
        var entry = Assert.Single(response.GetProperty("data").EnumerateArray());
        Assert.Equal(visible.Id, entry.GetProperty("id").GetInt32());
        Assert.Equal(0, entry.GetProperty("estimatedMonthlyRentVnd").GetDecimal());
        Assert.Equal(visible.Image, await visitor.GetByteArrayAsync($"{path}/{visible.Id}/image"));
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"{path}/{draft.Id}/image")).StatusCode);
        visible.Published = false;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"{path}/{visible.Id}/image")).StatusCode);
    }
}
