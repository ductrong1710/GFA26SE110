using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public sealed class ConsultationTests(ApiFactory factory)
{
    private const string EvidencePng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS3sAAAAASUVORK5CYII=";
    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" });
        response.EnsureSuccessStatusCode();
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        return client;
    }

    [Fact]
    public async Task Visitor_submission_is_private_and_assigned_engineer_records_follow_up_history()
    {
        using var visitor = factory.CreateApiClient();
        var submitted = await visitor.PostAsJsonAsync("/api/consultations", new
        {
            customerName = "Consultation customer", phone = "+84901234567", email = "customer@example.com",
            farmLocation = "Can Tho", farmArea = 1.5m, serviceNeed = "Sensor rental", content = "Monitor soil moisture"
        });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var receipt = (await submitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var id = receipt.GetProperty("id").GetInt32();
        Assert.Equal("NEW", receipt.GetProperty("status").GetString());
        Assert.False(receipt.TryGetProperty("phone", out _));
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync($"/api/consultations/{id}")).StatusCode);
        using var owner = await Login("owner@example.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/consultations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"/api/consultations/{id}/claim", new { })).StatusCode);
        using var engineer = await Login("engineer@example.com");
        var notifications = await engineer.GetFromJsonAsync<JsonElement>("/api/notifications?pageSize=100");
        Assert.Contains(notifications.GetProperty("data").EnumerateArray(), x =>
            x.GetProperty("message").GetString()!.Contains($"Consultation #{id} "));
        Assert.Equal(HttpStatusCode.Forbidden, (await engineer.GetAsync($"/api/consultations/{id}")).StatusCode);
        var inbox = await engineer.GetFromJsonAsync<JsonElement>("/api/consultations?inbox=true&pageSize=100");
        var item = inbox.GetProperty("data").EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == id);
        Assert.False(item.TryGetProperty("phone", out _));
        (await engineer.PostAsJsonAsync($"/api/consultations/{id}/claim", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await engineer.PostAsJsonAsync($"/api/consultations/{id}/claim", new { })).StatusCode);
        (await engineer.PostAsJsonAsync($"/api/consultations/{id}/follow-ups", new
        { notes = "Called customer", contactResult = "Customer answered", nextAction = "Arrange survey" })).EnsureSuccessStatusCode();
        var detail = (await engineer.GetFromJsonAsync<JsonElement>($"/api/consultations/{id}")).GetProperty("data");
        Assert.Equal("IN_PROGRESS", detail.GetProperty("status").GetString());
        Assert.Equal("+84901234567", detail.GetProperty("phone").GetString());
        Assert.Equal(3, detail.GetProperty("history").GetArrayLength());
        Assert.Equal("Arrange survey", detail.GetProperty("history")[2].GetProperty("nextAction").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await engineer.PostAsJsonAsync($"/api/consultations/{id}/follow-ups",
            new { notes = "", contactResult = "", nextAction = "" })).StatusCode);
    }

    [Fact]
    public async Task Concurrent_claims_have_one_winner_and_other_engineer_cannot_read_or_write()
    {
        using var admin = await Login("admin@example.com");
        var email = $"consultant-{Guid.NewGuid():N}@example.com";
        (await admin.PostAsJsonAsync("/api/users", new { email, fullName = "Second engineer", password = "Test-password-123!", roles = new[] { "FarmEngineer" } })).EnsureSuccessStatusCode();
        using var first = await Login("engineer@example.com");
        using var second = await Login(email);
        using var visitor = factory.CreateApiClient();
        var response = await visitor.PostAsJsonAsync("/api/consultations", new
        { customerName = "Concurrent claim", phone = "0901234567", farmLocation = "Farm", serviceNeed = "Monitoring", content = "Consultation" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        var claims = await Task.WhenAll(first.PostAsJsonAsync($"/api/consultations/{id}/claim", new { }), second.PostAsJsonAsync($"/api/consultations/{id}/claim", new { }));
        Assert.Single(claims, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(claims, x => x.StatusCode == HttpStatusCode.Conflict);
        var loser = claims[0].IsSuccessStatusCode ? second : first;
        Assert.Equal(HttpStatusCode.Forbidden, (await loser.GetAsync($"/api/consultations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await loser.PostAsJsonAsync($"/api/consultations/{id}/follow-ups",
            new { notes = "Unauthorized", contactResult = "Contacted", nextAction = "Survey" })).StatusCode);
    }

    [Fact]
    public async Task Survey_revisions_preserve_unknown_values_history_and_reject_stale_or_unassigned_writes()
    {
        using var visitor = factory.CreateApiClient();
        var created = await visitor.PostAsJsonAsync("/api/consultations", new
        { customerName = "Survey customer", phone = "0901234567", farmLocation = "Can Tho", serviceNeed = "Sensors", content = "Survey needed" });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        using var engineer = await Login("engineer@example.com");
        object Request(int revision, string notes, string image = EvidencePng) => new
        {
            expectedRevision = revision, farmName = "Customer farm", farmLocation = "Can Tho", farmArea = (decimal?)null,
            monitoringNeeds = "Moisture and temperature", notes,
            images = revision == 0 ? new[] { new { contentType = "image/png", dataBase64 = image } } : [],
            zones = new[] { new { name = "North", crop = "Rice", cropAgeDays = (int?)null, symptoms = "", installationLocation = "Along the irrigation canal" } }
        };
        // Recording the first survey now establishes the engineer context without claiming.
        Assert.Equal(HttpStatusCode.BadRequest, (await engineer.PostAsJsonAsync($"/api/consultations/{id}/surveys", Request(0, "Invalid image", "not-base64"))).StatusCode);
        var survey = await engineer.PostAsJsonAsync($"/api/consultations/{id}/surveys", Request(0, "Initial"));
        Assert.Equal(HttpStatusCode.Created, survey.StatusCode);
        var first = (await survey.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(1, first.GetProperty("revision").GetInt32());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("farmArea").ValueKind);
        Assert.Equal(JsonValueKind.Null, first.GetProperty("zones")[0].GetProperty("cropAgeDays").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await engineer.PostAsJsonAsync($"/api/consultations/{id}/surveys", Request(0, "Stale"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await engineer.PostAsJsonAsync($"/api/consultations/{id}/surveys", Request(1, "Updated"))).StatusCode);
        var history = await engineer.GetFromJsonAsync<JsonElement>($"/api/consultations/{id}/surveys");
        Assert.Equal(2, history.GetProperty("pagination").GetProperty("totalItems").GetInt32());
        var original = (await engineer.GetFromJsonAsync<JsonElement>($"/api/consultations/{id}/surveys/{first.GetProperty("id").GetInt32()}")).GetProperty("data");
        Assert.Equal("Initial", original.GetProperty("notes").GetString());
        Assert.Equal(EvidencePng, Assert.Single(original.GetProperty("images").EnumerateArray()).GetProperty("dataBase64").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync($"/api/consultations/{id}/surveys/{first.GetProperty("id").GetInt32()}")).StatusCode);
        using var owner = await Login("owner@example.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/consultations/{id}/surveys")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/consultations/{id}/surveys/{first.GetProperty("id").GetInt32()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await engineer.PostAsJsonAsync($"/api/consultations/{id}/surveys", new
        { expectedRevision = 2, farmName = "Farm", farmLocation = "Farm", monitoringNeeds = "Monitoring", zones = Array.Empty<object>() })).StatusCode);
    }

    [Fact]
    public async Task Administrator_binds_customer_farm_and_survey_reads_follow_current_owner_membership()
    {
        using var visitor = factory.CreateApiClient();
        var response = await visitor.PostAsJsonAsync("/api/consultations", new
        { customerName = "Bound customer", phone = "0901234567", farmLocation = "Farm", serviceNeed = "Survey", content = "Please survey" });
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        using var engineer = await Login("engineer@example.com");
        (await engineer.PostAsJsonAsync($"/api/consultations/{id}/claim", new { })).EnsureSuccessStatusCode();
        var survey = await engineer.PostAsJsonAsync($"/api/consultations/{id}/surveys", new
        { expectedRevision = 0, farmName = "Bound farm", farmLocation = "Farm", monitoringNeeds = "Moisture", zones = new[] { new { name = "North", installationLocation = "Edge" } } });
        survey.EnsureSuccessStatusCode();
        using var admin = await Login("admin@example.com");
        using var owner = await Login("owner@example.com");
        var ownerId = (await owner.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("data").GetProperty("id").GetInt32();
        var farm = await admin.PostAsJsonAsync("/api/farms", new { name = "Bound customer farm" });
        farm.EnsureSuccessStatusCode();
        var farmId = (await farm.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        var binding = new { ownerId, farmId };
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PutAsJsonAsync($"/api/consultations/{id}/customer-farm", binding)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PutAsJsonAsync($"/api/consultations/{id}/customer-farm", binding)).StatusCode);
        (await admin.PostAsJsonAsync($"/api/farms/{farmId}/members", new { userId = ownerId })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/consultations/{id}/customer-farm", binding)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/consultations/{id}/surveys")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/consultations/{id}/surveys")).StatusCode);
        var records = await owner.GetFromJsonAsync<JsonElement>($"/api/farms/{farmId}/survey-consultations");
        Assert.Equal(id, Assert.Single(records.GetProperty("data").EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/consultations/{id}")).StatusCode);
        (await admin.DeleteAsync($"/api/farms/{farmId}/members/{ownerId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/consultations/{id}/surveys")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/farms/{farmId}/survey-consultations")).StatusCode);
    }

    [Fact]
    public async Task Proposal_versions_require_plan_and_external_deposit_confirmation_before_installation_submission()
    {
        using var visitor = factory.CreateApiClient();
        var response = await visitor.PostAsJsonAsync("/api/consultations", new
        { customerName = "Proposal customer", phone = "0901234567", farmLocation = "Farm", serviceNeed = "Devices", content = "Survey and proposal" });
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        using var engineer = await Login("engineer@example.com");
        (await engineer.PostAsJsonAsync($"/api/consultations/{id}/claim", new { })).EnsureSuccessStatusCode();
        var surveyResult = await engineer.PostAsJsonAsync($"/api/consultations/{id}/surveys", new
        { expectedRevision = 0, farmName = "Farm", farmLocation = "Can Tho", monitoringNeeds = "Soil moisture", zones = new[] { new { name = "North", installationLocation = "Canal" } } });
        surveyResult.EnsureSuccessStatusCode();
        var survey = (await surveyResult.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        int productId;
        await using (var priceScope = factory.Services.CreateAsyncScope()) { var priceDb=priceScope.ServiceProvider.GetRequiredService<AppDbContext>(); var product=new FarmMonitoring.Domain.Entities.PublicContent { Kind=FarmMonitoring.Domain.Entities.PublicContentKind.DEVICE,Title="Rental sensor",Body="Sensor",Published=true,EstimatedMonthlyRentVnd=100000 };priceDb.PublicContent.Add(product);await priceDb.SaveChangesAsync();productId=product.Id; }
        object Proposal(int revision) => new { expectedRevision = revision, surveyId = survey.GetProperty("id").GetInt32(), rentalMonths = 6,
            requestedInstallationAt = DateTimeOffset.UtcNow.AddDays(7), notes = "Agreed monitoring needs",
            lines = new[] { new { surveyZoneId = survey.GetProperty("zones")[0].GetProperty("id").GetInt32(), deviceType = "Soil moisture sensor", quantity = 3, installationLocation = "Canal edge",publicContentId=productId } } };
        var created = await engineer.PostAsJsonAsync($"/api/consultations/{id}/proposals", Proposal(0));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var first = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var firstId = first.GetProperty("id").GetInt32();
        Assert.Equal(3, first.GetProperty("lines")[0].GetProperty("quantity").GetInt32());
        var firstPath = $"/api/consultations/{id}/proposals/{firstId}";
        Assert.Equal(HttpStatusCode.Conflict, (await engineer.PostAsJsonAsync(firstPath + "/confirm-deposit", new { note = "Bank receipt" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await engineer.PostAsJsonAsync(firstPath + "/submit-installation", new { })).StatusCode);
        var secondResult = await engineer.PostAsJsonAsync($"/api/consultations/{id}/proposals", Proposal(1));
        secondResult.EnsureSuccessStatusCode();
        var second = (await secondResult.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var path = $"/api/consultations/{id}/proposals/{second.GetProperty("id").GetInt32()}";
        Assert.Equal(HttpStatusCode.Conflict, (await engineer.PostAsJsonAsync(firstPath + "/confirm-plan", new { note = "Old plan" })).StatusCode);
        (await engineer.PostAsJsonAsync(path + "/confirm-plan", new { note = "Customer accepted by phone" })).EnsureSuccessStatusCode();
        (await engineer.PostAsJsonAsync(path + "/confirm-deposit", new { note = "External bank receipt REF-42 confirmed" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await engineer.PostAsJsonAsync(path + "/submit-installation", new { })).StatusCode);
        using var owner = await Login("owner@example.com");
        using var admin = await Login("admin@example.com");
        var ownerId = (await owner.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("data").GetProperty("id").GetInt32();
        var farmResult = await admin.PostAsJsonAsync("/api/farms", new { name = "Proposal farm" });
        farmResult.EnsureSuccessStatusCode();
        var farmId = (await farmResult.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        (await admin.PostAsJsonAsync($"/api/farms/{farmId}/members", new { userId = ownerId })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/consultations/{id}/customer-farm", new { ownerId, farmId })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(path + "/submit-installation", new { })).StatusCode);
        var submitted = await engineer.PostAsJsonAsync(path + "/submit-installation", new { });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var installation = (await submitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("SUBMITTED", installation.GetProperty("status").GetString());
        Assert.Equal(farmId, installation.GetProperty("farmId").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await engineer.PostAsJsonAsync(path + "/submit-installation", new { })).StatusCode);
        var preserved = (await engineer.GetFromJsonAsync<JsonElement>(firstPath)).GetProperty("data");
        Assert.Equal("DRAFT", preserved.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(path)).StatusCode);
        var requestPath = $"/api/installation-requests/{installation.GetProperty("id").GetInt32()}";
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(requestPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(requestPath + "/approve", new { note = "Owner cannot approve" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(requestPath + "/return", new { note = " " })).StatusCode);
        (await admin.PostAsJsonAsync(requestPath + "/return", new { note = "Clarify installation location" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(requestPath + "/approve", new { note = "Too late" })).StatusCode);
        var returned = (await engineer.GetFromJsonAsync<JsonElement>(requestPath)).GetProperty("data");
        Assert.Equal("RETURNED", returned.GetProperty("status").GetString());
        Assert.Equal("Clarify installation location", returned.GetProperty("reviewNote").GetString());
        var revisedResult = await engineer.PostAsJsonAsync($"/api/consultations/{id}/proposals", Proposal(2));
        revisedResult.EnsureSuccessStatusCode();
        var revisedId = (await revisedResult.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        var revisedPath = $"/api/consultations/{id}/proposals/{revisedId}";
        (await engineer.PostAsJsonAsync(revisedPath + "/confirm-plan", new { note = "Revised plan accepted" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync(revisedPath + "/confirm-deposit", new { note = "Deposit receipt checked" })).EnsureSuccessStatusCode();
        var resubmitted = await engineer.PostAsJsonAsync(revisedPath + "/submit-installation", new { });
        resubmitted.EnsureSuccessStatusCode();
        var newId = (await resubmitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        var approvedResult = await admin.PostAsJsonAsync($"/api/installation-requests/{newId}/approve", new { note = "Information verified" });
        approvedResult.EnsureSuccessStatusCode();
        Assert.Equal("APPROVED", (await approvedResult.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/installation-requests/{newId}/return", new { note = "Already reviewed" })).StatusCode);
        var technicianEmail = $"install-tech-{Guid.NewGuid():N}@example.com";
        var technicianResult = await admin.PostAsJsonAsync("/api/users", new { email = technicianEmail, fullName = "Installation technician", password = "Test-password-123!", roles = new[] { "Technician" } });
        technicianResult.EnsureSuccessStatusCode();
        var technicianId = (await technicianResult.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        using var technician = await Login(technicianEmail);
        var appointment = DateTimeOffset.UtcNow.AddDays(8);
        var jobRequest = new { technicianId, scheduledAt = appointment, note = "Customer appointment agreed" };
        Assert.Equal(HttpStatusCode.Forbidden, (await engineer.PostAsJsonAsync($"/api/installation-requests/{newId}/jobs", jobRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(requestPath + "/jobs", jobRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync($"/api/installation-requests/{newId}/jobs", new { technicianId = ownerId, scheduledAt = appointment, note = "Wrong role" })).StatusCode);
        var jobResult = await admin.PostAsJsonAsync($"/api/installation-requests/{newId}/jobs", jobRequest);
        Assert.Equal(HttpStatusCode.Created, jobResult.StatusCode);
        var job = (await jobResult.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var jobId = job.GetProperty("id").GetInt32();
        var jobPath = $"/api/installation-jobs/{jobId}";
        Assert.Equal("ASSIGNED", job.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/installation-requests/{newId}/jobs", jobRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.GetAsync($"/api/farms/{farmId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await technician.GetAsync(jobPath)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(jobPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync(jobPath + "/start", new { expectedRevision = 1 })).StatusCode);
        (await technician.PostAsJsonAsync(jobPath + "/start", new { expectedRevision = 1 })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await technician.PostAsJsonAsync(jobPath + "/start", new { expectedRevision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(jobPath + "/assignment", new { expectedRevision = 1, technicianId, scheduledAt = appointment, note = "Stale assignment" })).StatusCode);
        var secondEmail = $"install-tech-{Guid.NewGuid():N}@example.com";
        var secondTechResult = await admin.PostAsJsonAsync("/api/users", new { email = secondEmail, fullName = "Replacement technician", password = "Test-password-123!", roles = new[] { "Technician" } });
        secondTechResult.EnsureSuccessStatusCode();
        var secondTechId = (await secondTechResult.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        using var secondTech = await Login(secondEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await secondTech.GetAsync(jobPath)).StatusCode);
        var changed = await admin.PostAsJsonAsync(jobPath + "/assignment", new { expectedRevision = 2, technicianId = secondTechId, scheduledAt = appointment.AddDays(1), note = "Original technician unavailable" });
        changed.EnsureSuccessStatusCode();
        var changedJob = (await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(3, changedJob.GetProperty("revision").GetInt32());
        Assert.Equal(3, changedJob.GetProperty("history").GetArrayLength());
        Assert.Equal("ASSIGNED", changedJob.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.GetAsync(jobPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.PostAsJsonAsync(jobPath + "/start", new { expectedRevision = 3 })).StatusCode);
        (await secondTech.PostAsJsonAsync(jobPath + "/start", new { expectedRevision = 3 })).EnsureSuccessStatusCode();
        var zoneResult = await admin.PostAsJsonAsync($"/api/farms/{farmId}/zones", new { name = "Installed zone" });
        zoneResult.EnsureSuccessStatusCode();
        var zoneId = (await zoneResult.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        var nodes = new List<int>();
        for (var n = 0; n < 3; n++)
        {
            var node = await admin.PostAsJsonAsync("/api/sensor-nodes", new { zoneId, deviceCode = $"installed-{Guid.NewGuid():N}", name = "Installed sensor", status = "ONLINE" });
            node.EnsureSuccessStatusCode();
            nodes.Add((await node.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32());
        }
        var proposalLineId = changedJob.GetProperty("lines")[0].GetProperty("id").GetInt32();
        var allocation = new { expectedRevision = 4, note = "Prepared devices for this installation", devices = nodes.Select(nodeId => new { proposalLineId, sensorNodeId = nodeId }).ToArray() };
        Assert.Equal(HttpStatusCode.Forbidden, (await secondTech.PutAsJsonAsync(jobPath + "/devices", allocation)).StatusCode);
        (await admin.PutAsJsonAsync(jobPath + "/devices", allocation)).EnsureSuccessStatusCode();
        var allocated = (await secondTech.GetFromJsonAsync<JsonElement>(jobPath + "/devices")).GetProperty("data");
        Assert.Equal(3, allocated.GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.GetAsync(jobPath + "/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(jobPath + "/devices", allocation)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await secondTech.PostAsJsonAsync(jobPath + "/reports", new { expectedRevision = 5, notes = "Wrong device set",
            devices = nodes.Select(nodeId => new { proposalLineId, sensorNodeId = nodeId + 1000000, installationLocation = "Canal edge", accessoriesChecked = true,
                connectivityPassed = true, operationPassed = true, testResults = "Reading received" }).ToArray() })).StatusCode);
        object Report(int revision, bool connectivityPassed = true) => new { expectedRevision = revision,paperwork=new {contract=new[]{new{contentType="image/png",dataBase64=EvidencePng}},handover=new[]{new{contentType="image/png",dataBase64=EvidencePng}}}, notes = "Devices tested and handover demonstrated", issues = connectivityPassed ? null : "Sensor readings did not reach the gateway",
            images = connectivityPassed ? Array.Empty<object>() : new object[] { new { contentType = "image/png", dataBase64 = EvidencePng } },
            devices = nodes.Select(nodeId => new { proposalLineId, sensorNodeId = nodeId, installationLocation = "Canal edge",
                accessoriesChecked = true, connectivityPassed, operationPassed = true, testResults = connectivityPassed ? "Live sensor reading received" : "Gateway received no readings" }).ToArray() };
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.PostAsJsonAsync(jobPath + "/reports", Report(5))).StatusCode);
        var reportResult = await secondTech.PostAsJsonAsync(jobPath + "/reports", Report(5, connectivityPassed: false));
        Assert.Equal(HttpStatusCode.Created, reportResult.StatusCode);
        var reportId = (await reportResult.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(jobPath + "/approve", new { expectedRevision = 6, reportId, note = "Failed checks prevent approval despite paper evidence" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await secondTech.PostAsJsonAsync(jobPath + "/confirm-handover", new { expectedRevision = 6, reportId, note = "Cannot self-confirm" })).StatusCode);
        (await owner.PostAsJsonAsync(jobPath + "/confirm-handover", new { expectedRevision = 6, reportId, note = "Handover received" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync(jobPath + "/approve", new { expectedRevision = 7, reportId, note = "Cannot approve failed connectivity" })).StatusCode);
        var rejectedJob = (await admin.GetFromJsonAsync<JsonElement>(jobPath)).GetProperty("data");
        Assert.Equal("OWNER_CONFIRMED", rejectedJob.GetProperty("status").GetString());
        Assert.Equal(7, rejectedJob.GetProperty("revision").GetInt32());
        var rentalNow = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var rentalStart=FarmMonitoring.Domain.Entities.OperationalRules.RentalStart(rentalNow);
        var rentalDates = new { startAt = rentalStart, endAt = rentalStart.AddMonths(6), note = "Confirmed rental agreement" };
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(jobPath + "/rental-contract", rentalDates)).StatusCode);
        (await admin.PostAsJsonAsync(jobPath + "/return", new { expectedRevision = 7, reportId, note = "Repeat connectivity checks" })).EnsureSuccessStatusCode();
        var correctedResult = await secondTech.PostAsJsonAsync(jobPath + "/reports", Report(8));
        correctedResult.EnsureSuccessStatusCode();
        var correctedId = (await correctedResult.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(jobPath + "/confirm-handover", new { expectedRevision = 9, reportId, note = "Stale report" })).StatusCode);
        (await owner.PostAsJsonAsync(jobPath + "/confirm-handover", new { expectedRevision = 9, reportId = correctedId, note = "Corrected installation accepted" })).EnsureSuccessStatusCode();
        var finalApproval = await admin.PostAsJsonAsync(jobPath + "/approve", new { expectedRevision = 10, reportId = correctedId, note = "Verified installation" });
        finalApproval.EnsureSuccessStatusCode();
        Assert.Equal("APPROVED", (await finalApproval.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("status").GetString());
        var originalReport = (await owner.GetFromJsonAsync<JsonElement>(jobPath + $"/reports/{reportId}")).GetProperty("data");
        Assert.Equal("Repeat connectivity checks", originalReport.GetProperty("reviewNote").GetString());
        Assert.Equal(3, originalReport.GetProperty("devices").GetArrayLength());
        Assert.All(originalReport.GetProperty("devices").EnumerateArray(), device => Assert.False(device.GetProperty("connectivityPassed").GetBoolean()));
        Assert.Equal("Sensor readings did not reach the gateway", originalReport.GetProperty("issues").GetString());
        Assert.Equal(EvidencePng, Assert.Single(originalReport.GetProperty("images").EnumerateArray()).GetProperty("dataBase64").GetString());
        var correctedReport = (await owner.GetFromJsonAsync<JsonElement>(jobPath + $"/reports/{correctedId}")).GetProperty("data");
        Assert.Empty(correctedReport.GetProperty("images").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.GetAsync(jobPath + $"/reports/{reportId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await secondTech.PostAsJsonAsync(jobPath + "/reports", Report(11))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(jobPath + "/rental-contract", rentalDates)).StatusCode);
        int rentalId;
        await using(var rentalScope=factory.Services.CreateAsyncScope()) rentalId=await rentalScope.ServiceProvider.GetRequiredService<AppDbContext>().RentalContracts.Where(x=>x.InstallationJobId==jobId).Select(x=>x.Id).SingleAsync();
        var rental=(await admin.GetFromJsonAsync<JsonElement>($"/api/rental-contracts/{rentalId}")).GetProperty("data");
        var rentalPath = $"/api/rental-contracts/{rental.GetProperty("id").GetInt32()}";
        Assert.Equal("DRAFT", rental.GetProperty("status").GetString());
        Assert.Equal(3, rental.GetProperty("devices").GetArrayLength());
        Assert.All(rental.GetProperty("devices").EnumerateArray(), device => Assert.Equal("Canal edge", device.GetProperty("installationLocation").GetString()));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(jobPath + "/rental-contract", rentalDates)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await secondTech.GetAsync(rentalPath)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await engineer.GetAsync(rentalPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(rentalPath + "/activate", new { expectedRevision = 1, note = "Owner cannot activate" })).StatusCode);
        var activated = await admin.PostAsJsonAsync(rentalPath + "/activate", new { expectedRevision = 1, note = "Start agreed rental" });
        Assert.Equal(HttpStatusCode.Conflict,activated.StatusCode);
        await MonitorRentals(rentalStart);
        Assert.Equal("ACTIVE", (await owner.GetFromJsonAsync<JsonElement>(rentalPath)).GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(rentalPath + "/activate", new { expectedRevision = 1, note = "Duplicate activation" })).StatusCode);
        async Task MonitorRentals(DateTimeOffset now)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IRentalRepository>().MonitorAsync(now, 7, 100, CancellationToken.None);
        }
        async Task<int> NotificationCount()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.CountAsync();
        }
        var beforeWarning = await NotificationCount();
        await MonitorRentals(rentalDates.endAt.AddDays(-2));
        var afterWarning = await NotificationCount();
        Assert.True(afterWarning > beforeWarning);
        await MonitorRentals(rentalDates.endAt.AddDays(-2));
        Assert.Equal(afterWarning, await NotificationCount());
        await MonitorRentals(rentalDates.endAt.AddSeconds(1));
        var afterExpiry = await NotificationCount();
        Assert.True(afterExpiry > afterWarning);
        await MonitorRentals(rentalDates.endAt.AddSeconds(1));
        Assert.Equal(afterExpiry, await NotificationCount());
        var expiredRental = (await owner.GetFromJsonAsync<JsonElement>(rentalPath)).GetProperty("data");
        Assert.Equal("EXPIRED", expiredRental.GetProperty("status").GetString());
        Assert.Equal(3, expiredRental.GetProperty("revision").GetInt32());
        Assert.All(expiredRental.GetProperty("devices").EnumerateArray(), device => Assert.Equal(JsonValueKind.Null, device.GetProperty("returnedAt").ValueKind));
        var extendedEnd = rentalDates.endAt.AddMonths(1);
        var extension = await admin.PostAsJsonAsync(rentalPath + "/extend", new { expectedRevision = 3, endAt = extendedEnd, note = "External renewal confirmed" });
        extension.EnsureSuccessStatusCode();
        Assert.Equal(extendedEnd, (await extension.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("endAt").GetDateTimeOffset());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(rentalPath + "/extend", new { expectedRevision = 2, endAt = extendedEnd.AddMonths(1), note = "Stale renewal" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(rentalPath + "/end", new { expectedRevision = 4, note = "Devices still on farm", devicesReturned = false })).StatusCode);
        var ended = await admin.PostAsJsonAsync(rentalPath + "/end", new { expectedRevision = 4, note = "All devices physically returned", devicesReturned = true });
        ended.EnsureSuccessStatusCode();
        var endedRental = (await owner.GetFromJsonAsync<JsonElement>(rentalPath)).GetProperty("data");
        Assert.Equal("ENDED", endedRental.GetProperty("status").GetString());
        Assert.All(endedRental.GetProperty("devices").EnumerateArray(), device => Assert.NotEqual(JsonValueKind.Null, device.GetProperty("returnedAt").ValueKind));
        Assert.Equal(5, endedRental.GetProperty("history").GetArrayLength());
        Assert.Empty((await admin.GetFromJsonAsync<JsonElement>(jobPath + "/devices")).GetProperty("data").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(rentalPath + "/extend", new { expectedRevision = 5, endAt = extendedEnd.AddMonths(1), note = "Cannot renew returned devices" })).StatusCode);
        (await admin.DeleteAsync($"/api/farms/{farmId}/members/{ownerId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(rentalPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(requestPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(jobPath)).StatusCode);
    }

    [Fact]
    public async Task Invalid_public_request_does_not_create_a_consultation()
    {
        using var visitor = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.PostAsJsonAsync("/api/consultations", new
        { customerName = "", phone = "bad", email = "bad", farmLocation = "", farmArea = -1, serviceNeed = "", content = "" })).StatusCode);
    }
}
