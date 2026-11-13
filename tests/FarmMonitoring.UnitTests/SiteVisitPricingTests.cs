using System.Globalization;
using System.Text.Json;
using FarmMonitoring.Application.Features.Consultations;
using FluentValidation;

namespace FarmMonitoring.UnitTests;

public class SiteVisitPricingTests
{
    // Test fixtures only: the approved production thresholds have not been supplied.
    private static SiteVisitPricingSettings Fixture(string? t1 = "20", string? t2 = "80") => new()
    {
        Version = "test-v1", T1Km = t1, T2Km = t2,
        Offices = new() { ["OFFICE_1"] = "Trụ sở 1", ["OFFICE_2"] = "Trụ sở 2" }
    };

    [Theory]
    [InlineData("19.99", 0)]
    [InlineData("20", 0)]
    [InlineData("20.01", 200000)]
    [InlineData("79.99", 200000)]
    [InlineData("80", 200000)]
    [InlineData("80.01", 400000)]
    [InlineData("149.99", 400000)]
    [InlineData("150", 400000)]
    [InlineData("150.01", null)]
    [InlineData("165", null)]
    public void Rates_use_original_km_and_inclusive_upper_bound(string text, int? fee)
    {
        var result = new SiteVisitPricing(Fixture()).Preview("OFFICE_1", decimal.Parse(text, CultureInfo.InvariantCulture));
        Assert.Equal(fee is null ? "MANUAL_QUOTE_REQUIRED" : "CALCULATED", result.Status);
        Assert.Equal((long?)fee, result.AmountVnd);
        Assert.NotNull(result.PricingVersion);
    }

    [Theory]
    [InlineData(null, "80")]
    [InlineData("20", null)]
    [InlineData("20", "20")]
    [InlineData("20", "150")]
    [InlineData("40", "20")]
    [InlineData("0", "80")]
    [InlineData("20.001", "80")]
    [InlineData("not-a-number", "80")]
    public void Invalid_tariff_never_quotes_zero(string? t1, string? t2)
    {
        var result = new SiteVisitPricing(Fixture(t1, t2)).Preview("OFFICE_2", 10);
        Assert.Equal("PRICING_NOT_CONFIGURED", result.Status);
        Assert.Null(result.AmountVnd);
    }

    [Fact]
    public void Absent_configuration_never_activates_automatic_or_manual_pricing()
    {
        var pricing = new SiteVisitPricing(new SiteVisitPricingSettings());
        Assert.Equal("PRICING_NOT_CONFIGURED", pricing.Preview("OFFICE_1", 20).Status);
        Assert.Equal("PRICING_NOT_CONFIGURED", pricing.Preview("OFFICE_1", 165).Status);
    }

    [Fact]
    public void Changed_configuration_invalidates_old_preview_even_with_same_version_label()
    {
        var before = new SiteVisitPricing(Fixture()).Preview("OFFICE_1", 21);
        var after = new SiteVisitPricing(Fixture("21")).Preview("OFFICE_1", 21);
        Assert.NotEqual(before.PricingVersion, after.PricingVersion);
        Assert.NotEqual(before.AmountVnd, after.AmountVnd);
    }

    [Theory]
    [InlineData("150.000000000000000000000000000000001")]
    [InlineData("2e1")]
    [InlineData("20.001")]
    public void Preview_and_submit_reject_invalid_wire_precision_before_decimal_rounding(string token)
    {
        var json = "{\"DistanceKm\":" + token + "}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SiteVisitPreviewRequest>(json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SiteVisitProposeRequest>(json));
        Assert.Equal(20.01m, JsonSerializer.Deserialize<SiteVisitProposeRequest>("{\"DistanceKm\":20.01}")!.DistanceKm);
    }

    [Theory]
    [InlineData("OFFICE_1", "0")]
    [InlineData("OFFICE_1", "-0.01")]
    [InlineData("OFFICE_1", "10000000")]
    [InlineData("OFFICE_2", "149.999")]
    [InlineData("OTHER", "2")]
    public void Invalid_input_is_rejected(string office, string text) =>
        Assert.Throws<ValidationException>(() => new SiteVisitPricing(Fixture()).Preview(office, decimal.Parse(text, CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("https://maps.app.goo.gl/Foo123")]
    [InlineData("https://maps.google.com/?q=Hanoi")]
    [InlineData("https://www.google.com.vn/maps/dir/A/B")]
    [InlineData("https://www.google.com/maps?q=garden")]
    public void Maps_accepts_reference_links(string url) => Assert.Equal(url, SiteVisitPricing.ValidateMapsUrl(url));

    [Theory]
    [InlineData("http://maps.app.goo.gl/Foo123")]
    [InlineData("https://maps.app.goo.gl.evil.org/Foo123")]
    [InlineData("https://maps.app.goo.gl@evil.org/Foo123")]
    [InlineData("https://foo@maps.app.goo.gl/Foo123")]
    [InlineData("https://maps.app.goo.gl:8080/Foo123")]
    [InlineData("https://www.google.com/url?q=https://evil.org")]
    [InlineData("https://www.google.com/mapsevil")]
    [InlineData("https://maps.google.com/url?q=https://evil.org")]
    [InlineData("https://maps.app.goo.gl/")]
    [InlineData("https://maps.app.goo.gl\\evil")]
    public void Maps_rejects_non_map_or_deceptive_links(string url) =>
        Assert.Throws<ValidationException>(() => SiteVisitPricing.ValidateMapsUrl(url));
}
