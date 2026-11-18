using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure;
using Microsoft.Extensions.Configuration;
using PayOS;
using System.Text.Json;

namespace FarmMonitoring.UnitTests;

public sealed class PayOsServiceTests
{
    [Fact]
    public async Task Checkout_uses_reserved_proposal_amount_and_reuses_active_link()
    {
        var repository = new FakeRepository();
        var client = new FakeClient();
        var service = new PayOsService(repository, client, new TestUser(9), TimeProvider.System);

        var created = await service.CheckoutAsync(44, 3, default);
        var repeated = await service.CheckoutAsync(44, 3, default);

        Assert.Equal(245000, client.LastAmount);
        Assert.Equal(created.OrderCode, repeated.OrderCode);
        Assert.Equal(1, client.CreateCalls);
        Assert.Equal("PENDING", repeated.Status);
    }

    [Fact]
    public async Task Provider_failure_leaves_order_for_reconciliation_and_does_not_retry_creation()
    {
        var repository = new FakeRepository();
        var client = new FakeClient { CreateException = new TimeoutException("provider timeout") };
        var service = new PayOsService(repository, client, new TestUser(9), TimeProvider.System);

        await Assert.ThrowsAsync<TimeoutException>(() => service.CheckoutAsync(44, 3, default));
        Assert.Equal("RECONCILIATION_REQUIRED", repository.Transaction!.Status);
        await Assert.ThrowsAsync<ConflictException>(() => service.CheckoutAsync(44, 3, default));
        Assert.Equal(1, client.CreateCalls);
    }

    [Fact]
    public async Task Webhook_passes_raw_body_to_signature_verifier_and_acknowledges()
    {
        var repository = new FakeRepository();
        var client = new FakeClient();
        var service = new PayOsService(repository, client, new TestUser(9), TimeProvider.System);
        const string raw = "{\"signature\":\"mock-only\"}";

        var result = await service.WebhookAsync(raw, default);

        Assert.Equal(raw, client.LastWebhookBody);
        Assert.Equal(0, result.Error);
        Assert.Equal(0, repository.WebhookCalls); // registration test payload must not enter payment processing
    }

    [Fact]
    public async Task Signed_payment_webhook_is_applied_by_repository_once()
    {
        var repository = new FakeRepository();
        var client = new FakeClient { Webhook = new PayOsVerifiedWebhook(true, "00", 987654321, 245000, "link-1", "ref-1", "2026-11-18 10:00:00") };
        var service = new PayOsService(repository, client, new TestUser(9), TimeProvider.System);

        var result = await service.WebhookAsync("raw-signed-body", default);

        Assert.Equal(0, result.Error);
        Assert.Equal(1, repository.WebhookCalls);
    }

    [Fact]
    public async Task Sdk_verifies_raw_payos_webhook_signature_without_network_access()
    {
        const string checksum = "unit-test-payOS-checksum-key";
        var client = new PayOSClient(new PayOSOptions { ClientId = "unit-test-client", ApiKey = "unit-test-api-key", ChecksumKey = checksum });
        var data = WebhookData();
        var signature = client.Crypto.CreateSignatureFromObject(data, checksum);
        var raw = JsonSerializer.Serialize(new { code = "00", desc = "success", success = true, data, signature }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var adapter = new PayOsSdkClient(Configuration(checksum));

        var verified = await adapter.VerifyWebhookAsync(raw, default);

        Assert.True(verified.Success);
        Assert.Equal(987654321, verified.OrderCode);
        Assert.Equal(245000, verified.Amount);
        Assert.Equal("link-1", verified.PaymentLinkId);
    }

    [Fact]
    public async Task Sdk_rejects_invalid_payos_webhook_signature()
    {
        var data = WebhookData();
        var raw = JsonSerializer.Serialize(new { code = "00", desc = "success", success = true, data, signature = "invalid" }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var adapter = new PayOsSdkClient(Configuration("unit-test-payOS-checksum-key"));

        await Assert.ThrowsAsync<PayOS.Exceptions.WebhookException>(() => adapter.VerifyWebhookAsync(raw, default));
    }

    private static IConfiguration Configuration(string checksum) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["PayOS:ClientId"] = "unit-test-client", ["PayOS:ApiKey"] = "unit-test-api-key", ["PayOS:ChecksumKey"] = checksum,
        ["PayOS:ReturnUrl"] = "https://example.test/return", ["PayOS:CancelUrl"] = "https://example.test/cancel"
    }).Build();

    private static object WebhookData() => new
    {
        orderCode = 987654321L, amount = 245000L, description = "TVVU-987654321", accountNumber = "12345678",
        reference = "test-provider-reference", transactionDateTime = "2026-11-18 17:00:00", currency = "VND",
        paymentLinkId = "link-1", code = "00", desc = "Thành công", counterAccountBankId = "",
        counterAccountBankName = "", counterAccountName = "", counterAccountNumber = "", virtualAccountName = "", virtualAccountNumber = ""
    };

    private sealed record TestUser(int Id) : ICurrentUser { public int? UserId => Id; }

    private sealed class FakeClient : IPayOsClient
    {
        public bool IsConfigured => true;
        public int CreateCalls { get; private set; }
        public long LastAmount { get; private set; }
        public string? LastWebhookBody { get; private set; }
        public Exception? CreateException { get; init; }
        public PayOsVerifiedWebhook Webhook { get; init; } = new(false, "01", 0, 0, "", "", "");
        public Task<PayOsLinkResult> CreatePaymentLinkAsync(long orderCode, long amountVnd, string description, string publicStatusToken, DateTimeOffset expiresAt, CancellationToken ct)
        {
            CreateCalls++;
            LastAmount = amountVnd;
            Assert.Matches("^[a-f0-9]{64}$", publicStatusToken);
            if (CreateException is not null) throw CreateException;
            return Task.FromResult(new PayOsLinkResult("link-1", "https://payos.example/checkout", "qr-data", expiresAt));
        }
        public Task<PayOsVerifiedWebhook> VerifyWebhookAsync(string rawJson, CancellationToken ct)
        {
            LastWebhookBody = rawJson;
            return Task.FromResult(Webhook);
        }
        public Task<PayOsProviderStatus> GetPaymentStatusAsync(long orderCode, CancellationToken ct) =>
            Task.FromResult(new PayOsProviderStatus("Pending", 0, "link-1", "", ""));
    }

    private sealed class FakeRepository : IPayOsRepository
    {
        public PayOsTransaction? Transaction { get; private set; }
        public int WebhookCalls { get; private set; }
        public Task<(PayOsTransaction Transaction, bool Created)> ReserveAsync(int consultationId, int revision, int actor, DateTimeOffset now, CancellationToken ct)
        {
            if (Transaction is not null)
            {
                if (Transaction.Status == "RECONCILIATION_REQUIRED") throw new ConflictException("unresolved");
                return Task.FromResult((Transaction, false));
            }
            Transaction = new PayOsTransaction { OrderCode = 987654321, ProposalId = 1, AmountVnd = 245000,
                PublicStatusToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
                CreatedBy = actor, CreatedAt = now, ExpiresAt = now.AddMinutes(30), Status = "CREATING" };
            return Task.FromResult((Transaction, true));
        }
        public Task SaveLinkAsync(long orderCode, string paymentLinkId, string checkoutUrl, string qrCode, DateTimeOffset? expiresAt, CancellationToken ct)
        {
            Transaction!.PaymentLinkId = paymentLinkId; Transaction.CheckoutUrl = checkoutUrl; Transaction.QrCode = qrCode; Transaction.Status = "PENDING";
            return Task.CompletedTask;
        }
        public Task MarkUncertainAsync(long orderCode, CancellationToken ct) { Transaction!.Status = "RECONCILIATION_REQUIRED"; return Task.CompletedTask; }
        public Task<PayOsTransaction?> FindAsync(long orderCode, int actor, bool administrator, CancellationToken ct) => Task.FromResult(Transaction);
        public Task<string?> GetPublicStatusAsync(string token, CancellationToken ct) => Task.FromResult<string?>(Transaction?.Status);
        public Task<bool> ApplyVerifiedAsync(PayOsVerifiedWebhook webhook, DateTimeOffset verifiedAt, CancellationToken ct) { WebhookCalls++; return Task.FromResult(false); }
        public Task ApplyReconciliationAsync(long orderCode, PayOsProviderStatus status, DateTimeOffset verifiedAt, CancellationToken ct) => Task.CompletedTask;
    }
}
