using System.Globalization;
using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Consultations;

// Validate the wire precision before the JSON decimal parser can round a value onto a tier boundary.
public sealed class SiteVisitDistanceConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number || (reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length) > 10)
            throw new JsonException("distanceKm must be a decimal number with up to two fractional digits.");
        var token = reader.HasValueSequence ? reader.ValueSequence.ToArray().AsSpan() : reader.ValueSpan;
        var point = token.IndexOf((byte)'.');
        if (token.IndexOfAny((byte)'E', (byte)'e') >= 0 || (point >= 0 && token.Length - point - 1 > 2))
            throw new JsonException("distanceKm must use fixed-point notation with up to two fractional digits.");
        return reader.GetDecimal();
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
}

public sealed class SiteVisitPricingSettings
{
    public string? Version { get; set; }
    public string? T1Km { get; set; }
    public string? T2Km { get; set; }
    public Dictionary<string, string> Offices { get; set; } = [];
}

public sealed record SiteVisitOffice(string Code, string Label, string? MapsUrl = null);
public sealed record SiteVisitFeePreview(string OfficeCode, string OfficeLabel, decimal DistanceKm,
    string Status, long? AmountVnd, string? PricingVersion, string? OfficeMapsUrl = null);

public sealed record SiteVisitTariff(decimal FreeThroughKm, decimal FirstTierThroughKm, int BaseThroughKm,
    long FirstTierVnd, long BaseVnd, long ExtraVndPerKm);

public sealed class SiteVisitPricing(SiteVisitPricingSettings settings)
{
    public IReadOnlyList<SiteVisitOffice> Offices { get; } = settings.Offices
        .OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new SiteVisitOffice(x.Key, x.Value)).ToArray();

    public SiteVisitTariff? Tariff =>
        Threshold(settings.T1Km, out var t1) && Threshold(settings.T2Km, out var t2) &&
        t1 < t2 && !string.IsNullOrWhiteSpace(settings.Version)
            ? new(t1, t2, 150, 200_000, 400_000, 3_000)
            : null;

    public SiteVisitFeePreview Preview(string? officeCode, decimal distanceKm, IReadOnlyList<SiteVisitOffice>? configuredOffices = null)
    {
        var offices = configuredOffices ?? Offices;
        if (distanceKm <= 0 || distanceKm > 9999999.99m || decimal.Round(distanceKm, 2) != distanceKm)
            throw new ValidationException("distanceKm must be positive, at most 9999999.99, with at most two decimal places.");
        bool validOffices = offices.Count > 0 && offices.All(x =>
            !string.IsNullOrWhiteSpace(x.Code) && x.Code.Length <= 32 && !string.IsNullOrWhiteSpace(x.Label) && x.Label.Length <= 120);
        if (string.IsNullOrWhiteSpace(officeCode) || officeCode.Length > 32) throw new ValidationException("Invalid officeCode.");
        if (!validOffices) return new(officeCode, "", distanceKm, "PRICING_NOT_CONFIGURED", null, null);
        var office = offices.SingleOrDefault(x => x.Code == officeCode);
        if (office is null) throw new ValidationException("Unknown site visit officeCode.");
        var tariff = Tariff;
        if (tariff is null)
            return new(office.Code, office.Label, distanceKm, "PRICING_NOT_CONFIGURED", null, null);

        var version = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            settings.Version, Tariff = tariff, Office = office
        })));
        var amount = distanceKm <= tariff.FreeThroughKm ? 0
            : distanceKm <= tariff.FirstTierThroughKm ? tariff.FirstTierVnd
            : distanceKm <= tariff.BaseThroughKm ? tariff.BaseVnd
            : tariff.BaseVnd + (long)((distanceKm - tariff.BaseThroughKm) * tariff.ExtraVndPerKm);
        return new(office.Code, office.Label, distanceKm, "CALCULATED", amount, version, office.MapsUrl);
    }

    private static bool Threshold(string? raw, out decimal value) =>
        decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
        && value is > 0 and < 150 && decimal.Round(value, 2) == value;

    public static string ValidateMapsUrl(string? mapsUrl)
    {
        if (mapsUrl is not { Length: > 0 and <= 2048 } || mapsUrl.Any(char.IsWhiteSpace) || mapsUrl.Any(char.IsControl)
            || mapsUrl.Contains('\\') || !Uri.TryCreate(mapsUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length != 0)
            throw new ValidationException("mapsUrl must be a valid HTTPS Google Maps link.");
        var path = uri.AbsolutePath;
        var isMapPath = path == "/maps" || path.StartsWith("/maps/", StringComparison.Ordinal);
        bool allowed = uri.IdnHost switch
        {
            "maps.app.goo.gl" => path.Trim('/') is { Length: > 0 } slug && slug.All(char.IsAsciiLetterOrDigit),
            "maps.google.com" or "maps.google.com.vn" => isMapPath || path == "/",
            "google.com" or "www.google.com" or "google.com.vn" or "www.google.com.vn" => isMapPath,
            _ => false
        };
        if (!allowed) throw new ValidationException("mapsUrl must be an allowed Google Maps reference.");
        return mapsUrl;
    }
}
