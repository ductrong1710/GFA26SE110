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
public class DeviceIncidentTests(ApiFactory factory)
{
    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateApiClient();
        var token = await Data(await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("accessToken").GetString());
        return client;
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    [Fact]
    public async Task Incident_evidence_notifications_mission_scope_and_return_history_are_preserved()
    {
        using var owner = await Login("owner@example.com");
        using var admin = await Login("admin@example.com");
        using var engineer = await Login("engineer@example.com");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm = new Farm { Name = "Incident farm", CreatedAt = DateTimeOffset.UtcNow };
        var uav = new Uav { Code = Guid.NewGuid().ToString("N"), Name = "Incident UAV", Status = "OFFLINE", CreatedAt = DateTimeOffset.UtcNow };
        var gateway = new Gateway { Code = Guid.NewGuid().ToString("N"), Name = "Incident gateway", GatewayType = "ESP32", CreatedAt = DateTimeOffset.UtcNow };
        db.Farms.Add(farm); db.Uavs.Add(uav); db.Gateways.Add(gateway); await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, "owner@example.com");
        await factory.SeedRentalAsync(farm.Id, uav.Id, gateway.Id);
        var assignment = await db.RentalDeviceAssignments.SingleAsync(x => x.UavId == uav.Id);
        var ownerId = (await Data(await owner.GetAsync("/api/auth/me"))).GetProperty("id").GetInt32();
        var mission = new Mission { FarmId = farm.Id, UavId = uav.Id, GatewayId = gateway.Id, Name = "Failed mission", Status = MissionStatus.FAILED, CreatedByUserId = ownerId, CreatedAt = DateTimeOffset.UtcNow };
        db.Missions.Add(mission); await db.SaveChangesAsync();
        await db.RentalContracts.Where(x => x.Id == assignment.ContractId).ExecuteUpdateAsync(x => x.SetProperty(r => r.Status, RentalStatus.EXPIRED));
        const string png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS3sAAAAASUVORK5CYII=";
        var request = new { rentalDeviceAssignmentId = assignment.Id, description = "Motor stopped", missionId = mission.Id, images = new[] { new { contentType = "image/png", dataBase64 = png } } };
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/device-incidents", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await engineer.GetAsync("/api/device-incidents")).StatusCode);
        using var anonymous = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/device-incidents")).StatusCode);
        var response = await owner.PostAsJsonAsync("/api/device-incidents", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var incident = await Data(response); var id = incident.GetProperty("id").GetInt32();
        Assert.Equal("OPEN", incident.GetProperty("status").GetString());
        Assert.Equal(mission.Id, incident.GetProperty("missionId").GetInt32());
        Assert.Equal(png, incident.GetProperty("images")[0].GetProperty("dataBase64").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/device-incidents/{id}")).StatusCode);
        var email = $"service-{Guid.NewGuid():N}@example.com";
        var tech = await Data(await admin.PostAsJsonAsync("/api/users", new { email, fullName = "Service technician", password = "Test-password-123!", roles = new[] { "Technician" } }));
        var techId = tech.GetProperty("id").GetInt32();
        using var technician = await Login(email);
        var appointment = DateTimeOffset.UtcNow.AddDays(1);
        var assignmentRequest = new { type = "REPAIR", technicianId = techId, scheduledAt = appointment, note = "Inspect failed motor" };
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"/api/device-incidents/{id}/jobs", assignmentRequest)).StatusCode);
        var job = await Data(await admin.PostAsJsonAsync($"/api/device-incidents/{id}/jobs", assignmentRequest with {scheduledAt=appointment.AddDays(5)}));
        var jobPath = $"/api/service-jobs/{job.GetProperty("id").GetInt32()}";
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/device-incidents/{id}/jobs", assignmentRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await engineer.GetAsync(jobPath)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(jobPath)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await technician.GetAsync(jobPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.GetAsync($"/api/farms/{farm.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(jobPath + "/start", new { expectedRevision = 1 })).StatusCode);
        var startedJob = await Data(await technician.PostAsJsonAsync(jobPath + "/start", new { expectedRevision = 1 }));
        Assert.Equal("IN_PROGRESS", startedJob.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await technician.PostAsJsonAsync(jobPath + "/start", new { expectedRevision = 1 })).StatusCode);
        var secondEmail = $"service-{Guid.NewGuid():N}@example.com";
        var secondTech = await Data(await admin.PostAsJsonAsync("/api/users", new { email = secondEmail, fullName = "Replacement technician", password = "Test-password-123!", roles = new[] { "Technician" } }));
        var secondId = secondTech.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(jobPath + "/assignment", new { type = "REPAIR", technicianId = secondId, scheduledAt = appointment, note = "Stale revision", expectedRevision = 1 })).StatusCode);
        var reassigned = await Data(await admin.PostAsJsonAsync(jobPath + "/assignment", new { type = "REPAIR", technicianId = secondId, scheduledAt = appointment, note = "Rescheduled with replacement technician", expectedRevision = 2 }));
        Assert.Equal(3, reassigned.GetProperty("history").GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.GetAsync(jobPath)).StatusCode);
        using var replacement = await Login(secondEmail);
        Assert.Equal(HttpStatusCode.OK, (await replacement.GetAsync(jobPath)).StatusCode);
        (await replacement.PostAsJsonAsync(jobPath + "/start", new { expectedRevision = 3 })).EnsureSuccessStatusCode();
        db.ServicePlans.Add(new ServicePlan{JobId=job.GetProperty("id").GetInt32(),OldAssignmentId=assignment.Id,TechnicianId=secondId,Status=ServicePlanStatus.EXECUTED,Note="Approved fixture method",CreatedAt=DateTimeOffset.UtcNow,EvidenceJson="{}"});await db.SaveChangesAsync();
        var reportRequest = new { signedHandover=request.images,expectedRevision = 4, cause = "Motor connector loosened", work = "Reseated connector", components = "No replacement parts", testResults = "Connection restored; motor still stalls", notes = "Further work required", connectivityPassed = true, operationPassed = false, images = request.images };
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.PostAsJsonAsync(jobPath + "/reports", reportRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(jobPath + "/reports", reportRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await replacement.PostAsJsonAsync(jobPath + "/reports", reportRequest with { images = new[] { new { contentType = "image/png", dataBase64 = "invalid" } } })).StatusCode);
        var submissions = await Task.WhenAll(replacement.PostAsJsonAsync(jobPath + "/reports", reportRequest), replacement.PostAsJsonAsync(jobPath + "/reports", reportRequest));
        Assert.Single(submissions, x => x.IsSuccessStatusCode);
        Assert.Single(submissions, x => x.StatusCode == HttpStatusCode.Conflict);
        var report = await Data(submissions.Single(x => x.IsSuccessStatusCode));
        var reportId = report.GetProperty("id").GetInt32();
        Assert.Equal(1, report.GetProperty("revision").GetInt32());
        Assert.Equal(png, report.GetProperty("images")[0].GetProperty("dataBase64").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await replacement.PostAsJsonAsync(jobPath + "/reports", reportRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.GetAsync(jobPath + $"/reports/{reportId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await engineer.GetAsync(jobPath + "/reports")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(jobPath + $"/reports/{reportId}")).StatusCode);
        var otherIncident = await Data(await owner.PostAsJsonAsync("/api/device-incidents", request));
        var otherJob = await Data(await admin.PostAsJsonAsync($"/api/device-incidents/{otherIncident.GetProperty("id").GetInt32()}/jobs", assignmentRequest with {scheduledAt=appointment.AddDays(5)}));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/service-jobs/{otherJob.GetProperty("id").GetInt32()}/reports/{reportId}")).StatusCode);
        var decision = new { expectedRevision = 5, reportId, note = "Reviewed submitted checks" };
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(jobPath + "/approve", decision)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await replacement.PostAsJsonAsync(jobPath + "/confirm-operation", decision)).StatusCode);
        (await admin.DeleteAsync($"/api/farms/{farm.Id}/members/{ownerId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(jobPath + "/confirm-operation", decision)).StatusCode);
        await factory.AssignFarmAsync(farm.Id, "owner@example.com");
        var confirmed = await Data(await owner.PostAsJsonAsync(jobPath + "/confirm-operation", decision));
        Assert.Equal("OWNER_CONFIRMED", confirmed.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(jobPath + "/confirm-operation", decision)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(jobPath + "/approve", decision with { expectedRevision = 6 })).StatusCode);
        Assert.Equal(6, (await Data(await admin.GetAsync(jobPath))).GetProperty("revision").GetInt32());
        var returned = await Data(await admin.PostAsJsonAsync(jobPath + "/return", decision with { expectedRevision = 6, note = "Resolve the failed motor check" }));
        Assert.Equal("REWORK", returned.GetProperty("status").GetString());
        Assert.Equal("OPEN", (await Data(await owner.GetAsync($"/api/device-incidents/{id}"))).GetProperty("status").GetString());
        (await admin.PostAsJsonAsync(jobPath + "/assignment", new { type = "REPAIR", technicianId = techId, scheduledAt = appointment, note = "Assign rework to available technician", expectedRevision = 7 })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await replacement.GetAsync(jobPath + $"/reports/{reportId}")).StatusCode);
        (await technician.PostAsJsonAsync(jobPath + "/start", new { expectedRevision = 8 })).EnsureSuccessStatusCode();
        var corrected = await Data(await technician.PostAsJsonAsync(jobPath + "/reports", reportRequest with { expectedRevision = 9, work = "Secured motor connector", testResults = "Connectivity and motor checks pass", operationPassed = true, notes = "Ready for acceptance" }));
        var correctedId = corrected.GetProperty("id").GetInt32();
        Assert.Equal(2, corrected.GetProperty("revision").GetInt32());
        Assert.Equal(JsonValueKind.Null, corrected.GetProperty("confirmedAt").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(jobPath + "/confirm-operation", decision with { expectedRevision = 10 })).StatusCode);
        (await owner.PostAsJsonAsync(jobPath + "/confirm-operation", decision with { expectedRevision = 10, reportId = correctedId })).EnsureSuccessStatusCode();
        var closed = await Data(await admin.PostAsJsonAsync(jobPath + "/approve", decision with { expectedRevision = 11, reportId = correctedId }));
        Assert.Equal("CLOSED", closed.GetProperty("status").GetString());
        Assert.Equal("RESOLVED", (await Data(await owner.GetAsync($"/api/device-incidents/{id}"))).GetProperty("status").GetString());
        Assert.Equal("OPEN", (await Data(await owner.GetAsync($"/api/device-incidents/{otherIncident.GetProperty("id").GetInt32()}"))).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(jobPath + "/return", decision with { expectedRevision = 12, reportId = correctedId })).StatusCode);
        var originalReport = await Data(await owner.GetAsync(jobPath + $"/reports/{reportId}"));
        Assert.False(originalReport.GetProperty("operationPassed").GetBoolean());
        Assert.False(originalReport.GetProperty("approved").GetBoolean());
        Assert.Equal("Reseated connector", originalReport.GetProperty("work").GetString());
        Assert.Equal(png, originalReport.GetProperty("images")[0].GetProperty("dataBase64").GetString());
        var revisit = new { type = "REPAIR", technicianId = techId, scheduledAt = appointment.AddDays(8),
            expectedRevision = closed.GetProperty("revision").GetInt32(), note = "Owner reported the fault again", reopen = true };
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(jobPath + "/assignment", revisit)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.PostAsJsonAsync(jobPath + "/assignment", revisit)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(jobPath + "/assignment", revisit with { reopen = false })).StatusCode);
        var reopened = await Data(await admin.PostAsJsonAsync(jobPath + "/assignment", revisit));
        Assert.Equal("ASSIGNED", reopened.GetProperty("status").GetString());
        Assert.Equal("REOPENED", reopened.GetProperty("history").EnumerateArray().Last().GetProperty("action").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(jobPath + "/assignment", revisit)).StatusCode);
        Assert.Equal("OPEN", (await Data(await owner.GetAsync($"/api/device-incidents/{id}"))).GetProperty("status").GetString());
        Assert.True((await Data(await owner.GetAsync(jobPath + $"/reports/{correctedId}"))).GetProperty("approved").GetBoolean());
        var revision = reopened.GetProperty("revision").GetInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(jobPath + "/assignment", revisit with { expectedRevision = revision })).StatusCode);
        await Data(await technician.PostAsJsonAsync(jobPath + "/start", new { expectedRevision = revision++ }));
        Assert.Equal(HttpStatusCode.Conflict, (await technician.PostAsJsonAsync(jobPath + "/reports", reportRequest with { expectedRevision = revision, operationPassed = true })).StatusCode);
        var plan = await Data(await technician.PostAsJsonAsync($"/api/operations/service-jobs/{job.GetProperty("id").GetInt32()}/plans",
            new { expectedRevision = revision++, note = "Inspect recurring connector fault", lines = Array.Empty<object>() }));
        var planPath = $"/api/operations/service-plans/{plan[0].GetProperty("id").GetInt32()}";
        await Data(await admin.PostAsJsonAsync(planPath + "/review", new { expectedRevision = revision++, approve = true, note = "Approve repeat inspection" }));
        await Data(await technician.PostAsJsonAsync(planPath + "/execute", new { expectedRevision = revision++, signedAgreement = request.images }));
        var repeatReport = await Data(await technician.PostAsJsonAsync(jobPath + "/reports", reportRequest with { expectedRevision = revision++, operationPassed = true, work = "Replaced connector clamp" }));
        var repeatId = repeatReport.GetProperty("id").GetInt32();
        await Data(await owner.PostAsJsonAsync(jobPath + "/confirm-operation", decision with { expectedRevision = revision++, reportId = repeatId }));
        var reclosed = await Data(await admin.PostAsJsonAsync(jobPath + "/approve", decision with { expectedRevision = revision++, reportId = repeatId }));
        Assert.Equal("CLOSED", reclosed.GetProperty("status").GetString());
        Assert.Equal("RESOLVED", (await Data(await owner.GetAsync($"/api/device-incidents/{id}"))).GetProperty("status").GetString());
        Assert.True((await Data(await owner.GetAsync(jobPath + $"/reports/{correctedId}"))).GetProperty("approved").GetBoolean());
        var admins = await db.Users.Where(x => x.IsActive && x.UserRoles.Any(r => r.Role.Name == FarmMonitoring.Domain.Constants.RoleNames.FarmAdministrator)).Select(x => x.Id).ToArrayAsync();
        var notifications = await db.Notifications.Where(x => x.Subject == "Device incident reported" && x.Message.Contains($"#{id} for device")).ToArrayAsync();
        Assert.Equal(admins.Length * 2, notifications.Length);
        foreach (var adminId in admins)
            foreach (var channel in new[] { NotificationChannel.WEB, NotificationChannel.EMAIL })
                Assert.Single(notifications, x => x.UserId == adminId && x.Channel == channel);
        var count = await db.DeviceIncidents.CountAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/device-incidents", new { rentalDeviceAssignmentId = assignment.Id, description = "Bad image", images = new[] { new { contentType = "image/png", dataBase64 = "invalid" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync("/api/device-incidents", new { rentalDeviceAssignmentId = assignment.Id, description = "Wrong mission", missionId = int.MaxValue })).StatusCode);
        Assert.Equal(count, await db.DeviceIncidents.CountAsync());
        await db.RentalDeviceAssignments.Where(x => x.Id == assignment.Id).ExecuteUpdateAsync(x => x.SetProperty(r => r.ReturnedAt, DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(jobPath + "/assignment", revisit with { expectedRevision = revision })).StatusCode);
        Assert.Equal("CLOSED", (await Data(await admin.GetAsync(jobPath))).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PostAsJsonAsync("/api/device-incidents", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/device-incidents/{id}")).StatusCode);
        (await admin.DeleteAsync($"/api/farms/{farm.Id}/members/{ownerId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/device-incidents/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(jobPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(jobPath + $"/reports/{reportId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/device-incidents", request)).StatusCode);
        Assert.DoesNotContain((await Data(await owner.GetAsync("/api/device-incidents"))).EnumerateArray(), x => x.GetProperty("id").GetInt32() == id);
        Assert.Equal(png, (await Data(await admin.GetAsync($"/api/device-incidents/{id}"))).GetProperty("images")[0].GetProperty("dataBase64").GetString());
    }
}
