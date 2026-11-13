using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public sealed class SiteVisitTests(ApiFactory factory)
{
    private const string Office = "OFFICE_1";
    private const string Maps = "https://maps.app.goo.gl/example";
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS3sAAAAASUVORK5CYII=";
    private static async Task<JsonElement> Data(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private static async Task Expect(HttpStatusCode status, Task<HttpResponseMessage> request)
    {
        using var response = await request;
        Assert.True(response.StatusCode == status, $"Expected {status}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateApiClient();
        var auth = await Data(client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<string> Lead(string? channel)
    {
        using var visitor = factory.CreateApiClient();
        var body = new Dictionary<string,string> { ["customerName"]="Site visit customer", ["phone"]="0901234567",
            ["farmLocation"]="Can Tho", ["content"]="Please call", ["serviceNeed"]="Sensor monitoring" };
        if (channel is not null) body["channel"] = channel;
        var receipt = await Data(visitor.PostAsJsonAsync("/api/consultations", body));
        Assert.False(receipt.TryGetProperty("payment", out _));
        return $"/api/consultations/{receipt.GetProperty("id").GetInt32()}";
    }

    private static Task<HttpResponseMessage> Survey(HttpClient engineer, string lead) => engineer.PostAsJsonAsync(lead + "/surveys",
        new { expectedRevision = 0, farmName = "Garden", farmLocation = "Can Tho", monitoringNeeds = "Moisture", zones = new[] { new { name = "North", installationLocation = "Edge" } } });

    private static Task<HttpResponseMessage> Preview(HttpClient user,string path, decimal km) =>
        user.PostAsJsonAsync(path + "/fee-preview", new { officeCode = Office, distanceKm = km });

    private static Task<HttpResponseMessage> Proposal(HttpClient user,string path,int revision,decimal km,string version,long? manual=null,string? reason=null) =>
        user.PostAsJsonAsync(path + "/proposals", new { expectedRevision = revision, officeCode = Office, distanceKm = km,
            mapsUrl = Maps, proposedAt = DateTimeOffset.UtcNow.AddDays(4), pricingVersion = version, manualAmountVnd = manual, manualReason = reason });

    private static Task<HttpResponseMessage> Review(HttpClient admin,string path,int rev,bool approve,string? reason=null) =>
        admin.PostAsJsonAsync(path + "/review", new { expectedRevision = rev, decision = approve ? "APPROVE":"RETURN", reason });

    private static Task<HttpResponseMessage> Schedule(HttpClient admin,string path,int rev) =>
        admin.PostAsJsonAsync(path + "/schedule", new { expectedRevision = rev });

    private static string Version(JsonElement preview) => preview.GetProperty("pricingVersion").GetString()!;

    private static async Task OneTransition(Func<Task<HttpResponseMessage>> post)
    {
        var responses = await Task.WhenAll(post(),post());
        try {
            Assert.Single(responses,r=>r.IsSuccessStatusCode);
            Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
        } finally { foreach(var r in responses) r.Dispose(); }
    }

    [Fact]
    public async Task Upgrade_preserves_existing_consultation_without_inventing_channel_or_fees()
    {
        await using var previous = new ApiFactory { SeedAccounts=false,InitialMigration="20261104011126_AdminReviewedDossierCustomer" };
        await previous.InitializeAsync();
        await using var scope = previous.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO consultations (customer_name,phone,farm_location,service_need,content,status,created_at)
            VALUES ('Historical garden','0901234567','Can Tho','Survey','Contact customer','NEW',now());
            """);
        await db.GetService<IMigrator>().MigrateAsync();
        var lead = await db.Consultations.SingleAsync();
        Assert.Null(lead.Channel);
        Assert.Equal("Historical garden",lead.CustomerName);
        Assert.False(await db.Set<FarmMonitoring.Domain.Entities.SiteVisitProposal>().AnyAsync());
        Assert.False(await db.Set<FarmMonitoring.Domain.Entities.SiteVisitPayment>().AnyAsync());
    }

    [Fact]
    public async Task Actual_tariff_change_requires_new_preview_and_missing_tariff_disables_proposals()
    {
        using var engineer = await Login("engineer@example.com");
        var lead = await Lead("ON_SITE"); var path = lead + "/site-visit";
        await Data(engineer.PostAsJsonAsync(lead+"/claim",new {}));
        var oldPreview = await Data(Preview(engineer,path,45));
        async Task CheckTariff(string threshold, bool enabled)
        {
            await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string,string?> { ["SiteVisitPricing:T1Km"] = threshold })));
            using var next = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
            next.DefaultRequestHeaders.Authorization = engineer.DefaultRequestHeaders.Authorization;
            var preview = await Data(Preview(next,path,45));
            if (enabled)
            {
                Assert.Equal(0L,preview.GetProperty("amountVnd").GetInt64());
                Assert.NotEqual(Version(oldPreview),Version(preview));
                await Expect(HttpStatusCode.Conflict,Proposal(next,path,0,45,Version(oldPreview)));
                await Data(Proposal(next,path,0,45,Version(preview)));
            }
            else
            {
                Assert.Equal("PRICING_NOT_CONFIGURED",preview.GetProperty("status").GetString());
                Assert.Equal(JsonValueKind.Null,preview.GetProperty("amountVnd").ValueKind);
                using var rejected = await Proposal(next,path,0,45,Version(oldPreview));
                Assert.Contains(rejected.StatusCode, new[] { HttpStatusCode.UnprocessableEntity,HttpStatusCode.Conflict });
                Assert.Equal(0,(await Data(next.GetAsync(path))).GetProperty("revision").GetInt32());
            }
        }
        await CheckTariff("",false);
        await CheckTariff("50",true); // Test fixture only: 45 km changes bands.
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ONLINE")]
    public async Task Online_and_legacy_leads_keep_existing_first_survey_flow(string? channel)
    {
        using var engineer = await Login("engineer@example.com");
        var lead = await Lead(channel);
        await Expect(HttpStatusCode.Created, Survey(engineer, lead));
        var detail = await Data(engineer.GetAsync(lead));
        Assert.Equal(channel, detail.GetProperty("channel").GetString());
    }

    [Fact]
    public async Task First_on_site_survey_cannot_claim_or_create_history_before_schedule()
    {
        using var engineer = await Login("engineer@example.com");
        var lead = await Lead("ON_SITE");
        await Expect(HttpStatusCode.Conflict, Survey(engineer,lead));
        var id = int.Parse(lead.Split('/')[^1]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Consultations.AsNoTracking().Include(c=>c.History).SingleAsync(c=>c.Id==id);
        Assert.Null(row.EngineerId);
        Assert.Equal("NEW",row.Status.ToString());
        Assert.Single(row.History);
        Assert.False(await db.Set<FarmMonitoring.Domain.Entities.FarmSurvey>().AnyAsync(s=>s.ConsultationId==id));
    }

    [Fact]
    public async Task Assigned_engineer_sets_legacy_channel_and_zero_fee_still_requires_approval_and_schedule()
    {
        using var engineer = await Login("engineer@example.com");
        using var admin = await Login("admin@example.com");
        var lead = await Lead(null);
        var path = lead + "/site-visit";
        await Data(engineer.PostAsJsonAsync(lead + "/claim",new {}));
        await Data(engineer.PutAsJsonAsync(lead + "/channel",new { channel="ON_SITE", expectedRevision=0 }));
        var preview = await Data(Preview(engineer,path, 5m));
        Assert.Equal(0L,preview.GetProperty("amountVnd").GetInt64());
        await Expect(HttpStatusCode.Conflict,Proposal(engineer,path,0,5,"outdated-test-version"));
        await Data(Proposal(engineer,path,0,5,Version(preview)));
        await Expect(HttpStatusCode.Conflict,Schedule(admin,path,1));
        await Expect(HttpStatusCode.Conflict,engineer.PutAsJsonAsync(lead + "/channel",new { channel="ONLINE",expectedRevision=1 }));
        await OneTransition(()=>Review(admin,path,1,true));
        await Expect(HttpStatusCode.Conflict,Survey(engineer,lead));
        var scheduled = await Data(Schedule(admin,path,1));
        var current = scheduled.GetProperty("current");
        Assert.Equal("NOT_REQUIRED",current.GetProperty("paymentStatus").GetString());
        Assert.Equal(JsonValueKind.Null,current.GetProperty("payment").ValueKind);
        await Expect(HttpStatusCode.Conflict,Proposal(engineer,path,1,5,Version(preview)));
        await Expect(HttpStatusCode.Created,Survey(engineer,lead));
    }

    [Fact]
    public async Task Manual_quote_return_then_channel_adjustment_waits_for_admin_and_preserves_old_quote()
    {
        using var engineer = await Login("engineer@example.com");
        using var admin = await Login("admin@example.com");
        var lead = await Lead("ON_SITE"); var path = lead + "/site-visit";
        await Data(engineer.PostAsJsonAsync(lead+"/claim",new {}));
        var preview = await Data(Preview(engineer,path,165m));
        Assert.Equal("MANUAL_QUOTE_REQUIRED",preview.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null,preview.GetProperty("amountVnd").ValueKind);
        await Data(Proposal(engineer,path,0,165,Version(preview),620000,"Ferry and long route"));
        await Expect(HttpStatusCode.BadRequest,Review(admin,path,1,false));
        await Data(Review(admin,path,1,false,"Offer remote consultation first"));
        var adjust = new { expectedRevision=1,channel="ONLINE",reason="Customer chose an online meeting" };
        var change = await Data(engineer.PostAsJsonAsync(path+"/proposals",adjust));
        Assert.Equal("ON_SITE",change.GetProperty("channel").GetString());
        await Expect(HttpStatusCode.Conflict,Survey(engineer,lead));
        await Expect(HttpStatusCode.Conflict,Review(admin,path,1,true));
        var approved = await Data(Review(admin,path,2,true));
        Assert.Equal("ONLINE",approved.GetProperty("channel").GetString());
        var original = Assert.Single(approved.GetProperty("history").EnumerateArray(),r=>r.GetProperty("revision").GetInt32()==1);
        Assert.Equal(620000L,original.GetProperty("amountVnd").GetInt64());
        Assert.Equal("RETURNED",original.GetProperty("reviewStatus").GetString());
        Assert.Equal(165m,original.GetProperty("distanceKm").GetDecimal());
        await Expect(HttpStatusCode.Created,Survey(engineer,lead));
    }

    [Fact]
    public async Task Approved_but_unpaid_quote_can_be_replaced_only_as_new_unapproved_revision()
    {
        using var engineer = await Login("engineer@example.com");
        using var admin = await Login("admin@example.com");
        var lead = await Lead("ON_SITE"); var path = lead + "/site-visit";
        await Data(engineer.PostAsJsonAsync(lead+"/claim",new {}));
        var first = await Data(Preview(engineer,path,45));
        await Data(Proposal(engineer,path,0,45,Version(first)));
        await Data(Review(admin,path,1,true));
        var second = await Data(Preview(engineer,path,100));
        var later = DateTimeOffset.UtcNow.AddDays(7);
        var newRevision = await Data(engineer.PostAsJsonAsync(path+"/proposals",new {
            expectedRevision=1,officeCode=Office,distanceKm=100,mapsUrl=Maps, proposedAt=later,
            pricingVersion=Version(second), amountVnd=0 // Untrusted UI value cannot override computed amount.
        }));
        var revised = newRevision.GetProperty("current");
        Assert.Equal(2,revised.GetProperty("revision").GetInt32());
        Assert.Equal(400000L,revised.GetProperty("amountVnd").GetInt64());
        Assert.Equal("PENDING",revised.GetProperty("reviewStatus").GetString());
        var earlier = Assert.Single(newRevision.GetProperty("history").EnumerateArray(),p=>p.GetProperty("revision").GetInt32()==1);
        Assert.Equal("APPROVED",earlier.GetProperty("reviewStatus").GetString());
        Assert.Equal(200000L,earlier.GetProperty("amountVnd").GetInt64());
        Assert.True(earlier.GetProperty("proposedAt").GetDateTimeOffset()<later);
        await Expect(HttpStatusCode.Conflict,Schedule(admin,path,1));
        await Expect(HttpStatusCode.Conflict,Schedule(admin,path,2));
        await Expect(HttpStatusCode.Conflict,Review(admin,path,1,true));
    }

    [Fact]
    public async Task Concurrent_confirm_and_schedule_record_one_payment_and_restrict_bill_access()
    {
        using var engineer = await Login("engineer@example.com");
        using var admin = await Login("admin@example.com");
        var lead = await Lead("ON_SITE"); var path = lead + "/site-visit";
        await Data(engineer.PostAsJsonAsync(lead+"/claim",new {}));
        var outsiderEmail = $"outside-{Guid.NewGuid():N}@example.com";
        await Data(admin.PostAsJsonAsync("/api/users",new {email=outsiderEmail,fullName="Other engineer", password="Test-password-123!",roles=new[]{"FarmEngineer"}}));
        using var outsider = await Login(outsiderEmail);
        await Expect(HttpStatusCode.Forbidden, outsider.GetAsync(path));
        await Expect(HttpStatusCode.Forbidden, Proposal(outsider,path,0,45,"unused"));
        var preview = await Data(Preview(engineer,path,45));
        Assert.Equal(200000L, preview.GetProperty("amountVnd").GetInt64());
        await Data(Proposal(engineer,path,0,45,Version(preview)));
        await Expect(HttpStatusCode.Forbidden, Review(engineer,path,1,true));
        await Data(Review(admin,path,1,true));
        await Expect(HttpStatusCode.Conflict,Schedule(admin,path,1));
        var paidAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        object Bill(long amount, string png=Png) => new { expectedRevision=1,paidAmountVnd=amount,paidAt,reference="BANK-"+lead.Split('/')[^1],
            bills=new[] {new {contentType="image/png", dataBase64=png}} };
        var confirm = path+"/payments/confirm";
        await Expect(HttpStatusCode.Forbidden,engineer.PostAsJsonAsync(confirm,Bill(200000)));
        await Expect(HttpStatusCode.UnprocessableEntity,admin.PostAsJsonAsync(confirm,Bill(199999)));
        await Expect(HttpStatusCode.BadRequest,admin.PostAsJsonAsync(confirm,Bill(200000,"AAAA")));
        await Expect(HttpStatusCode.Conflict,Schedule(admin,path,1));
        await OneTransition(()=>admin.PostAsJsonAsync(confirm,Bill(200000)));
        var verified = (await Data(admin.GetAsync(path))).GetProperty("current");
        Assert.Equal("CONFIRMED",verified.GetProperty("paymentStatus").GetString());
        var payment = verified.GetProperty("payment");
        Assert.Equal(200000L,payment.GetProperty("paidAmountVnd").GetInt64());
        Assert.Equal("BANK-"+lead.Split('/')[^1],payment.GetProperty("reference").GetString());
        var adminId = (await Data(admin.GetAsync("/api/auth/me"))).GetProperty("id").GetInt32();
        Assert.Equal(adminId,payment.GetProperty("verifiedBy").GetInt32());
        Assert.True(payment.GetProperty("verifiedAt").GetDateTimeOffset() >= paidAt);
        Assert.Equal(Png,Assert.Single(payment.GetProperty("bills").EnumerateArray()).GetProperty("dataBase64").GetString());
        var engineerPayment = (await Data(engineer.GetAsync(path))).GetProperty("current").GetProperty("payment");
        Assert.True(!engineerPayment.TryGetProperty("bills",out var bills) || bills.ValueKind==JsonValueKind.Null);
        await Expect(HttpStatusCode.Forbidden,outsider.GetAsync(path));
        await Expect(HttpStatusCode.Conflict,Proposal(engineer,path,1,45,Version(preview)));
        await Expect(HttpStatusCode.Conflict,Survey(engineer,lead));
        await OneTransition(()=>Schedule(admin,path,1));
        await Expect(HttpStatusCode.Created,Survey(engineer,lead));
    }
}
