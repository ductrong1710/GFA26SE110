using FarmMonitoring.Application.Features.Equipment;

namespace FarmMonitoring.UnitTests;

public class GatewayPresenceTests
{
    [Theory]
    [InlineData(true, 0, "ONLINE")]
    [InlineData(true, 90, "ONLINE")]
    [InlineData(true, 91, "OFFLINE")]
    [InlineData(true, -1, "OFFLINE")]
    [InlineData(false, 0, "OFFLINE")]
    public void Contact_expires_after_ninety_seconds(bool active, int age, string expected)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(expected, GatewayPresence.Status(active, now.AddSeconds(-age), now));
    }
    [Fact]
    public void Never_seen_gateway_is_offline() =>
        Assert.Equal("OFFLINE", GatewayPresence.Status(true, null, DateTimeOffset.UtcNow));
}
