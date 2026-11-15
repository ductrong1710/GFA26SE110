using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public sealed class VnpayTests(ApiFactory factory)
{
    private const string Merchant = "TESTVNP1";
    private const string Secret = "fictional-vnpay-integration-test-secret";
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS3sAAAAASUVORK5CYII=";

    private WebApplicationFactory<Program> Host(bool enabled = true) => factory.WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Vnpay:Enabled"] = enabled.ToString(), ["Vnpay:TmnCode"] = Merchant,
            ["Vnpay:HashSecret"] = Secret, ["Vnpay:ReturnUrl"] = "https://frontend.example/payments/vnpay/return",
            ["Vnpay:ExpiryMinutes"] = "15"
        })));

    private static HttpClient Client(WebApplicationFactory<Program> host) => host.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
    });

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

    private static async Task<HttpClient> Login(WebApplicationFactory<Program> host, string role)
    {
        var client = Client(host);
        var login = await Data(client.PostAsJsonAsync("/api/auth/login", new { email = role + "@example.com", password = "Test-password-123!" }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task Propose(HttpClient engineer, string path, int revision, decimal km, long? manualAmountVnd = null)
    {
        var preview = await Data(engineer.PostAsJsonAsync(path + "/fee-preview", new { officeCode = "OFFICE_1", distanceKm = km }));
        await Data(engineer.PostAsJsonAsync(path + "/proposals", new
        {
            expectedRevision = revision, officeCode = "OFFICE_1", distanceKm = km,
            mapsUrl = "https://maps.app.goo.gl/example", proposedAt = DateTimeOffset.UtcNow.AddDays(4),
            pricingVersion = preview.GetProperty("pricingVersion").GetString(), manualAmountVnd,
            manualReason = manualAmountVnd.HasValue ? "Ferry and long route" : null
        }));
    }

    private static async Task<string> Visit(HttpClient engineer, HttpClient admin, bool approve = true, decimal km = 45, long? manualAmountVnd = null)
    {
        var lead = await Data(engineer.PostAsJsonAsync("/api/consultations", new
        {
            customerName = "Private VNPay customer", phone = "0901234567", farmLocation = "Can Tho",
            content = "Call before visiting", serviceNeed = "Sensor monitoring", channel = "ON_SITE"
        }));
        var path = $"/api/consultations/{lead.GetProperty("id").GetInt32()}";
        await Data(engineer.PostAsJsonAsync(path + "/claim", new { }));
        path += "/site-visit";
        await Propose(engineer, path, 0, km, manualAmountVnd);
        if (approve) await Data(admin.PostAsJsonAsync(path + "/review", new { expectedRevision = 1, decision = "APPROVE" }));
        return path;
    }

    private static Task<HttpResponseMessage> Checkout(HttpClient admin, string path, int revision = 1) =>
        admin.PostAsJsonAsync(path + "/payments/vnpay/checkout", new { expectedRevision = revision });

    private static Task<HttpResponseMessage> Manual(HttpClient admin, string path, int revision = 1) =>
        admin.PostAsJsonAsync(path + "/payments/confirm", new
        {
            expectedRevision = revision, paidAmountVnd = 200000, paidAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            reference = "MANUAL-" + Guid.NewGuid().ToString("N"), bills = new[] { new { contentType = "image/png", dataBase64 = Png } }
        });

    private static Dictionary<string, string> Parameters(string url) => QueryHelpers.ParseQuery(new Uri(url).Query)
        .ToDictionary(p => p.Key, p => p.Value.ToString(), StringComparer.Ordinal);

    // Fixtures implement VNPay's encoding independently of the production signer.
    private static string Query(IEnumerable<KeyValuePair<string, string>> fields) => string.Join("&", fields
        .OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => WebUtility.UrlEncode(p.Key) + "=" + WebUtility.UrlEncode(p.Value)));

    private static string Signature(IEnumerable<KeyValuePair<string, string>> fields) => Convert.ToHexString(
        HMACSHA512.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(Query(fields)))).ToLowerInvariant();

    private static Dictionary<string, string> Callback(JsonElement checkout, string responseCode = "00", string transactionStatus = "00") => new()
    {
        ["vnp_TmnCode"] = Merchant, ["vnp_TxnRef"] = checkout.GetProperty("txnRef").GetString()!,
        ["vnp_Amount"] = "20000000", ["vnp_ResponseCode"] = responseCode,
        ["vnp_TransactionStatus"] = transactionStatus, ["vnp_TransactionNo"] = "123456789",
        ["vnp_BankCode"] = "NCB", ["vnp_OrderInfo"] = "Khảo sát + vườn & đất=1",
        ["vnp_PayDate"] = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss")
    };

    private static string IpnUrl(Dictionary<string, string> fields) => "/api/payments/vnpay/ipn?" + Query(fields) + "&vnp_SecureHash=" + Signature(fields);

    private static async Task<string> Ipn(HttpClient visitor, string url)
    {
        using var response = await visitor.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "Message", "RspCode" }, result.EnumerateObject().Select(p => p.Name).Order().ToArray());
        return result.GetProperty("RspCode").GetString()!;
    }

    private static string Token(JsonElement checkout) => Parameters(Parameters(checkout.GetProperty("checkoutUrl").GetString()!)["vnp_ReturnUrl"])["token"];

    private static async Task Status(HttpClient visitor, JsonElement checkout, string expected)
    {
        var result = await Data(visitor.GetAsync("/api/payments/vnpay/status?token=" + Uri.EscapeDataString(Token(checkout))));
        Assert.Equal(new[] { "sandbox", "status" }, result.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.True(result.GetProperty("sandbox").GetBoolean());
        Assert.Equal(expected, result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Signed_checkout_is_private_and_return_cannot_mark_payment_successful()
    {
        await using var host = Host();
        using var admin = await Login(host, "admin"); using var engineer = await Login(host, "engineer"); using var visitor = Client(host);
        var path = await Visit(engineer, admin);
        var checkout = await Data(Checkout(admin, path));
        Assert.True(checkout.GetProperty("sandbox").GetBoolean());
        Assert.Equal("PENDING", checkout.GetProperty("status").GetString());
        Assert.InRange(checkout.GetProperty("expiresAt").GetDateTimeOffset(), DateTimeOffset.UtcNow.AddMinutes(14), DateTimeOffset.UtcNow.AddMinutes(16));
        var url = new Uri(checkout.GetProperty("checkoutUrl").GetString()!);
        Assert.Equal("https", url.Scheme); Assert.Equal("sandbox.vnpayment.vn", url.Host);
        var fields = Parameters(url.ToString());
        var signature = fields["vnp_SecureHash"]; fields.Remove("vnp_SecureHash");
        Assert.Equal(Signature(fields), signature, ignoreCase: true);
        Assert.Equal("20000000", fields["vnp_Amount"]); Assert.Equal("VND", fields["vnp_CurrCode"]);
        Assert.Equal(Merchant, fields["vnp_TmnCode"]);
        Assert.DoesNotContain("Private VNPay customer", url.ToString()); Assert.DoesNotContain("0901234567", url.ToString());
        Assert.True(Token(checkout).Length >= 32);
        await Status(visitor, checkout, "PENDING");
        var returnQuery = Query(Callback(checkout));
        await Data(visitor.GetAsync("/api/payments/vnpay/status?token=" + Token(checkout) + "&" + returnQuery));
        await Status(visitor, checkout, "PENDING");
        var before = (await Data(admin.GetAsync(path))).GetProperty("current");
        Assert.Equal(JsonValueKind.Null, before.GetProperty("payment").ValueKind);
        await Expect(HttpStatusCode.Conflict, admin.PostAsJsonAsync(path + "/schedule", new { expectedRevision = 1 }));
        Assert.Equal("00", await Ipn(visitor, IpnUrl(Callback(checkout))));
        await Status(visitor, checkout, "SUCCEEDED");
        var after = (await Data(admin.GetAsync(path))).GetProperty("current");
        Assert.Equal("CONFIRMED", after.GetProperty("paymentStatus").GetString());
        Assert.Equal(200000L, after.GetProperty("payment").GetProperty("paidAmountVnd").GetInt64());
        Assert.Equal(JsonValueKind.Null, after.GetProperty("scheduledAt").ValueKind);
        await Data(admin.PostAsJsonAsync(path + "/schedule", new { expectedRevision = 1 }));
    }

    [Fact]
    public async Task Invalid_callbacks_cannot_settle_and_duplicate_success_creates_one_receipt()
    {
        await using var host = Host();
        using var admin = await Login(host, "admin"); using var engineer = await Login(host, "engineer"); using var visitor = Client(host);
        var path = await Visit(engineer, admin); var checkout = await Data(Checkout(admin, path));
        Assert.Equal("97", await Ipn(visitor, IpnUrl(Callback(checkout)).Replace("vnp_Amount=20000000", "vnp_Amount=100")));
        var fields = Callback(checkout); fields["vnp_TxnRef"] = "MISSING";
        Assert.Equal("01", await Ipn(visitor, IpnUrl(fields)));
        fields = Callback(checkout); fields["vnp_Amount"] = "19999900";
        Assert.Equal("04", await Ipn(visitor, IpnUrl(fields)));
        fields = Callback(checkout); fields["vnp_TmnCode"] = "OTHER";
        Assert.Equal("97", await Ipn(visitor, IpnUrl(fields)));
        await Status(visitor, checkout, "PENDING");
        var results = await Task.WhenAll(Ipn(visitor, IpnUrl(Callback(checkout))), Ipn(visitor, IpnUrl(Callback(checkout))));
        Assert.Equal(new[] { "00", "02" }, results.Order().ToArray());
        Assert.Equal("02", await Ipn(visitor, IpnUrl(Callback(checkout))));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = int.Parse(path.Split('/')[3]);
        var proposal = await db.Set<SiteVisitProposal>().SingleAsync(p => p.ConsultationId == id);
        Assert.Equal(1, await db.Set<SiteVisitPayment>().CountAsync(p => p.ProposalId == proposal.Id));
    }

    [Fact]
    public async Task Checkout_requires_admin_enabled_gateway_current_approved_positive_quote()
    {
        await using var host = Host();
        using var admin = await Login(host, "admin"); using var engineer = await Login(host, "engineer"); using var visitor = Client(host);
        var path = await Visit(engineer, admin, approve: false);
        await Expect(HttpStatusCode.Unauthorized, Checkout(visitor, path));
        await Expect(HttpStatusCode.Forbidden, Checkout(engineer, path));
        await Expect(HttpStatusCode.Conflict, Checkout(admin, path));
        await Data(admin.PostAsJsonAsync(path + "/review", new { expectedRevision = 1, decision = "APPROVE" }));
        await Expect(HttpStatusCode.Conflict, Checkout(admin, path, 2));
        var free = await Visit(engineer, admin, km: 5);
        await Expect(HttpStatusCode.Conflict, Checkout(admin, free));
        await Data(engineer.PostAsJsonAsync(free + "/proposals", new { expectedRevision = 1, channel = "ONLINE", reason = "Customer chooses online" }));
        await Data(admin.PostAsJsonAsync(free + "/review", new { expectedRevision = 2, decision = "APPROVE" }));
        await Expect(HttpStatusCode.Conflict, Checkout(admin, free, 2));
        await using var disabled = Host(false);
        using var disabledAdmin = await Login(disabled, "admin");
        using var rejected = await Checkout(disabledAdmin, path);
        Assert.False(rejected.IsSuccessStatusCode);
        var checkout = await Data(Checkout(admin, path));
        await Expect(HttpStatusCode.Conflict, Manual(disabledAdmin, path));
        var reused = await Data(Checkout(admin, path));
        Assert.Equal(checkout.GetProperty("txnRef").GetString(), reused.GetProperty("txnRef").GetString());
        Assert.Equal(checkout.GetProperty("checkoutUrl").GetString(), reused.GetProperty("checkoutUrl").GetString());
        await Status(visitor, checkout, "PENDING");
        using var disabledVisitor = Client(disabled);
        Assert.Equal("00", await Ipn(disabledVisitor, IpnUrl(Callback(checkout))));
        await Status(disabledVisitor, checkout, "SUCCEEDED");
    }

    [Theory]
    [InlineData("24", "02", "FAILED")]
    [InlineData("00", "01", "RECONCILIATION_REQUIRED")]
    [InlineData("24", "00", "RECONCILIATION_REQUIRED")]
    public async Task Definitive_failure_allows_retry_but_mixed_provider_statuses_block_payment(string response, string transaction, string expected)
    {
        await using var host = Host();
        using var admin = await Login(host, "admin"); using var engineer = await Login(host, "engineer"); using var visitor = Client(host);
        var path = await Visit(engineer, admin); var checkout = await Data(Checkout(admin, path));
        Assert.Equal("00", await Ipn(visitor, IpnUrl(Callback(checkout, response, transaction))));
        await Status(visitor, checkout, expected);
        Assert.Equal(JsonValueKind.Null, (await Data(admin.GetAsync(path))).GetProperty("current").GetProperty("payment").ValueKind);
        if (expected == "FAILED")
        {
            var retry = await Data(Checkout(admin, path));
            Assert.NotEqual(checkout.GetProperty("txnRef").GetString(), retry.GetProperty("txnRef").GetString());
            Assert.NotEqual(Token(checkout), Token(retry));
        }
        else
        {
            await Expect(HttpStatusCode.Conflict, Checkout(admin, path));
            await Expect(HttpStatusCode.Conflict, Manual(admin, path));
        }
    }

    [Fact]
    public async Task Success_for_replaced_revision_requires_reconciliation_and_blocks_new_revision_payment()
    {
        await using var host = Host();
        using var admin = await Login(host, "admin"); using var engineer = await Login(host, "engineer"); using var visitor = Client(host);
        var path = await Visit(engineer, admin); var checkout = await Data(Checkout(admin, path));
        await Propose(engineer, path, 1, 45);
        await Data(admin.PostAsJsonAsync(path + "/review", new { expectedRevision = 2, decision = "APPROVE" }));
        await Expect(HttpStatusCode.Conflict, Checkout(admin, path, 2));
        Assert.Equal("00", await Ipn(visitor, IpnUrl(Callback(checkout))));
        await Status(visitor, checkout, "RECONCILIATION_REQUIRED");
        await Expect(HttpStatusCode.Conflict, Checkout(admin, path, 2));
        await Expect(HttpStatusCode.Conflict, Manual(admin, path, 2));
        await Expect(HttpStatusCode.Conflict, admin.PostAsJsonAsync(path + "/schedule", new { expectedRevision = 2 }));
        var state = await Data(admin.GetAsync(path));
        Assert.All(state.GetProperty("history").EnumerateArray(), proposal => Assert.Equal(JsonValueKind.Null, proposal.GetProperty("payment").ValueKind));
    }

    [Fact]
    public async Task Concurrent_checkout_and_manual_confirmation_cannot_create_two_payment_paths()
    {
        await using var host = Host();
        using var admin = await Login(host, "admin"); using var engineer = await Login(host, "engineer");
        var path = await Visit(engineer, admin);
        var responses = await Task.WhenAll(Checkout(admin, path), Manual(admin, path));
        try
        {
            Assert.Single(responses, response => response.IsSuccessStatusCode);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Expiry_reports_unknown_and_blocks_payment_until_late_valid_ipn_arrives()
    {
        await using var host = Host();
        using var admin = await Login(host, "admin"); using var engineer = await Login(host, "engineer"); using var visitor = Client(host);
        var path = await Visit(engineer, admin); var checkout = await Data(Checkout(admin, path));
        var txnRef = checkout.GetProperty("txnRef").GetString();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Set<VnpayTransaction>().SingleAsync(t => t.TxnRef == txnRef);
            row.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20);
            row.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }
        await Status(visitor, checkout, "UNKNOWN");
        await Expect(HttpStatusCode.Conflict, Checkout(admin, path));
        await Expect(HttpStatusCode.Conflict, Manual(admin, path));
        await Expect(HttpStatusCode.Conflict, admin.PostAsJsonAsync(path + "/schedule", new { expectedRevision = 1 }));
        var callback = Callback(checkout);
        callback["vnp_PayDate"] = DateTimeOffset.UtcNow.AddMinutes(-10).ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss");
        Assert.Equal("00", await Ipn(visitor, IpnUrl(callback)));
        await Status(visitor, checkout, "SUCCEEDED");
    }

    [Fact]
    public async Task Ipn_competing_with_manual_bill_keeps_only_provider_receipt()
    {
        await using var host = Host();
        using var admin = await Login(host, "admin"); using var engineer = await Login(host, "engineer"); using var visitor = Client(host);
        var path = await Visit(engineer, admin); var checkout = await Data(Checkout(admin, path));
        var ipn = Ipn(visitor, IpnUrl(Callback(checkout)));
        var manual = Expect(HttpStatusCode.Conflict, Manual(admin, path));
        await Task.WhenAll(ipn, manual);
        Assert.Equal("00", await ipn);
        var payment = (await Data(admin.GetAsync(path))).GetProperty("current").GetProperty("payment");
        Assert.Equal("VNPAY_SANDBOX", payment.GetProperty("source").GetString());
        Assert.Equal(JsonValueKind.Null, payment.GetProperty("verifiedBy").ValueKind);
        Assert.Equal(JsonValueKind.Null, payment.GetProperty("bills").ValueKind);
        await Expect(HttpStatusCode.Conflict, Checkout(admin, path));
    }

    [Fact]
    public async Task Concurrent_checkouts_reuse_one_attempt_and_invalid_public_token_reveals_nothing()
    {
        await using var host = Host();
        using var admin = await Login(host, "admin"); using var engineer = await Login(host, "engineer"); using var visitor = Client(host);
        var path = await Visit(engineer, admin);
        var checkouts = await Task.WhenAll(Data(Checkout(admin, path)), Data(Checkout(admin, path)));
        Assert.Equal(checkouts[0].GetProperty("txnRef").GetString(), checkouts[1].GetProperty("txnRef").GetString());
        await Expect(HttpStatusCode.NotFound, visitor.GetAsync("/api/payments/vnpay/status?token=" + new string('a', 64)));
        await Expect(HttpStatusCode.NotFound, visitor.GetAsync("/api/payments/vnpay/status?token=123"));
        await Expect(HttpStatusCode.NotFound, visitor.GetAsync("/api/payments/vnpay/status?token=" + checkouts[0].GetProperty("txnRef").GetString()));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = int.Parse(path.Split('/')[3]);
        var proposal = await db.Set<SiteVisitProposal>().SingleAsync(p => p.ConsultationId == id);
        Assert.Equal(1, await db.Set<VnpayTransaction>().CountAsync(t => t.ProposalId == proposal.Id));
    }

    [Fact]
    public async Task Contradictory_callback_after_definitive_failure_requires_reconciliation()
    {
        await using var host = Host();
        using var admin = await Login(host, "admin"); using var engineer = await Login(host, "engineer"); using var visitor = Client(host);
        var path = await Visit(engineer, admin); var checkout = await Data(Checkout(admin, path));
        var failed = IpnUrl(Callback(checkout, "24", "02"));
        Assert.Equal("00", await Ipn(visitor, failed));
        Assert.Equal("02", await Ipn(visitor, failed));
        Assert.Equal("00", await Ipn(visitor, IpnUrl(Callback(checkout))));
        await Status(visitor, checkout, "RECONCILIATION_REQUIRED");
        await Expect(HttpStatusCode.Conflict, Checkout(admin, path));
        await Expect(HttpStatusCode.Conflict, Manual(admin, path));
        Assert.Equal(JsonValueKind.Null, (await Data(admin.GetAsync(path))).GetProperty("current").GetProperty("payment").ValueKind);
    }

    [Fact]
    public async Task Manual_quote_beyond_150km_uses_approved_amount_for_checkout_and_receipt()
    {
        await using var host = Host();
        using var admin = await Login(host, "admin"); using var engineer = await Login(host, "engineer"); using var visitor = Client(host);
        var path = await Visit(engineer, admin, km: 165, manualAmountVnd: 620000);
        var checkout = await Data(Checkout(admin, path));
        Assert.Equal("62000000", Parameters(checkout.GetProperty("checkoutUrl").GetString()!)["vnp_Amount"]);
        Assert.Equal("04", await Ipn(visitor, IpnUrl(Callback(checkout))));
        var callback = Callback(checkout); callback["vnp_Amount"] = "62000000";
        Assert.Equal("00", await Ipn(visitor, IpnUrl(callback)));
        await Status(visitor, checkout, "SUCCEEDED");
        var current = (await Data(admin.GetAsync(path))).GetProperty("current");
        Assert.Equal(620000L, current.GetProperty("payment").GetProperty("paidAmountVnd").GetInt64());
    }

    [Fact]
    public async Task Contradictory_old_attempt_blocks_success_of_replacement_attempt()
    {
        await using var host = Host();
        using var admin = await Login(host, "admin"); using var engineer = await Login(host, "engineer"); using var visitor = Client(host);
        var path = await Visit(engineer, admin);
        var first = await Data(Checkout(admin, path));
        Assert.Equal("00", await Ipn(visitor, IpnUrl(Callback(first, "24", "02"))));
        var second = await Data(Checkout(admin, path));
        Assert.Equal("00", await Ipn(visitor, IpnUrl(Callback(first))));
        Assert.Equal("00", await Ipn(visitor, IpnUrl(Callback(second))));
        await Status(visitor, first, "RECONCILIATION_REQUIRED");
        await Status(visitor, second, "RECONCILIATION_REQUIRED");
        Assert.Equal(JsonValueKind.Null, (await Data(admin.GetAsync(path))).GetProperty("current").GetProperty("payment").ValueKind);
        await Expect(HttpStatusCode.Conflict, Manual(admin, path));
        await Expect(HttpStatusCode.Conflict, admin.PostAsJsonAsync(path + "/schedule", new { expectedRevision = 1 }));
    }

    [Fact]
    public async Task Migration_preserves_manual_receipt_and_bill_and_refuses_to_drop_vnpay_history()
    {
        const string priorMigration = "20261113064256_CropSupportFollowUp";
        await using var previous = new ApiFactory { InitialMigration = priorMigration };
        await previous.InitializeAsync();
        await using var scope = previous.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bills = JsonSerializer.Serialize(new[] { new { contentType = "image/png", dataBase64 = Png } });
        // Historical schemas must be seeded without today's payment entity columns.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO consultations (customer_name,phone,farm_location,service_need,content,status,created_at,channel)
            VALUES ('Historical paid visit','0901234567','Can Tho','Survey','Existing receipt','IN_PROGRESS',now(),'ON_SITE');
            INSERT INTO site_visit_proposals
                ("ConsultationId","Revision","Channel","AmountVnd","PricingMethod","CreatedBy","CreatedAt","ReviewStatus")
            SELECT c.id,1,'ON_SITE',200000,'AUTOMATIC',u.id,now(),'APPROVED'
            FROM consultations c CROSS JOIN users u WHERE u.email='admin@example.com';
            INSERT INTO site_visit_payments
                ("ProposalId","PaidAmountVnd","PaidAt","Reference","VerifiedAt","VerifiedBy")
            SELECT p."Id",200000,'2026-11-01T03:00:00Z','HISTORICAL-BANK-001','2026-11-01T04:00:00Z',u.id
            FROM site_visit_proposals p CROSS JOIN users u WHERE u.email='admin@example.com';
            INSERT INTO site_visit_bills ("ProposalId","ImagesJson")
            SELECT "Id",{bills}::jsonb FROM site_visit_proposals;
            """);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync();
        var payment = await db.Set<SiteVisitPayment>().AsNoTracking().Include(p => p.Bill).SingleAsync();
        var admin = await db.Users.SingleAsync(u => u.Email == "admin@example.com");
        Assert.Equal("MANUAL", payment.Source);
        Assert.Equal(admin.Id, payment.VerifiedBy);
        Assert.Null(payment.VnpayTxnRef);
        Assert.Equal(200000L, payment.PaidAmountVnd);
        Assert.Equal("HISTORICAL-BANK-001", payment.Reference);
        Assert.Equal(DateTimeOffset.Parse("2026-11-01T03:00:00Z"), payment.PaidAt);
        Assert.Equal(DateTimeOffset.Parse("2026-11-01T04:00:00Z"), payment.VerifiedAt);
        Assert.NotNull(payment.Bill);
        var image = Assert.Single(JsonSerializer.Deserialize<JsonElement>(payment.Bill.ImagesJson).EnumerateArray());
        Assert.Equal("image/png", image.GetProperty("contentType").GetString());
        Assert.Equal(Png, image.GetProperty("dataBase64").GetString());
        Assert.False(await db.Set<VnpayTransaction>().AnyAsync());
        db.Set<VnpayTransaction>().Add(new()
        {
            ProposalId = payment.ProposalId, TxnRef = "HISTORICAL-FAILED-ATTEMPT", TmnCode = Merchant,
            AmountVnd = 200000, CreatedBy = admin.Id, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-15), PublicToken = new string('b', 64),
            CheckoutUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html", Status = "FAILED"
        });
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(priorMigration));
        Assert.Equal("P0001", error.SqlState);
        Assert.Contains("Cannot remove VNPAY audit history", error.MessageText);
        Assert.Equal(1, await db.Set<VnpayTransaction>().CountAsync());
        Assert.Equal("MANUAL", (await db.Set<SiteVisitPayment>().AsNoTracking().SingleAsync()).Source);
        Assert.Equal(1, await db.Set<SiteVisitBill>().CountAsync());
    }
}
