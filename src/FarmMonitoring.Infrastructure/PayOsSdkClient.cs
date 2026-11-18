using FarmMonitoring.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using Microsoft.Extensions.Logging;

namespace FarmMonitoring.Infrastructure;

public sealed class PayOsSdkClient(IConfiguration configuration, ILogger<PayOsSdkClient>? logger = null) : IPayOsClient
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["PayOS:ClientId"])
        && !string.IsNullOrWhiteSpace(configuration["PayOS:ApiKey"])
        && !string.IsNullOrWhiteSpace(configuration["PayOS:ChecksumKey"])
        && Uri.TryCreate(configuration["PayOS:ReturnUrl"], UriKind.Absolute, out var returnUri) && returnUri.Scheme == Uri.UriSchemeHttps
        && Uri.TryCreate(configuration["PayOS:CancelUrl"], UriKind.Absolute, out var cancelUri) && cancelUri.Scheme == Uri.UriSchemeHttps;
    private PayOSClient Client()
    {
        var clientId = configuration["PayOS:ClientId"];
        var apiKey = configuration["PayOS:ApiKey"];
        var checksumKey = configuration["PayOS:ChecksumKey"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(checksumKey))
            throw new InvalidOperationException("PayOS credentials are not configured.");
        return new PayOSClient(new PayOSOptions { ClientId = clientId, ApiKey = apiKey, ChecksumKey = checksumKey });
    }

    public async Task<PayOsLinkResult> CreatePaymentLinkAsync(long orderCode, long amountVnd, string description, string publicStatusToken, DateTimeOffset expiresAt, CancellationToken ct)
    {
        var returnUrl = configuration["PayOS:ReturnUrl"];
        var cancelUrl = configuration["PayOS:CancelUrl"];
        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var ret) || ret.Scheme != Uri.UriSchemeHttps
            || !Uri.TryCreate(cancelUrl, UriKind.Absolute, out var cancel) || cancel.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("PayOS ReturnUrl and CancelUrl must be configured HTTPS URLs.");
        var response = await Client().PaymentRequests.CreateAsync(new CreatePaymentLinkRequest
        {
            OrderCode = orderCode, Amount = amountVnd, Description = description,
            ReturnUrl = AddToken(returnUrl, publicStatusToken), CancelUrl = AddToken(cancelUrl, publicStatusToken), ExpiredAt = expiresAt.ToUnixTimeSeconds()
        });
        return new(response.PaymentLinkId, response.CheckoutUrl, response.QrCode,
            response.ExpiredAt is long epoch ? DateTimeOffset.FromUnixTimeSeconds(epoch) : expiresAt);
    }

    private static string AddToken(string url, string token)
    {
        var builder = new UriBuilder(url);
        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
        query["token"] = token;
        builder.Query = query.ToString();
        return builder.Uri.ToString();
    }

    public async Task<PayOsVerifiedWebhook> VerifyWebhookAsync(string rawJson, CancellationToken ct)
    {
        PayOS.Models.Webhooks.WebhookData webhook;
        try
        {
            webhook = await Client().Webhooks.VerifyAsync(rawJson);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning("PayOS webhook signature or payload verification failed. ExceptionType={ExceptionType}", ex.GetType().Name);
            throw;
        }
        using var envelope = JsonDocument.Parse(rawJson);
        var root = envelope.RootElement;
        var envelopeSuccess = root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True
            && root.TryGetProperty("code", out var code) && code.GetString() == "00";
        logger?.LogInformation("PayOS webhook signature verified. CallbackKind={CallbackKind}; ProviderSuccess={ProviderSuccess}; ProviderCodeIsSuccess={ProviderCodeIsSuccess}",
            webhook.OrderCode <= 0 ? "Registration" : "Payment", envelopeSuccess, webhook.Code == "00");
        return new(envelopeSuccess && webhook.Code == "00", webhook.Code, webhook.OrderCode,
            webhook.Amount, webhook.PaymentLinkId ?? "", webhook.Reference ?? "", webhook.TransactionDateTime ?? "");
    }

    public async Task<PayOsProviderStatus> GetPaymentStatusAsync(long orderCode, CancellationToken ct)
    {
        var result = await Client().PaymentRequests.GetAsync(orderCode);
        var transaction = result.Transactions?.LastOrDefault();
        return new(result.Status.ToString(), result.AmountPaid, result.Id, transaction?.Reference ?? "",
            transaction?.TransactionDateTime ?? "");
    }
}
