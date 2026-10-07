using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class FarmAccessTests(ApiFactory factory)
{
    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" });
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString());
        return client;
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }
    [Fact]
    public async Task Membership_controls_access_and_removal_is_immediate()
    {
        using var admin = await Login("admin@example.com");
        using var owner = await Login("owner@example.com");
        var me = await Data(await owner.GetAsync("/api/auth/me"));
        var userId = me.GetProperty("id").GetInt32();
        var farm = await Data(await admin.PostAsJsonAsync("/api/farms", new { name = "Restricted-" + Guid.NewGuid() }));
        var id = farm.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/farms/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/farms/{id}/members", new { userId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/farms/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/farms/{id}/members", new { userId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"/api/farms/{id}/members", new { userId })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/farms/{id}/members/{userId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/farms/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/api/farms/2147483647")).StatusCode);
    }
}
