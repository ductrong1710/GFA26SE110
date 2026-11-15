using System.Net;
using System.Security.Cryptography;
using System.Text;
using FarmMonitoring.Application.Features.Consultations;
using FluentValidation;

namespace FarmMonitoring.UnitTests;

public class VnpayProtocolTests
{
    private const string Secret = "unit-test-only-vnpay-secret";
    private static readonly DateTimeOffset Created = new(2026, 11, 14, 18, 1, 2, TimeSpan.Zero);
    private static VnpaySettings Settings() => new()
    {
        Enabled = true, TmnCode = "TEST1234", HashSecret = Secret,
        ReturnUrl = "https://frontend.example/payment-return"
    };

    [Fact]
    public void Pay_url_uses_sandbox_exact_encoded_signature_integer_amount_and_vietnam_time()
    {
        var url = new VnpayProtocol(Settings()).BuildPayUrl("ABC123", 200000, Created,
            Created.AddMinutes(15), "127.0.0.1", "token123");
        const string signed = "vnp_Amount=20000000&vnp_Command=pay&vnp_CreateDate=20261115010102"
            + "&vnp_CurrCode=VND&vnp_ExpireDate=20261115011602&vnp_IpAddr=127.0.0.1"
            + "&vnp_Locale=vn&vnp_OrderInfo=Thanh+toan+phi+khao+sat+ABC123&vnp_OrderType=other"
            + "&vnp_ReturnUrl=https%3A%2F%2Ffrontend.example%2Fpayment-return%3Ftoken%3Dtoken123"
            + "&vnp_TmnCode=TEST1234&vnp_TxnRef=ABC123&vnp_Version=2.1.0";
        Assert.Equal(VnpayProtocol.PayEndpoint + "?" + signed + "&vnp_SecureHash=" + Hash(signed), url);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000000000)]
    [InlineData(long.MaxValue)]
    public void Amount_outside_provider_twelve_digit_wire_limit_is_rejected(long amount) =>
        Assert.Throws<ValidationException>(() => VnpayProtocol.ToProviderAmount(amount));

    [Fact]
    public void Largest_whole_vnd_amount_fits_provider_limit() =>
        Assert.Equal(999999999900, VnpayProtocol.ToProviderAmount(9999999999));

    [Fact]
    public void Disabled_or_missing_credentials_cannot_create_payment()
    {
        var protocol = new VnpayProtocol(new VnpaySettings());
        Assert.False(protocol.Enabled);
        Assert.False(protocol.IsConfigured);
        Assert.Throws<ValidationException>(() => protocol.BuildPayUrl("ABC123", 200000, Created,
            Created.AddMinutes(15), "127.0.0.1", "token123"));
    }

    [Theory]
    [InlineData("http://localhost/payment-return")]
    [InlineData("https://user:password@frontend.example/payment-return")]
    [InlineData("https://frontend.example/payment-return#fragment")]
    [InlineData("https://frontend.example/payment-return?token=old")]
    public void Invalid_return_configuration_is_unavailable(string returnUrl)
    {
        var settings = Settings();
        settings.ReturnUrl = returnUrl;
        Assert.False(new VnpayProtocol(settings).IsConfigured);
    }

    [Fact]
    public void Callback_signature_uses_ordinal_keys_form_encoding_and_excludes_hash_metadata()
    {
        var callback = Callback();
        const string signed = "vnp_Amount=20000000&vnp_OrderInfo=Phi+khao+sat+%2B+%26+%2F"
            + "&vnp_ResponseCode=00&vnp_TmnCode=TEST1234&vnp_TransactionStatus=00&vnp_TxnRef=ABC123";
        callback["vnp_SecureHash"] = Hash(signed).ToUpperInvariant();
        callback["vnp_SecureHashType"] = "SHA512";
        callback["token"] = "not-provider-data";
        callback["vnp_Empty"] = "";
        var protocol = new VnpayProtocol(Settings());
        Assert.True(protocol.ValidateCallback(callback));
        callback["vnp_Amount"] = "1";
        Assert.False(protocol.ValidateCallback(callback));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abcd")]
    [InlineData("not-a-hex-signature")]
    public void Malformed_callback_hash_is_rejected_without_throwing(string signature)
    {
        var callback = Callback();
        callback["vnp_SecureHash"] = signature;
        Assert.False(new VnpayProtocol(Settings()).ValidateCallback(callback));
    }

    [Fact]
    public void Disabling_checkout_preserves_signed_callback_processing_for_existing_attempts()
    {
        var settings = Settings();
        settings.Enabled = false;
        settings.ReturnUrl = null;
        var callback = Callback();
        callback["vnp_SecureHash"] = Hash("vnp_Amount=20000000&vnp_OrderInfo=Phi+khao+sat+%2B+%26+%2F"
            + "&vnp_ResponseCode=00&vnp_TmnCode=TEST1234&vnp_TransactionStatus=00&vnp_TxnRef=ABC123");
        var protocol = new VnpayProtocol(settings);
        Assert.False(protocol.IsConfigured);
        Assert.True(protocol.ValidateCallback(callback));
    }

    [Fact]
    public void Correct_signature_for_different_merchant_is_rejected()
    {
        var callback = Callback();
        callback["vnp_TmnCode"] = "OTHER123";
        callback["vnp_SecureHash"] = Hash(string.Join("&", callback.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => WebUtility.UrlEncode(x.Key) + "=" + WebUtility.UrlEncode(x.Value))));
        Assert.False(new VnpayProtocol(Settings()).ValidateCallback(callback));
    }

    [Fact]
    public void Query_request_signs_documented_pipe_order_with_original_creation_time()
    {
        var request = new VnpayProtocol(Settings()).BuildQueryRequest("ABC123", Created,
            Created.AddHours(2), "127.0.0.1");
        Assert.Matches("^[A-F0-9]{32}$", request["vnp_RequestId"]);
        Assert.Equal("20261115010102", request["vnp_TransactionDate"]);
        Assert.Equal(Hash(request["vnp_RequestId"]
            + "|2.1.0|querydr|TEST1234|ABC123|20261115010102|20261115030102|127.0.0.1|Truy van ABC123"),
            request["vnp_SecureHash"]);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("PROMO", "10000")]
    public void Query_response_signs_optional_promotion_fields_and_rejects_tampering(string promotion, string discount)
    {
        var response = new Dictionary<string, string>
        {
            ["vnp_ResponseId"] = "R1", ["vnp_Command"] = "querydr", ["vnp_ResponseCode"] = "00",
            ["vnp_Message"] = "Success", ["vnp_TmnCode"] = "TEST1234", ["vnp_TxnRef"] = "ABC123",
            ["vnp_Amount"] = "20000000", ["vnp_BankCode"] = "NCB", ["vnp_TransactionNo"] = "123",
            ["vnp_TransactionType"] = "01", ["vnp_TransactionStatus"] = "00", ["vnp_OrderInfo"] = "Visit fee"
        };
        if (promotion.Length > 0)
        {
            response["vnp_PromotionCode"] = promotion;
            response["vnp_PromotionAmount"] = discount;
        }
        response["vnp_SecureHash"] = Hash("R1|querydr|00|Success|TEST1234|ABC123|20000000|NCB||123|01|00|Visit fee|"
            + promotion + "|" + discount);
        var protocol = new VnpayProtocol(Settings());
        Assert.True(protocol.ValidateQueryResponse(response));
        response["vnp_TransactionStatus"] = "01";
        Assert.False(protocol.ValidateQueryResponse(response));
    }

    [Fact]
    public void References_are_random_alphanumeric_and_fit_provider_limits()
    {
        var first = VnpayProtocol.NewTransactionReference();
        Assert.Matches("^[A-F0-9]{32}$", first);
        Assert.NotEqual(first, VnpayProtocol.NewTransactionReference());
    }

    [Fact]
    public void Query_response_cannot_move_signed_delimiters_between_fields()
    {
        var response = new Dictionary<string, string>
        {
            ["vnp_TmnCode"] = "TEST1234", ["vnp_Command"] = "querydr", ["vnp_Message"] = "Success|extra"
        };
        response["vnp_SecureHash"] = Hash("|querydr||Success|extra|TEST1234||||||||||");
        Assert.False(new VnpayProtocol(Settings()).ValidateQueryResponse(response));
    }

    [Fact]
    public void Pay_url_rejects_invalid_reference_ip_expiry_and_oversize_return_url()
    {
        var protocol = new VnpayProtocol(Settings());
        Assert.Throws<ValidationException>(() => protocol.BuildPayUrl("ABC&123", 200000, Created,
            Created.AddMinutes(15), "127.0.0.1", "token"));
        Assert.Throws<ValidationException>(() => protocol.BuildPayUrl("ABC123", 200000, Created,
            Created.AddMinutes(15), "not-an-ip", "token"));
        Assert.Throws<ValidationException>(() => protocol.BuildPayUrl("ABC123", 200000, Created,
            Created, "127.0.0.1", "token"));
        Assert.Throws<ValidationException>(() => protocol.BuildPayUrl("ABC123", 200000, Created,
            Created.AddMinutes(15), "127.0.0.1", new string('a', 256)));
    }

    private static Dictionary<string, string> Callback() => new()
    {
        ["vnp_TxnRef"] = "ABC123", ["vnp_TmnCode"] = "TEST1234", ["vnp_ResponseCode"] = "00",
        ["vnp_TransactionStatus"] = "00", ["vnp_OrderInfo"] = "Phi khao sat + & /", ["vnp_Amount"] = "20000000"
    };

    private static string Hash(string data) => Convert.ToHexString(
        HMACSHA512.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
}
