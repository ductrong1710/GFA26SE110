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
public sealed class InstallationDossierTests(ApiFactory factory)
{
    private static readonly EvidenceImageData Bill = new("image/jpeg", Convert.ToBase64String([255, 216, 255, 217]));
    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateApiClient();
        var data = await Data(await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        return client;
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }
    private async Task<(int Lead, int Proposal, int Survey, int Zone, int Product)> Prepare(HttpClient engineer, bool confirmed = true)
    {
        using var guest = factory.CreateApiClient();
        var lead = (await Data(await guest.PostAsJsonAsync("/api/consultations", new {
            customerName = "External customer", phone = "0901234567", farmLocation = "Can Tho", serviceNeed = "Rent sensors", content = "Call outside"
        }))).GetProperty("id").GetInt32();
        var survey = await Data(await engineer.PostAsJsonAsync($"/api/consultations/{lead}/surveys", new {
            expectedRevision = 0, farmName = "External garden", farmLocation = "Garden address", farmArea = 2,
            monitoringNeeds = "Moisture", zones = new[] { new { name = "North", crop = "Mango" } }
        }));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = new PublicContent { Kind = PublicContentKind.DEVICE, Title = "Rental sensor", Body = "Sensor", Published = true, EstimatedMonthlyRentVnd = 150000 };
        db.PublicContent.Add(product); await db.SaveChangesAsync();
        var surveyId = survey.GetProperty("id").GetInt32();
        var zone = survey.GetProperty("zones")[0].GetProperty("id").GetInt32();
        var proposal = await Data(await engineer.PostAsJsonAsync($"/api/consultations/{lead}/proposals", Proposal(0, surveyId, zone, product.Id)));
        var proposalId = proposal.GetProperty("id").GetInt32();
        if (confirmed) await Confirm(engineer, lead, proposalId);
        return (lead, proposalId, surveyId, zone, product.Id);
    }
    private static async Task Confirm(HttpClient engineer, int lead, int proposal)
    {
        var path = $"/api/consultations/{lead}/proposals/{proposal}";
        await Data(await engineer.PostAsJsonAsync(path + "/confirm-plan", new { note = "Customer agreed externally" }));
        await Data(await engineer.PostAsJsonAsync(path + "/confirm-deposit", new { note = "External receipt checked" }));
    }
    private static object Proposal(int revision, int survey, int zone, int product) => new {
        expectedRevision = revision, surveyId = survey, rentalMonths = 6, requestedInstallationAt = DateTimeOffset.UtcNow.AddDays(10),
        lines = new[] { new { surveyZoneId = zone, deviceType = "Sensor", quantity = 2, publicContentId = product } }
    };
    private static object Submission(string email, EvidenceImageData[]? bills = null, int revision = 1) => new {
        expectedRevision = revision, note = "Devices and deposit agreed outside", bills = bills ?? [Bill],
        customer = new { fullName = "External customer", email }
    };
    [Fact]
    public async Task Missing_or_fake_bill_does_not_create_customer_farm_or_pending_dossier()
    {
        using var engineer = await Login("engineer@example.com");
        var setup = await Prepare(engineer, false); var email = $"missing-{Guid.NewGuid():N}@example.com";
        var path = $"/api/installation-dossiers/{setup.Proposal}/submit";
        Assert.Equal(HttpStatusCode.Conflict, (await engineer.PostAsJsonAsync(path, Submission(email))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await engineer.PostAsJsonAsync(path, Submission(email, []))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await engineer.PostAsJsonAsync(path, Submission(email, [new("image/png", "AAAA")]))).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(x => x.Email == email));
        var lead = await db.Consultations.AsNoTracking().SingleAsync(x => x.Id == setup.Lead);
        Assert.Null(lead.OwnerId); Assert.Null(lead.FarmId);
        Assert.Equal(ProposalStatus.DRAFT, (await db.DeviceProposals.SingleAsync(x => x.Id == setup.Proposal)).Status);
    }
    [Fact]
    public async Task Engineer_sends_complete_customer_garden_and_bill_without_online_confirmation_or_admin_data_entry()
    {
        using var engineer = await Login("engineer@example.com"); using var admin = await Login("admin@example.com");
        var setup = await Prepare(engineer); var email = $"dossier-{Guid.NewGuid():N}@example.com";
        var path = $"/api/installation-dossiers/{setup.Proposal}";
        var sent = await Data(await engineer.PostAsJsonAsync(path + "/submit", Submission(email)));
        Assert.Equal("WAITING_ADMIN", sent.GetProperty("status").GetString());
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lead = await db.Consultations.AsNoTracking().SingleAsync(x => x.Id == setup.Lead);
        Assert.NotNull(lead.OwnerId); Assert.NotNull(lead.FarmId);
        var owner = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleAsync(x => x.Id == lead.OwnerId);
        Assert.Equal(email, owner.Email); Assert.Equal("FarmOwner", Assert.Single(owner.UserRoles).Role.Name);
        Assert.True(await db.UserFarms.AnyAsync(x => x.UserId == owner.Id && x.FarmId == lead.FarmId));
        Assert.False(await db.InstallationRequests.AnyAsync(x => x.ProposalId == setup.Proposal));
        var pending = await Data(await admin.GetAsync("/api/installation-dossiers?pageSize=100"));
        Assert.Contains(pending.EnumerateArray(), x => x.GetProperty("id").GetInt32() == setup.Proposal);
        var detail = await Data(await admin.GetAsync($"/api/consultations/{setup.Lead}/proposals/{setup.Proposal}"));
        Assert.Equal(Bill.DataBase64, detail.GetProperty("depositBills")[0].GetProperty("dataBase64").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await engineer.PostAsJsonAsync(path + "/submit", Submission(email))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await engineer.PostAsJsonAsync($"/api/consultations/{setup.Lead}/proposals", Proposal(1, setup.Survey, setup.Zone, setup.Product))).StatusCode);
        var results = await Task.WhenAll(admin.PostAsJsonAsync(path + "/review", new { expectedRevision = 1, approve = true, note = "Farm and bill match" }),
            admin.PostAsJsonAsync(path + "/review", new { expectedRevision = 1, approve = true, note = "Concurrent approval" }));
        Assert.Single(results, x => x.IsSuccessStatusCode); Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        var decision = await Data(results.Single(x => x.IsSuccessStatusCode));
        var requestId = decision.GetProperty("installationRequestId").GetInt32();
        Assert.Equal(InstallationRequestStatus.APPROVED, (await db.InstallationRequests.AsNoTracking().SingleAsync(x => x.Id == requestId)).Status);
        Assert.Single(await db.InstallationRequests.Where(x => x.ProposalId == setup.Proposal).ToArrayAsync());
        // Existing assignment API is available immediately after this one approval.
        var technician = await Data(await admin.PostAsJsonAsync("/api/users", new { email = $"tech-{Guid.NewGuid():N}@example.com", fullName = "Technician", password = "Test-password-123!", roles = new[] { "Technician" } }));
        var job = await Data(await admin.PostAsJsonAsync($"/api/installation-requests/{requestId}/jobs", new {
            technicianId = technician.GetProperty("id").GetInt32(), scheduledAt = DateTimeOffset.UtcNow.AddDays(11), note = "Install approved garden"
        }));
        Assert.Equal("ASSIGNED", job.GetProperty("status").GetString());
        var techId = technician.GetProperty("id").GetInt32();
        Assert.True(await db.Notifications.AnyAsync(x => x.UserId == techId));
        Assert.True(await db.Notifications.AnyAsync(x => x.UserId == owner.Id));
    }
    [Fact]
    public async Task Unrelated_engineer_owner_and_admin_cannot_submit_or_self_approve_the_dossier()
    {
        using var engineer = await Login("engineer@example.com"); using var admin = await Login("admin@example.com"); using var owner = await Login("owner@example.com");
        var setup = await Prepare(engineer); var path = $"/api/installation-dossiers/{setup.Proposal}";
        var email = $"other-{Guid.NewGuid():N}@example.com";
        await Data(await admin.PostAsJsonAsync("/api/users", new { email, fullName = "Other engineer", password = "Test-password-123!", roles = new[] { "FarmEngineer" } }));
        using var other = await Login(email);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync(path + "/submit", Submission("owner@example.com"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(path + "/submit", Submission("owner@example.com"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync(path + "/submit", Submission("owner@example.com"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await engineer.GetAsync("/api/installation-dossiers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await engineer.PostAsJsonAsync(path + "/review", new { expectedRevision = 1, approve = true, note = "Self approval" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await engineer.PostAsJsonAsync($"/api/consultations/{setup.Lead}/proposals/{setup.Proposal}/submit-installation", new { })).StatusCode);
    }
    [Fact]
    public async Task Existing_admin_email_cannot_be_converted_into_customer_and_submission_rolls_back()
    {
        using var engineer = await Login("engineer@example.com"); var setup = await Prepare(engineer);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await engineer.PostAsJsonAsync($"/api/installation-dossiers/{setup.Proposal}/submit", Submission("admin@example.com"))).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null((await db.Consultations.SingleAsync(x => x.Id == setup.Lead)).FarmId);
        Assert.Equal(ProposalStatus.DEPOSIT_CONFIRMED, (await db.DeviceProposals.SingleAsync(x => x.Id == setup.Proposal)).Status);
    }
    [Fact]
    public async Task Returned_dossier_keeps_old_bill_and_requires_a_new_revision()
    {
        using var engineer = await Login("engineer@example.com"); using var admin = await Login("admin@example.com");
        var setup = await Prepare(engineer); var email = $"return-{Guid.NewGuid():N}@example.com";
        var path = $"/api/installation-dossiers/{setup.Proposal}";
        await Data(await engineer.PostAsJsonAsync(path + "/submit", Submission(email)));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path + "/review", new { expectedRevision = 999, approve = true, note = "Stale revision" })).StatusCode);
        await Data(await admin.PostAsJsonAsync(path + "/review", new { expectedRevision = 1, approve = false, note = "Need clearer bill" }));
        Assert.Equal(HttpStatusCode.Conflict, (await engineer.PostAsJsonAsync(path + "/submit", Submission(email))).StatusCode);
        var revised = await Data(await engineer.PostAsJsonAsync($"/api/consultations/{setup.Lead}/proposals", Proposal(1, setup.Survey, setup.Zone, setup.Product)));
        var next = revised.GetProperty("id").GetInt32();
        await Confirm(engineer, setup.Lead, next);
        await Data(await engineer.PostAsJsonAsync($"/api/installation-dossiers/{next}/submit", Submission(email, revision: 2)));
        var old = await Data(await admin.GetAsync($"/api/consultations/{setup.Lead}/proposals/{setup.Proposal}"));
        Assert.Equal("RETURNED", old.GetProperty("status").GetString());
        Assert.Equal("Need clearer bill", old.GetProperty("dossierReviewNote").GetString());
        Assert.Equal(Bill.DataBase64, old.GetProperty("depositBills")[0].GetProperty("dataBase64").GetString());
    }
}
