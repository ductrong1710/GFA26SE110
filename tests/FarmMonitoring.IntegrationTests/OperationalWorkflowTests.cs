using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;
[Collection("PostgreSQL Auth")]
public sealed class OperationalWorkflowTests(ApiFactory factory)
{
    private static readonly EvidenceImageData Image=new("image/jpeg",Convert.ToBase64String([255,216,255,217]));
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    { Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync()); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data"); }
    private async Task<HttpClient> Login(string email)
    {
        var client=factory.CreateApiClient(); var result=await Data(await client.PostAsJsonAsync("/api/auth/login",new {email,password="Test-password-123!"}));
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",result.GetProperty("accessToken").GetString()); return client;
    }
    [Fact]
    public async Task Paper_service_plan_keeps_assignment_until_final_admin_approval_and_freezes_prices()
    {
        using var admin=await Login("admin@example.com"); using var owner=await Login("owner@example.com");
        var email=$"workflow-{Guid.NewGuid():N}@example.com";
        var tech=await Data(await admin.PostAsJsonAsync("/api/users",new {email,fullName="Workflow technician",password="Test-password-123!",roles=new[]{"Technician"}}));
        var techId=tech.GetProperty("id").GetInt32(); using var technician=await Login(email);
        await using var scope=factory.Services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm=new Farm{Name="Workflow farm",CreatedAt=DateTimeOffset.UtcNow};
        var old=new Uav{Code=Guid.NewGuid().ToString("N"),Name="Old UAV",CreatedAt=DateTimeOffset.UtcNow};
        var next=new Uav{Code=Guid.NewGuid().ToString("N"),Name="New UAV",CreatedAt=DateTimeOffset.UtcNow};
        var gateway=new Gateway{Code=Guid.NewGuid().ToString("N"),Name="Gateway",GatewayType="ESP32",CreatedAt=DateTimeOffset.UtcNow};
        db.Farms.Add(farm);db.Uavs.AddRange(old,next);db.Gateways.Add(gateway);await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id,"owner@example.com");await factory.SeedRentalAsync(farm.Id,old.Id,gateway.Id);
        var assignment=await db.RentalDeviceAssignments.SingleAsync(x=>x.UavId==old.Id);
        var job=new ServiceJob{RentalDeviceAssignmentId=assignment.Id,TechnicianId=techId,Type=ServiceJobType.REPAIR,Status=ServiceJobStatus.IN_PROGRESS,ScheduledAt=DateTimeOffset.UtcNow,ScheduledEndAt=DateTimeOffset.UtcNow.AddHours(2),CreatedAt=DateTimeOffset.UtcNow};
        db.ServiceJobs.Add(job);await db.SaveChangesAsync();var path=$"/api/service-jobs/{job.Id}";
        var price=await Data(await admin.PostAsJsonAsync("/api/admin/public-components",new {title="Antenna",body="Replacement antenna",expectedRevision=0,replacementPriceVnd=200000}));var priceId=price.GetProperty("id").GetInt32();
        await Data(await admin.PostAsJsonAsync($"/api/admin/public-components/{priceId}/publish",new {expectedRevision=1}));
        var proposed=await Data(await technician.PostAsJsonAsync($"/api/operations/service-jobs/{job.Id}/plans",new {expectedRevision=1,note="Approved repair method requested",uavId=next.Id,lines=new[]{new{publicContentId=priceId,quantity=2,warranty=false,reason="Accidental damage"}}}));
        var planId=proposed[0].GetProperty("id").GetInt32();Assert.Equal(400000,proposed[0].GetProperty("totalVnd").GetDecimal());
        Assert.Null((await db.RentalDeviceAssignments.AsNoTracking().SingleAsync(x=>x.Id==assignment.Id)).ReturnedAt);
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.PostAsJsonAsync($"/api/operations/service-plans/{planId}/review",new{expectedRevision=2,approve=true,note="No"})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await technician.PostAsJsonAsync($"/api/operations/service-plans/{planId}/execute",new{expectedRevision=2,signedAgreement=new[]{Image},oldDeviceImages=new[]{Image},newDeviceImages=new[]{Image}})).StatusCode);
        await Data(await admin.PostAsJsonAsync($"/api/operations/service-plans/{planId}/review",new{expectedRevision=2,approve=true,note="Method and warranty checked"}));
        await Data(await admin.PutAsJsonAsync($"/api/admin/public-components/{priceId}",new{title="Antenna",body="New price",expectedRevision=2,replacementPriceVnd=300000}));
        var executed=await Data(await technician.PostAsJsonAsync($"/api/operations/service-plans/{planId}/execute",new{expectedRevision=3,signedAgreement=new[]{Image},oldDeviceImages=new[]{Image},newDeviceImages=new[]{Image}}));Assert.Equal(400000,executed[0].GetProperty("totalVnd").GetDecimal());
        var report=await Data(await technician.PostAsJsonAsync(path+"/reports",new{expectedRevision=4,cause="Broken antenna",work="Replace and test",components="Antenna",testResults="Passed",notes="Paper signed",connectivityPassed=true,operationPassed=true,signedHandover=new[]{Image}}));
        Assert.Null((await db.RentalDeviceAssignments.AsNoTracking().SingleAsync(x=>x.Id==assignment.Id)).ReturnedAt);
        var reportId=report.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path + "/approve",
            new { expectedRevision = 5, reportId, note = "Signed documents only" })).StatusCode);
        Assert.Equal(ServiceJobStatus.REPORT_SUBMITTED, await db.ServiceJobs.Where(x => x.Id == job.Id).Select(x => x.Status).SingleAsync());
        await Data(await owner.PostAsJsonAsync(path + "/confirm-operation",
            new { expectedRevision = 5, reportId, note = "Operation checked by owner" }));
        var ownerId=(await db.Users.SingleAsync(x=>x.Email=="owner@example.com")).Id;
        var mission=new Mission{FarmId=farm.Id,UavId=old.Id,Name="Active flight blocks replacement",Status=MissionStatus.RUNNING,CreatedByUserId=ownerId,CreatedAt=DateTimeOffset.UtcNow};db.Missions.Add(mission);await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync(path+"/approve",new{expectedRevision=6,reportId,note="Flight still active"})).StatusCode);
        Assert.Equal(ServicePlanStatus.EXECUTED,(await db.ServicePlans.AsNoTracking().SingleAsync(x=>x.Id==planId)).Status);
        Assert.Null((await db.RentalDeviceAssignments.AsNoTracking().SingleAsync(x=>x.Id==assignment.Id)).ReturnedAt);
        await db.Missions.Where(x=>x.Id==mission.Id).ExecuteUpdateAsync(x=>x.SetProperty(m=>m.Status,MissionStatus.COMPLETED));
        var attempts=await Task.WhenAll(admin.PostAsJsonAsync(path+"/approve",new{expectedRevision=6,reportId,note="Signed papers, price and tests verified"}),admin.PostAsJsonAsync(path+"/approve",new{expectedRevision=6,reportId,note="Concurrent approval"}));
        Assert.Single(attempts,x=>x.IsSuccessStatusCode);Assert.Single(attempts,x=>x.StatusCode==HttpStatusCode.Conflict);
        var approved=await Data(attempts.Single(x=>x.IsSuccessStatusCode));
        var replacementEvent = approved.GetProperty("history").EnumerateArray().Single(x => x.GetProperty("action").GetString() == "DEVICE_REPLACED");
        Assert.Equal(techId, replacementEvent.GetProperty("technicianId").GetInt32());
        Assert.Equal((await Data(await admin.GetAsync("/api/auth/me"))).GetProperty("id").GetInt32(), replacementEvent.GetProperty("userId").GetInt32());
        Assert.Equal("CLOSED",approved.GetProperty("status").GetString());Assert.Equal(next.Code,approved.GetProperty("currentDeviceCode").GetString());
        Assert.NotNull((await db.RentalDeviceAssignments.AsNoTracking().SingleAsync(x=>x.Id==assignment.Id)).ReturnedAt);
        Assert.False((await db.Uavs.AsNoTracking().SingleAsync(x=>x.Id==old.Id)).IsActive);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync(path+"/approve",new{expectedRevision=6,reportId,note="Duplicate"})).StatusCode);
        Assert.Single(await db.ServiceDeviceReplacements.Where(x=>x.JobId==job.Id).ToArrayAsync());
        await Data(await admin.GetAsync($"/api/operations/device-history?code={old.Code}"));
        var successor = await db.ServiceDeviceReplacements.Where(x => x.JobId == job.Id).Select(x => x.NewAssignmentId).SingleAsync();
        var reopen = new { type = "REPAIR", technicianId = techId, scheduledAt = DateTimeOffset.UtcNow.AddDays(3),
            expectedRevision = approved.GetProperty("revision").GetInt32(), note = "Reinspect replacement equipment", reopen = true };
        await db.RentalDeviceAssignments.Where(x => x.Id == successor).ExecuteUpdateAsync(x => x.SetProperty(a => a.ReturnedAt, DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path + "/assignment", reopen)).StatusCode);
        await db.RentalDeviceAssignments.Where(x => x.Id == successor).ExecuteUpdateAsync(x => x.SetProperty(a => a.ReturnedAt, (DateTimeOffset?)null));
        var rentalStatus = await db.RentalContracts.Where(x => x.Id == assignment.ContractId).Select(x => x.Status).SingleAsync();
        await db.RentalContracts.Where(x => x.Id == assignment.ContractId).ExecuteUpdateAsync(x => x.SetProperty(c => c.Status, RentalStatus.ENDED));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path + "/assignment", reopen)).StatusCode);
        await db.RentalContracts.Where(x => x.Id == assignment.ContractId).ExecuteUpdateAsync(x => x.SetProperty(c => c.Status, rentalStatus));
        var reopened = await Data(await admin.PostAsJsonAsync(path + "/assignment", reopen));
        Assert.Equal("ASSIGNED", reopened.GetProperty("status").GetString());
        Assert.Equal(next.Code, reopened.GetProperty("currentDeviceCode").GetString());
        Assert.Single(await db.ServiceDeviceReplacements.Where(x => x.JobId == job.Id).ToArrayAsync());
        Assert.True((await Data(await owner.GetAsync(path + $"/reports/{reportId}"))).GetProperty("approved").GetBoolean());
        var revision = reopened.GetProperty("revision").GetInt32();
        await Data(await technician.PostAsJsonAsync(path + "/start", new { expectedRevision = revision++ }));
        var inspection = await Data(await technician.PostAsJsonAsync($"/api/operations/service-jobs/{job.Id}/plans",
            new { expectedRevision = revision++, note = "Inspect the new UAV without replacing it", lines = Array.Empty<object>() }));
        var inspectionPath = $"/api/operations/service-plans/{inspection[0].GetProperty("id").GetInt32()}";
        await Data(await admin.PostAsJsonAsync(inspectionPath + "/review", new { expectedRevision = revision++, approve = true, note = "Inspect only" }));
        await Data(await technician.PostAsJsonAsync(inspectionPath + "/execute", new { expectedRevision = revision++, signedAgreement = new[] { Image } }));
        var inspectionReport = await Data(await technician.PostAsJsonAsync(path + "/reports", new {
            expectedRevision = revision++, cause = "Follow-up inspection", work = "Checked replacement antenna", components = "None",
            testResults = "Passed", notes = "No additional parts", connectivityPassed = true, operationPassed = true, signedHandover = new[] { Image }
        }));
        var replacementId = (await Data(await owner.GetAsync(path + $"/reports/{reportId}"))).GetProperty("replacement").GetProperty("id").GetInt32();
        Assert.Equal(replacementId, inspectionReport.GetProperty("replacement").GetProperty("id").GetInt32());
        var inspectionId = inspectionReport.GetProperty("id").GetInt32();
        await Data(await owner.PostAsJsonAsync(path + "/confirm-operation", new { expectedRevision = revision++, reportId = inspectionId, note = "Confirmed inspection" }));
        await Data(await admin.PostAsJsonAsync(path + "/approve", new { expectedRevision = revision, reportId = inspectionId, note = "Inspection complete" }));
        Assert.Equal(replacementId, (await Data(await owner.GetAsync(path + $"/reports/{inspectionId}"))).GetProperty("replacement").GetProperty("id").GetInt32());
        Assert.Single(await db.ServiceDeviceReplacements.Where(x => x.JobId == job.Id).ToArrayAsync());
        Assert.Equal(next.Code, (await Data(await owner.GetAsync(path + $"/reports/{reportId}"))).GetProperty("replacement").GetProperty("newDeviceCode").GetString());
    }
    [Fact]
    public async Task Installation_uses_paper_contract_next_day_rental_and_admin_can_change_busy_technician()
    {
        using var admin=await Login("admin@example.com");using var engineer=await Login("engineer@example.com");using var owner=await Login("owner@example.com");using var guest=factory.CreateApiClient();
        var techIds=new List<int>();var emails=new List<string>();
        for(var i=0;i<2;i++) {var email=$"install-flow-{Guid.NewGuid():N}@example.com";emails.Add(email);techIds.Add((await Data(await admin.PostAsJsonAsync("/api/users",new{email,fullName="Installation technician",password="Test-password-123!",roles=new[]{"Technician"}}))).GetProperty("id").GetInt32());}
        using var tech=await Login(emails[1]);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm=new Farm{Name="Paper installation farm",CreatedAt=DateTimeOffset.UtcNow};var uav=new Uav{Code=Guid.NewGuid().ToString("N"),Name="Installed UAV",CreatedAt=DateTimeOffset.UtcNow};db.Farms.Add(farm);db.Uavs.Add(uav);await db.SaveChangesAsync();await factory.AssignFarmAsync(farm.Id,"owner@example.com");
        var lead=(await Data(await guest.PostAsJsonAsync("/api/consultations",new{customerName="Paper customer",phone="0901234567",farmLocation="Farm",serviceNeed="UAV",content="Rent UAV"}))).GetProperty("id").GetInt32();
        var survey=await Data(await engineer.PostAsJsonAsync($"/api/consultations/{lead}/surveys",new{expectedRevision=0,farmName="Farm",farmLocation="Address",monitoringNeeds="UAV",zones=new[]{new{name="North",crop="Mango"}}}));
        Assert.Equal("",survey.GetProperty("zones")[0].GetProperty("installationLocation").GetString());Assert.Equal(JsonValueKind.Null,survey.GetProperty("zones")[0].GetProperty("cropAgeDays").ValueKind);
        var ownerId=(await Data(await owner.GetAsync("/api/auth/me"))).GetProperty("id").GetInt32();await Data(await admin.PutAsJsonAsync($"/api/consultations/{lead}/customer-farm",new{ownerId,farmId=farm.Id}));
        var product=(await Data(await admin.PostAsJsonAsync("/api/admin/public-devices",new{title="Rental UAV",body="UAV rental",estimatedMonthlyRentVnd=1000000,expectedRevision=0}))).GetProperty("id").GetInt32();await Data(await admin.PostAsJsonAsync($"/api/admin/public-devices/{product}/publish",new{expectedRevision=1}));
        var proposal=await Data(await engineer.PostAsJsonAsync($"/api/consultations/{lead}/proposals",new{expectedRevision=0,surveyId=survey.GetProperty("id").GetInt32(),rentalMonths=3,requestedInstallationAt=DateTimeOffset.UtcNow.AddDays(2),lines=new[]{new{surveyZoneId=survey.GetProperty("zones")[0].GetProperty("id").GetInt32(),deviceType="UAV",quantity=1,publicContentId=product}}}));
        var proposalId=proposal.GetProperty("id").GetInt32();var basePath=$"/api/consultations/{lead}/proposals/{proposalId}";
        await Data(await engineer.PostAsJsonAsync(basePath + "/confirm-plan", new { note = "Rental agreed externally" }));
        await Data(await engineer.PostAsJsonAsync(basePath + "/confirm-deposit", new { note = "Deposit receipt verified" }));
        await Data(await engineer.PostAsJsonAsync($"/api/installation-dossiers/{proposalId}/submit",new{expectedRevision=1,note="Customer agreed and deposited externally",bills=new[]{Image}}));
        var request=await Data(await admin.PostAsJsonAsync($"/api/installation-dossiers/{proposalId}/review",new{expectedRevision=1,approve=true,note="Farm and bill checked"}));var requestId=request.GetProperty("installationRequestId").GetInt32();
        var start=DateTimeOffset.UtcNow.AddDays(2);var end=start.AddHours(2);
        var job=await Data(await admin.PostAsJsonAsync($"/api/installation-requests/{requestId}/jobs",new{technicianId=techIds[0],scheduledAt=start,scheduledEndAt=end,note="Visit"}));var jobId=job.GetProperty("id").GetInt32();var path=$"/api/installation-jobs/{jobId}";
        var from=Uri.EscapeDataString(start.ToString("O"));var to=Uri.EscapeDataString(end.ToString("O"));var available=await Data(await admin.GetAsync($"/api/operations/available-technicians?from={from}&to={to}"));Assert.DoesNotContain(available.GetProperty("technicians").EnumerateArray(),x=>x.GetProperty("id").GetInt32()==techIds[0]);
        await Data(await admin.PostAsJsonAsync(path+"/assignment",new{expectedRevision=1,technicianId=techIds[1],scheduledAt=start,scheduledEndAt=end,note="Original technician unexpectedly busy"}));
        await Data(await tech.PostAsJsonAsync(path+"/start",new{expectedRevision=2}));var lineId=proposal.GetProperty("lines")[0].GetProperty("id").GetInt32();
        await Data(await admin.PutAsJsonAsync(path+"/devices",new{expectedRevision=3,note="Allocated",devices=new[]{new{proposalLineId=lineId,uavId=uav.Id}}}));
        await Data(await tech.GetAsync($"/api/operations/installation/{jobId}/installed-devices"));
        var report=await Data(await tech.PostAsJsonAsync(path+"/reports",new{expectedRevision=4,notes="Tested and handed over",devices=new[]{new{proposalLineId=lineId,uavId=uav.Id,installationLocation="Actual surveyed position",accessoriesChecked=true,connectivityPassed=true,operationPassed=true,testResults="Passed"}},paperwork=new{contract=new[]{Image},handover=new[]{Image},bills=new[]{Image},paperContractNumber="PAPER-01"}}));
        var reportId = report.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path + "/approve",
            new { expectedRevision = 5, reportId, note = "Signed documents only" })).StatusCode);
        Assert.False(await db.RentalContracts.AnyAsync(x => x.InstallationJobId == jobId));
        await Data(await owner.PostAsJsonAsync(path + "/confirm-handover",
            new { expectedRevision = 5, reportId, note = "Installation checked by owner" }));
        await Data(await admin.PostAsJsonAsync(path + "/approve",
            new { expectedRevision = 6, reportId, note = "Paper documents verified" }));
        var rental=await db.RentalContracts.AsNoTracking().SingleAsync(x=>x.InstallationJobId==jobId);Assert.Equal(OperationalRules.RentalStart(report.GetProperty("completedAt").GetDateTimeOffset()),rental.StartAt);
        var detail=await Data(await admin.GetAsync($"/api/rental-contracts/{rental.Id}"));Assert.Equal(3000000,detail.GetProperty("totalRentVnd").GetDecimal());
        await Data(await admin.GetAsync($"/api/operations/calendar?from={from}&to={to}"));
        var incident=await Data(await owner.PostAsJsonAsync("/api/device-incidents",new{rentalDeviceAssignmentId=(await db.RentalDeviceAssignments.SingleAsync(x=>x.ContractId==rental.Id)).Id,description="No data"}));
        // Use a separate open appointment to verify cross-type overlap, then a free technician.
        var blocked= new ServiceJob{RentalDeviceAssignmentId=(await db.RentalDeviceAssignments.SingleAsync(x=>x.ContractId==rental.Id)).Id,TechnicianId=techIds[0],ScheduledAt=start,ScheduledEndAt=end,CreatedAt=DateTimeOffset.UtcNow};db.ServiceJobs.Add(blocked);await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync($"/api/device-incidents/{incident.GetProperty("id").GetInt32()}/jobs",new{type="REPAIR",technicianId=techIds[0],scheduledAt=start,note="Overlapping visit"})).StatusCode);
    }
    [Fact]
    public async Task Anonymous_leads_are_visible_without_claim_and_unknown_device_report_needs_no_serial()
    {
        using var guest=factory.CreateApiClient();using var admin=await Login("admin@example.com");using var owner=await Login("owner@example.com");
        var name="Lead-"+Guid.NewGuid().ToString("N");
        await Data(await guest.PostAsJsonAsync("/api/consultations",new{customerName=name,phone="0901234567",farmLocation="Farm address",serviceNeed="Monitoring",content="Contact me"}));
        var from=Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));var to=Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var leads=await Data(await admin.GetAsync($"/api/operations/consultation-requests?from={from}&to={to}&search={name}"));Assert.Equal(1,leads.GetProperty("total").GetInt32());
        await Data(await admin.GetAsync($"/api/operations/dashboard?from={from}&to={to}"));
        Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync($"/api/operations/dashboard?from={from}&to={to}")).StatusCode);
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var farm=new Farm{Name="No signal farm",CreatedAt=DateTimeOffset.UtcNow};db.Farms.Add(farm);await db.SaveChangesAsync();await factory.AssignFarmAsync(farm.Id,"owner@example.com");
        var incident=await Data(await owner.PostAsJsonAsync("/api/device-incidents",new{farmId=farm.Id,description="No sensor data; I cannot identify device"}));Assert.Equal(JsonValueKind.Null,incident.GetProperty("rentalDeviceAssignmentId").ValueKind);
    }
}
