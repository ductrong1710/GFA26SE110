using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class AuthBoundaryTests(ApiFactory factory)
{
    [Fact]
    public async Task Me_without_bearer_token_returns_401()
    {
        using var client = factory.CreateApiClient();
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

