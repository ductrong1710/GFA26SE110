using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class AccountLifecycleTests(ApiFactory factory)
{
    [Fact]
    public async Task Login_upgrades_password_hash_and_disabled_user_cannot_refresh_or_get_me()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User { Email = "lifecycle@example.com", FullName = "Before", CreatedAt = DateTimeOffset.UtcNow };
        user.PasswordHash = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions { IterationCount = 1000 }))
            .HashPassword(user, "Lifecycle-password!");
        var oldHash = user.PasswordHash;
        user.UserRoles.Add(new UserRole { RoleId = 1 });
        user.UserRoles.Add(new UserRole { RoleId = 3 });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        using var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Lifecycle-password!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(2, data.GetProperty("user").GetProperty("roles").GetArrayLength());
        await db.Entry(user).ReloadAsync();
        Assert.NotEqual(oldHash, user.PasswordHash);
        Assert.NotNull(user.UpdatedAt);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        user.FullName = "After";
        await db.SaveChangesAsync();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("After", me.GetProperty("data").GetProperty("fullName").GetString());
        user.IsActive = false;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = data.GetProperty("refreshToken").GetString()
        })).StatusCode);
    }
}
