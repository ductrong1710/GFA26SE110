using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace FarmMonitoring.IntegrationTests;
public sealed class FlightTestTests : IAsyncLifetime
{
    private readonly ApiFactory factory = new();
    private HttpClient admin = null!, device = null!;
    private int gatewayId;
    private string Root => $"/api/gateways/{gatewayId}/flight-tests";
    private string Device => $"/api/device/gateways/{gatewayId}/flight-tests";
    public async Task InitializeAsync()
    {
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gateway = new Gateway { Code="flight-gateway", Name="Flight", GatewayType="ESP32", Status="ONLINE", CreatedAt=DateTimeOffset.UtcNow,
            Uav=new Uav { Code="flight-uav", Name="Tello", Status="ONLINE", CreatedAt=DateTimeOffset.UtcNow } };
        db.Gateways.Add(gateway); await db.SaveChangesAsync(); gatewayId=gateway.Id;
        await factory.SeedDeviceCredentialAsync(gatewayId);
        admin=await Login("admin@example.com"); device=factory.CreateApiClient();
        device.DefaultRequestHeaders.Add("X-Gateway-Code", "flight-gateway"); device.DefaultRequestHeaders.Add("X-Api-Key",factory.DeviceKey);
    }
    public async Task DisposeAsync() { admin?.Dispose(); device?.Dispose(); await factory.DisposeAsync(); }
    private async Task<HttpClient> Login(string email) { var c=factory.CreateApiClient(); var d=await Data(await c.PostAsJsonAsync("/api/auth/login",new {email,password="Test-password-123!"})); c.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",d.GetProperty("accessToken").GetString()); return c; }
    private static async Task<JsonElement> Data(HttpResponseMessage r) { Assert.True(r.IsSuccessStatusCode, $"{r.StatusCode}: {await r.Content.ReadAsStringAsync()}"); return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data"); }
    [Fact] public async Task Create_claim_complete_and_retries_preserve_command_and_evidence()
    {
        var commands=await Task.WhenAll(Enumerable.Range(0,3).Select(_=>admin.PostAsJsonAsync(Root,new {requestKey="same"})));
        var responses=await Task.WhenAll(commands.Select(Data));
        var id=responses[0].GetProperty("id").GetInt32();
        foreach(var c in responses) Assert.Equal(id,c.GetProperty("id").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync(Root,new {requestKey="different"})).StatusCode);
        Assert.Equal(id,(await Data(await device.GetAsync(Device+"/next"))).GetProperty("id").GetInt32());
        for(var i=0;i<2;i++) Assert.Equal("CLAIMED",(await Data(await device.PostAsJsonAsync($"{Device}/{id}/claim",new {}))).GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null,(await Data(await device.GetAsync(Device+"/next"))).ValueKind);
        var result=new {status="COMPLETED",takeoffAttempted=true,gatewayConnected=true,uavConnected=true,landed=true,observedAltitudeCm=100,heldMs=3000,errorCode=(string?)null};
        for(var i=0;i<2;i++) Assert.Equal("COMPLETED",(await Data(await device.PostAsJsonAsync($"{Device}/{id}/result",result))).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict,(await device.PostAsJsonAsync($"{Device}/{id}/result",result with {heldMs=3001})).StatusCode);
    }
    [Fact] public async Task Human_and_device_authentication_and_current_admin_role_are_enforced()
    {
        using var anonymous=factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync(Root,new {requestKey="anon"})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await admin.GetAsync(Device+"/next")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await device.PostAsJsonAsync(Root,new {requestKey="device"})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await device.GetAsync($"/api/device/gateways/{gatewayId+1}/flight-tests/next")).StatusCode);
        foreach(var email in new[]{"owner@example.com","engineer@example.com"}) { using var c=await Login(email); Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsJsonAsync(Root,new {requestKey=email})).StatusCode); }
        await using var scope=factory.Services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.UserRoles.Where(x=>x.User.Email=="admin@example.com").ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden,(await admin.PostAsJsonAsync(Root,new {requestKey="revoked"})).StatusCode);
    }

    [Fact] public async Task Expired_commands_cannot_be_claimed_and_never_reappear()
    {
        var id=(await Data(await admin.PostAsJsonAsync(Root,new {requestKey="expire"}))).GetProperty("id").GetInt32();
        await using(var scope=factory.Services.CreateAsyncScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.FlightTests.Where(x=>x.Id==id).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.ExpiresAt,DateTimeOffset.UtcNow.AddSeconds(-1)));
        }
        Assert.Equal(HttpStatusCode.Conflict,(await device.PostAsJsonAsync($"{Device}/{id}/claim",new {})).StatusCode);
        Assert.Equal(JsonValueKind.Null,(await Data(await device.GetAsync(Device+"/next"))).ValueKind);
        var retry=await Data(await admin.PostAsJsonAsync(Root,new {requestKey="expire"}));
        Assert.Equal(id,retry.GetProperty("id").GetInt32());Assert.Equal("EXPIRED",retry.GetProperty("status").GetString());
        Assert.NotEqual(id,(await Data(await admin.PostAsJsonAsync(Root,new {requestKey="fresh"}))).GetProperty("id").GetInt32());
    }

    [Fact] public async Task Claimed_and_recovery_commands_reserve_equipment_and_validate_evidence()
    {
        var created=await Data(await admin.PostAsJsonAsync(Root,new {requestKey="reserve"}));
        int id=created.GetProperty("id").GetInt32(),uavId=created.GetProperty("uavId").GetInt32();
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync($"/api/gateways/{gatewayId}/assign-uav",new {uavId=(int?)null})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PatchAsJsonAsync($"/api/uavs/{uavId}/status",new {status="OFFLINE",isActive=false})).StatusCode);
        await Data(await device.PostAsJsonAsync($"{Device}/{id}/claim",new {}));
        var invalid=new {status="COMPLETED",takeoffAttempted=true,gatewayConnected=true,uavConnected=true,landed=true,observedAltitudeCm=100,heldMs=2999,errorCode=(string?)null};
        Assert.Equal(HttpStatusCode.BadRequest,(await device.PostAsJsonAsync($"{Device}/{id}/result",invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await device.PostAsJsonAsync($"{Device}/{id}/result",invalid with {heldMs=3000,landed=false})).StatusCode);
        await Data(await device.PostAsJsonAsync($"{Device}/{id}/result",new {status="RECOVERY_REQUIRED",takeoffAttempted=true,gatewayConnected=true,uavConnected=true,landed=false,observedAltitudeCm=70,heldMs=0,errorCode="LINK_LOST"}));
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync(Root,new {requestKey="blocked"})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await device.PostAsJsonAsync($"{Device}/{id}/claim",new {})).StatusCode);
    }

    [Fact] public async Task Interrupted_claim_without_takeoff_can_be_closed_without_fabricated_landing()
    {
        var id=(await Data(await admin.PostAsJsonAsync(Root,new {requestKey="interrupted"}))).GetProperty("id").GetInt32();
        var result=new {status="FAILED",takeoffAttempted=false,gatewayConnected=true,uavConnected=false,landed=false,observedAltitudeCm=0,heldMs=0,errorCode="CLAIM_RESPONSE_UNCERTAIN"};
        Assert.Equal("FAILED",(await Data(await device.PostAsJsonAsync($"{Device}/{id}/result",result))).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict,(await device.PostAsJsonAsync($"{Device}/{id}/claim",new {})).StatusCode);
        Assert.Equal("QUEUED",(await Data(await admin.PostAsJsonAsync(Root,new {requestKey="next"}))).GetProperty("status").GetString());
    }

    [Fact] public async Task Admin_cancels_manual_claim_with_confirmation_and_preserves_unknown_telemetry()
    {
        var id=(await Data(await admin.PostAsJsonAsync(Root,new {requestKey="manual-claim"}))).GetProperty("id").GetInt32();
        await Data(await device.PostAsJsonAsync($"{Device}/{id}/claim",new {}));
        var body = new { reason="Manually claimed in Swagger; never started", confirmAircraftGrounded=true, confirmNeverStarted=true };
        using var owner=await Login("owner@example.com");
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.PostAsJsonAsync($"{Root}/{id}/cancel",body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await device.PostAsJsonAsync($"{Root}/{id}/cancel",body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsJsonAsync($"{Root}/{id}/cancel",body with {confirmNeverStarted=false})).StatusCode);
        var cancelled=await Data(await admin.PostAsJsonAsync($"{Root}/{id}/cancel",body));
        Assert.Equal("CANCELLED",cancelled.GetProperty("status").GetString());
        Assert.StartsWith("ADMIN_CANCELLED:",cancelled.GetProperty("errorCode").GetString());
        Assert.Equal(JsonValueKind.Null,cancelled.GetProperty("landed").ValueKind);
        Assert.Equal(JsonValueKind.Null,cancelled.GetProperty("takeoffAttempted").ValueKind);
        Assert.Equal("CANCELLED",(await Data(await admin.PostAsJsonAsync($"{Root}/{id}/cancel",body))).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict,(await device.PostAsJsonAsync($"{Device}/{id}/claim",new {})).StatusCode);
        Assert.Equal("QUEUED",(await Data(await admin.PostAsJsonAsync(Root,new {requestKey="released"}))).GetProperty("status").GetString());
    }

    [Fact] public async Task Admin_cannot_cancel_flight_recovery_as_a_manual_claim()
    {
        var id=(await Data(await admin.PostAsJsonAsync(Root,new {requestKey="recovery-cancel"}))).GetProperty("id").GetInt32();
        await Data(await device.PostAsJsonAsync($"{Device}/{id}/claim",new {}));
        await Data(await device.PostAsJsonAsync($"{Device}/{id}/result",new {status="RECOVERY_REQUIRED",takeoffAttempted=true,gatewayConnected=true,uavConnected=true,landed=false,observedAltitudeCm=70,heldMs=0,errorCode="LINK_LOST"}));
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync($"{Root}/{id}/cancel",new {reason="Cannot bypass recovery",confirmAircraftGrounded=true,confirmNeverStarted=true})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync(Root,new {requestKey="still-reserved"})).StatusCode);
    }

    [Fact] public async Task Concurrent_gateways_sharing_uav_cannot_both_reserve_it()
    {
        int otherId;
        await using(var scope=factory.Services.CreateAsyncScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gateway=await db.Gateways.SingleAsync(x=>x.Id==gatewayId);
            var other=new Gateway {Code="other-flight-gateway",Name="Other",GatewayType="ESP32",Status="ONLINE",UavId=gateway.UavId,CreatedAt=DateTimeOffset.UtcNow};
            db.Gateways.Add(other);await db.SaveChangesAsync();otherId=other.Id;
        }
        var results=await Task.WhenAll(admin.PostAsJsonAsync(Root,new {requestKey="first"}),
            admin.PostAsJsonAsync($"/api/gateways/{otherId}/flight-tests",new {requestKey="second"}));
        Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Accepted);
        Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);
    }
}
