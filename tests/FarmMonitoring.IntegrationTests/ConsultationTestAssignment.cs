using System.Net.Http.Json;
using System.Text.Json;

namespace FarmMonitoring.IntegrationTests;

internal static class ConsultationTestAssignment
{
    internal static async Task<string> CreateEngineerAsync(HttpClient admin)
    {
        var email = $"consultation-{Guid.NewGuid():N}@example.com";
        using var response = await admin.PostAsJsonAsync("/api/users",
            new { email, fullName = "Consultation engineer", password = "Test-password-123!", roles = new[] { "FarmEngineer" } });
        response.EnsureSuccessStatusCode();
        return email;
    }

    internal static async Task<int> EngineerIdAsync(HttpClient engineer) =>
        (await engineer.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("data").GetProperty("id").GetInt32();

    internal static async Task AssignAsync(HttpClient admin, HttpClient engineer, string lead)
    {
        var engineerId = await EngineerIdAsync(engineer);
        using var response = await admin.PostAsJsonAsync(lead + "/assign", new { engineerId });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        await AcceptAsync(engineer, lead, response);
    }

    internal static async Task AcceptAsync(HttpClient engineer, string lead, HttpResponseMessage assignment)
    {
        var id = (await assignment.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data")
            .GetProperty("assignment").GetProperty("id").GetInt32();
        using var accepted = await engineer.PostAsJsonAsync(lead + "/assignment-response", new { assignmentId = id, accept = true });
        Assert.True(accepted.IsSuccessStatusCode, await accepted.Content.ReadAsStringAsync());
    }
}
