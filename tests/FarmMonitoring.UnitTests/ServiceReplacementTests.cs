using FarmMonitoring.Application.Features.TechnicalSupport;

namespace FarmMonitoring.UnitTests;

public sealed class ServiceReplacementTests
{
    [Fact]
    public void Replacement_requires_one_positive_device_retrieval_and_bounded_note()
    {
        var validator = new ServiceReplacementValidator();
        var request = new ServiceReplacementRequest(2, null, 3, null, true, "Retrieved failed UAV");
        Assert.True(validator.Validate(request).IsValid);
        Assert.False(validator.Validate(request with { UavId = null }).IsValid);
        Assert.False(validator.Validate(request with { GatewayId = 4 }).IsValid);
        Assert.False(validator.Validate(request with { UavId = 0 }).IsValid);
        Assert.False(validator.Validate(request with { OldDeviceRetrieved = false }).IsValid);
        Assert.False(validator.Validate(request with { ExpectedRevision = 0 }).IsValid);
        Assert.False(validator.Validate(request with { ExpectedRevision = int.MaxValue }).IsValid);
        Assert.False(validator.Validate(request with { Note = " " }).IsValid);
        Assert.False(validator.Validate(request with { Note = new string('x', 2001) }).IsValid);
    }
}
