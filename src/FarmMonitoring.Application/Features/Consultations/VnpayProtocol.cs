using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Consultations;

public sealed class VnpaySettings
{
    public bool Enabled { get; set; }
    public string? TmnCode { get; set; }
    public string? HashSecret { get; set; }
    public string? ReturnUrl { get; set; }
    public int ExpiryMinutes { get; set; } = 15;
}

public sealed class VnpayProtocol(VnpaySettings settings)
{
    public const string PayEndpoint = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public const string QueryEndpoint = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
    public bool Enabled => settings.Enabled;
    public string TmnCode => settings.TmnCode ?? "";
    public int ExpiryMinutes => settings.ExpiryMinutes;
    private bool HasCredentials => settings.TmnCode is { Length: 8 }
        && settings.TmnCode.All(char.IsAsciiLetterOrDigit) && !string.IsNullOrWhiteSpace(settings.HashSecret);
    public bool IsConfigured => Enabled && HasCredentials && settings.ExpiryMinutes is >= 1 and <= 60
        && settings.ReturnUrl is { Length: >= 10 and <= 255 }
        && !settings.ReturnUrl.Any(char.IsWhiteSpace) && !settings.ReturnUrl.Any(char.IsControl)
        && !settings.ReturnUrl.Contains('\\')
        && Uri.TryCreate(settings.ReturnUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0
        && uri.Fragment.Length == 0 && uri.Query.Length == 0;

    public static string NewTransactionReference() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    public static long ToProviderAmount(long amountVnd)
    {
        if (amountVnd is <= 0 or > 9999999999)
            throw new ValidationException("VNPAY amount must be from 1 to 9999999999 VND.");
        return checked(amountVnd * 100);
    }

    public static string FormatTimestamp(DateTimeOffset value) =>
        value.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    public string BuildPayUrl(string txnRef, long amountVnd, DateTimeOffset createdAt,
        DateTimeOffset expiresAt, string ip, string returnToken)
    {
        EnsureConfigured();
        ValidateReferenceAndIp(txnRef, ip);
        if (expiresAt <= createdAt)
            throw new ValidationException("VNPAY expiry must be later than creation time.");
        if (string.IsNullOrWhiteSpace(returnToken))
            throw new ValidationException("Payment return token is required.");
        var returnUrl = settings.ReturnUrl + "?token=" + WebUtility.UrlEncode(returnToken);
        if (returnUrl.Length > 255)
            throw new ValidationException("VNPAY return URL exceeds 255 characters.");
        var values = new Dictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "pay", ["vnp_TmnCode"] = settings.TmnCode!,
            ["vnp_Amount"] = ToProviderAmount(amountVnd).ToString(CultureInfo.InvariantCulture),
            ["vnp_CreateDate"] = FormatTimestamp(createdAt), ["vnp_ExpireDate"] = FormatTimestamp(expiresAt),
            ["vnp_CurrCode"] = "VND", ["vnp_IpAddr"] = ip, ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = "Thanh toan phi khao sat " + txnRef, ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = returnUrl, ["vnp_TxnRef"] = txnRef
        };
        var data = CanonicalQuery(values);
        return PayEndpoint + "?" + data + "&vnp_SecureHash=" + Sign(data);
    }

    // Transport code must reject duplicate query keys before converting to a dictionary.
    public bool ValidateCallback(IReadOnlyDictionary<string, string> values) =>
        HasCredentials && values.GetValueOrDefault("vnp_TmnCode") == settings.TmnCode
        && Verify(CanonicalQuery(values), values.GetValueOrDefault("vnp_SecureHash"));

    public Dictionary<string, string> BuildQueryRequest(string txnRef, DateTimeOffset transactionCreatedAt,
        DateTimeOffset requestedAt, string serverIp)
    {
        EnsureConfigured();
        ValidateReferenceAndIp(txnRef, serverIp);
        var values = new Dictionary<string, string>
        {
            ["vnp_RequestId"] = NewTransactionReference(), ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "querydr", ["vnp_TmnCode"] = settings.TmnCode!, ["vnp_TxnRef"] = txnRef,
            ["vnp_TransactionDate"] = FormatTimestamp(transactionCreatedAt),
            ["vnp_CreateDate"] = FormatTimestamp(requestedAt), ["vnp_IpAddr"] = serverIp,
            ["vnp_OrderInfo"] = "Truy van " + txnRef
        };
        values["vnp_SecureHash"] = Sign(string.Join('|', values["vnp_RequestId"], values["vnp_Version"],
            values["vnp_Command"], values["vnp_TmnCode"], values["vnp_TxnRef"], values["vnp_TransactionDate"],
            values["vnp_CreateDate"], values["vnp_IpAddr"], values["vnp_OrderInfo"]));
        return values;
    }

    public bool ValidateQueryResponse(IReadOnlyDictionary<string, string> values)
    {
        if (!IsConfigured || values.GetValueOrDefault("vnp_TmnCode") != settings.TmnCode
            || values.GetValueOrDefault("vnp_Command") != "querydr") return false;
        // QueryDr signs ordered raw values, including empty optional promotion fields.
        string[] fields = ["vnp_ResponseId", "vnp_Command", "vnp_ResponseCode", "vnp_Message", "vnp_TmnCode",
            "vnp_TxnRef", "vnp_Amount", "vnp_BankCode", "vnp_PayDate", "vnp_TransactionNo",
            "vnp_TransactionType", "vnp_TransactionStatus", "vnp_OrderInfo", "vnp_PromotionCode", "vnp_PromotionAmount"];
        if (fields.Any(key => values.GetValueOrDefault(key)?.Contains('|') == true)) return false;
        return Verify(string.Join('|', fields.Select(key => values.GetValueOrDefault(key) ?? "")),
            values.GetValueOrDefault("vnp_SecureHash"));
    }

    private static string CanonicalQuery(IReadOnlyDictionary<string, string> values) => string.Join('&', values
        .Where(x => x.Key.StartsWith("vnp_", StringComparison.Ordinal)
            && x.Key is not ("vnp_SecureHash" or "vnp_SecureHashType") && !string.IsNullOrEmpty(x.Value))
        .OrderBy(x => x.Key, StringComparer.Ordinal)
        .Select(x => WebUtility.UrlEncode(x.Key) + "=" + WebUtility.UrlEncode(x.Value)));

    private string Sign(string data) => Convert.ToHexString(HMACSHA512.HashData(
        Encoding.UTF8.GetBytes(settings.HashSecret!), Encoding.UTF8.GetBytes(data))).ToLowerInvariant();

    private bool Verify(string data, string? signature)
    {
        if (signature is not { Length: 128 } || !signature.All(char.IsAsciiHexDigit)) return false;
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Sign(data)), Convert.FromHexString(signature));
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured) throw new ValidationException("VNPAY Sandbox is not configured or is disabled.");
    }

    private static void ValidateReferenceAndIp(string txnRef, string ip)
    {
        if (txnRef.Length is < 1 or > 100 || !txnRef.All(char.IsAsciiLetterOrDigit))
            throw new ValidationException("Invalid VNPAY transaction reference.");
        if (!IPAddress.TryParse(ip, out _)) throw new ValidationException("Invalid payment IP address.");
    }
}
