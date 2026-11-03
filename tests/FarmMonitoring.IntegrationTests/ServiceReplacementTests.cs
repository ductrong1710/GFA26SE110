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
public sealed class ServiceReplacementTests(ApiFactory factory)
{
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    { Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync()); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data"); }
    private async Task<HttpClient> Login(string email)
    { var client=factory.CreateApiClient();var data=await Data(await client.PostAsJsonAsync("/api/auth/login",new{email,password="Test-password-123!"}));client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",data.GetProperty("accessToken").GetString());return client; }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Plans_enforce_device_kind_farm_reservation_and_release_rejected_targets(bool sensor)
    {
        using var admin=await Login("admin@example.com");using var owner=await Login("owner@example.com");
        var email=$"plan-kind-{Guid.NewGuid():N}@example.com";var techId=(await Data(await admin.PostAsJsonAsync("/api/users",new{email,fullName="Plan technician",password="Test-password-123!",roles=new[]{"Technician"}}))).GetProperty("id").GetInt32();using var tech=await Login(email);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm=new Farm{Name="Typed plan farm",CreatedAt=DateTimeOffset.UtcNow};var other=new Farm{Name="Other farm",CreatedAt=DateTimeOffset.UtcNow};
        var uav=new Uav{Code=Guid.NewGuid().ToString("N"),Name="UAV",CreatedAt=DateTimeOffset.UtcNow};var oldGateway=new Gateway{Code=Guid.NewGuid().ToString("N"),Name="Old gateway",GatewayType="ESP32",CreatedAt=DateTimeOffset.UtcNow};var nextGateway=new Gateway{Code=Guid.NewGuid().ToString("N"),Name="New gateway",GatewayType="ESP32",CreatedAt=DateTimeOffset.UtcNow};
        db.Farms.AddRange(farm,other);db.Uavs.Add(uav);db.Gateways.AddRange(oldGateway,nextGateway);await db.SaveChangesAsync();
        var zone=new Zone{FarmId=farm.Id,Name="Field",CreatedAt=DateTimeOffset.UtcNow};var outside=new Zone{FarmId=other.Id,Name="Outside",CreatedAt=DateTimeOffset.UtcNow};db.Zones.AddRange(zone,outside);await db.SaveChangesAsync();
        var nodes=new[]{zone.Id,zone.Id,outside.Id}.Select(id=>new SensorNode{ZoneId=id,DeviceCode=Guid.NewGuid().ToString("N"),Name="Sensor",CreatedAt=DateTimeOffset.UtcNow}).ToArray();db.SensorNodes.AddRange(nodes);await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id,"owner@example.com");await factory.SeedRentalAsync(farm.Id,uav.Id,oldGateway.Id);var old=await db.RentalDeviceAssignments.SingleAsync(x=>x.GatewayId==oldGateway.Id);
        if(sensor){old.GatewayId=null;old.SensorNodeId=nodes[0].Id;var allocation=await db.InstallationDeviceAllocations.SingleAsync(x=>x.Id==old.InstallationAllocationId);allocation.GatewayId=null;allocation.SensorNodeId=nodes[0].Id;await db.SaveChangesAsync();}
        var job=new ServiceJob{RentalDeviceAssignmentId=old.Id,TechnicianId=techId,Status=ServiceJobStatus.IN_PROGRESS,Type=ServiceJobType.REPAIR,ScheduledAt=DateTimeOffset.UtcNow,ScheduledEndAt=DateTimeOffset.UtcNow.AddHours(2),CreatedAt=DateTimeOffset.UtcNow};db.ServiceJobs.Add(job);await db.SaveChangesAsync();
        var path=$"/api/operations/service-jobs/{job.Id}/plans";
        object Plan(int revision,int? sensorId,int? gatewayId,int? uavId=null)=>new{expectedRevision=revision,note="Replacement method",sensorNodeId=sensorId,gatewayId,uavId,lines=Array.Empty<object>()};
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await tech.PostAsJsonAsync(path,Plan(1,null,null,uav.Id))).StatusCode);
        if(sensor)Assert.Equal(HttpStatusCode.Conflict,(await tech.PostAsJsonAsync(path,Plan(1,nodes[2].Id,null))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await tech.PostAsJsonAsync(path,Plan(1,sensor?nodes[0].Id:null,sensor?null:oldGateway.Id))).StatusCode);
        var payload=Plan(1,sensor?nodes[1].Id:null,sensor?null:nextGateway.Id);var attempts=await Task.WhenAll(tech.PostAsJsonAsync(path,payload),tech.PostAsJsonAsync(path,payload));Assert.Single(attempts,x=>x.IsSuccessStatusCode);Assert.Single(attempts,x=>x.StatusCode==HttpStatusCode.Conflict);
        var planId=(await Data(attempts.Single(x=>x.IsSuccessStatusCode)))[0].GetProperty("id").GetInt32();
        Assert.Null((await db.RentalDeviceAssignments.AsNoTracking().SingleAsync(x=>x.Id==old.Id)).ReturnedAt);
        await Data(await admin.PostAsJsonAsync($"/api/operations/service-plans/{planId}/review",new{expectedRevision=2,approve=false,note="Select revised method"}));
        await Data(await tech.PostAsJsonAsync(path,Plan(3,sensor?nodes[1].Id:null,sensor?null:nextGateway.Id)));
        Assert.Equal(HttpStatusCode.Conflict,(await tech.PostAsJsonAsync($"/api/service-jobs/{job.Id}/replacements",new{expectedRevision=4,sensorNodeId=sensor?nodes[1].Id:(int?)null,gatewayId=sensor?null:(int?)nextGateway.Id,oldDeviceRetrieved=true,note="Legacy immediate write forbidden"})).StatusCode);
    }
}
