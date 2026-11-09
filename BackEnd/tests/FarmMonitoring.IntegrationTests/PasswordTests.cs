using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Concurrent;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.IntegrationTests;

public sealed class PasswordTests : IAsyncLifetime
{
    private readonly ApiFactory factory = new();
    private readonly Mailbox mail = new();
    private HttpClient client = null!;
    private const string Old = "Test-password-123!";
    private const string New = "Changed-password-456!";
    private sealed class Mailbox : IEmailSender
    {
        public ConcurrentQueue<EmailMessage> Messages { get; } = new();
        public SemaphoreSlim Received { get; } = new(0);
        public TaskCompletionSource? Hold { get; set; }
        public async Task SendAsync(EmailMessage message, CancellationToken ct)
        {
            Messages.Enqueue(message); Received.Release();
            if (Hold is not null) await Hold.Task.WaitAsync(ct);
        }
    }
    public async Task InitializeAsync()
    {
        await factory.InitializeAsync();
        client = factory.WithWebHostBuilder(b => {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Email:Enabled"]="true", ["Email:Host"]="localhost", ["Email:FromAddress"]="test@example.com"
            }));
            b.ConfigureServices(s => s.AddSingleton<IEmailSender>(mail));
        }).CreateClient();
    }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }
    private async Task<string> ResetToken()
    {
        Assert.True(await mail.Received.WaitAsync(TimeSpan.FromSeconds(5)));
        return Assert.Single(mail.Messages).Body.Split('\n').Last().Trim();
    }
    private async Task<JsonElement> Login(string password = Old)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new {email="owner@example.com",password});
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }
    [Fact] public async Task Change_requires_current_password_and_revokes_old_sessions()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/auth/change-password",new {currentPassword=Old,newPassword=New})).StatusCode);
        var login=await Login();
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",login.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/auth/change-password",new {currentPassword="wrong",newPassword=New})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/auth/change-password",new {currentPassword=Old,newPassword=New})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/auth/refresh",new {refreshToken=login.GetProperty("refreshToken").GetString()})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/auth/login",new {email="owner@example.com",password=Old})).StatusCode);
        var fresh=await Login(New);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/auth/refresh",new {refreshToken=fresh.GetProperty("refreshToken").GetString()})).StatusCode);
    }
    [Fact] public async Task Reset_is_private_single_use_and_revokes_sessions()
    {
        var login=await Login();
        var known=await client.PostAsJsonAsync("/api/auth/forgot-password",new {email=" OWNER@example.com "});
        var unknown=await client.PostAsJsonAsync("/api/auth/forgot-password",new {email="missing@example.com"});
        Assert.Equal(HttpStatusCode.OK,known.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(),await unknown.Content.ReadAsStringAsync());
        var token=await ResetToken();
        Assert.DoesNotContain(token,await known.Content.ReadAsStringAsync());
        var reset=await client.PostAsJsonAsync("/api/auth/reset-password",new {token,newPassword=New});
        Assert.Equal(HttpStatusCode.OK,reset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/auth/reset-password",new {token,newPassword=Old})).StatusCode);
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",login.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/auth/refresh",new {refreshToken=login.GetProperty("refreshToken").GetString()})).StatusCode);
        await Login(New);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Create_rejects_admin_role_including_mixed_roles(bool mixed)
    {
        var response=await client.PostAsJsonAsync("/api/auth/login",new {email="admin@example.com",password=Old});
        var login=(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",login.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/users",new {email="new@example.com",fullName="New",password=Old,roles=mixed?new[]{"FarmOwner","FarmAdministrator"}:new[]{"FarmAdministrator"}})).StatusCode);
    }

    [Fact] public async Task Reset_rejects_expired_tokens_and_limits_email_resends()
    {
        await client.PostAsJsonAsync("/api/auth/forgot-password",new {email="owner@example.com"});
        await client.PostAsJsonAsync("/api/auth/forgot-password",new {email="owner@example.com"});
        await client.PostAsJsonAsync("/api/auth/forgot-password",new {email="disabled@example.com"});
        var token=await ResetToken();
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user=await db.Users.SingleAsync(x=>x.Email=="owner@example.com");
        Assert.NotEqual(token,user.PasswordResetHash);
        Assert.Equal(64,user.PasswordResetHash!.Length);
        user.PasswordResetExpiresAt=DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/auth/reset-password",new {token,newPassword=New})).StatusCode);
        await Login();
    }

    [Fact] public async Task Concurrent_reset_consumes_token_exactly_once()
    {
        await client.PostAsJsonAsync("/api/auth/forgot-password",new {email="owner@example.com"});
        var token=await ResetToken();
        var results=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>client.PostAsJsonAsync("/api/auth/reset-password",new {token,newPassword=New})));
        Assert.Single(results,x=>x.StatusCode==HttpStatusCode.OK);
        Assert.Single(results,x=>x.StatusCode==HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("forgot-password", "{}")]
    [InlineData("forgot-password", "{\"email\":\"bad\"}")]
    [InlineData("reset-password", "{}")]
    [InlineData("reset-password", "{\"token\":\"invalid\",\"newPassword\":\"Changed-password-456!\"}")]
    public async Task Invalid_password_requests_return_400(string route,string json)
    {
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/api/auth/"+route,new StringContent(json,System.Text.Encoding.UTF8,"application/json"))).StatusCode);
    }

    [Fact] public async Task Forgot_response_does_not_wait_for_smtp()
    {
        mail.Hold=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var response=client.PostAsJsonAsync("/api/auth/forgot-password",new {email="owner@example.com"});
            Assert.True(await mail.Received.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(HttpStatusCode.OK,(await response.WaitAsync(TimeSpan.FromSeconds(2))).StatusCode);
        }
        finally { mail.Hold.SetResult(); }
    }

    [Fact] public async Task Concurrent_refresh_and_reset_never_preserve_an_old_session()
    {
        var login=await Login();
        await client.PostAsJsonAsync("/api/auth/forgot-password",new {email="owner@example.com"});
        var token=await ResetToken();
        var reset=client.PostAsJsonAsync("/api/auth/reset-password",new {token,newPassword=New});
        var refresh=client.PostAsJsonAsync("/api/auth/refresh",new {refreshToken=login.GetProperty("refreshToken").GetString()});
        await Task.WhenAll(reset,refresh);
        Assert.Equal(HttpStatusCode.OK,(await reset).StatusCode);
        var refreshed=await refresh;
        Assert.Contains(refreshed.StatusCode,new[]{HttpStatusCode.OK,HttpStatusCode.Unauthorized});
        if (refreshed.IsSuccessStatusCode)
        {
            var data=(await refreshed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",data.GetProperty("accessToken").GetString());
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/auth/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/auth/refresh",new {refreshToken=data.GetProperty("refreshToken").GetString()})).StatusCode);
        }
    }

    [Fact] public async Task Change_invalidates_an_outstanding_reset_token()
    {
        var login=await Login();
        await client.PostAsJsonAsync("/api/auth/forgot-password",new {email="owner@example.com"});
        var token=await ResetToken();
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",login.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/auth/change-password",new {currentPassword=Old,newPassword=New})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/auth/reset-password",new {token,newPassword=Old})).StatusCode);
    }

    [Fact] public async Task Reissuing_after_cooldown_invalidates_previous_token()
    {
        await client.PostAsJsonAsync("/api/auth/forgot-password",new {email="owner@example.com"});
        var oldToken=await ResetToken();
        await using (var scope=factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(x=>x.Email=="owner@example.com")
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.PasswordResetRequestedAt,DateTimeOffset.UtcNow.AddMinutes(-2)));
        await client.PostAsJsonAsync("/api/auth/forgot-password",new {email="owner@example.com"});
        Assert.True(await mail.Received.WaitAsync(TimeSpan.FromSeconds(5)));
        var newToken=mail.Messages.Last().Body.Split('\n').Last().Trim();
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/auth/reset-password",new {token=oldToken,newPassword=New})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/auth/reset-password",new {token=newToken,newPassword=New})).StatusCode);
    }
}
