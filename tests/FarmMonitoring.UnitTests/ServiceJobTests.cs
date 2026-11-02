using FarmMonitoring.Application.Features.TechnicalSupport;

namespace FarmMonitoring.UnitTests;

public sealed class ServiceJobTests
{
    [Theory]
    [InlineData("REPAIR", 1, true)]
    [InlineData("MAINTENANCE", 1, true)]
    [InlineData("OTHER", 1, false)]
    [InlineData("REPAIR", 0, false)]
    public void Service_assignment_requires_known_type_and_technician(string type, int technician, bool valid)
        => Assert.Equal(valid, new ServiceJobAssignmentValidator(TimeProvider.System)
            .Validate(new ServiceJobAssignmentRequest(type, technician, DateTimeOffset.UtcNow.AddDays(1), "Inspect device")).IsValid);

    [Fact]
    public void Past_appointments_and_empty_reason_are_rejected()
    {
        var validator = new ServiceJobAssignmentValidator(TimeProvider.System);
        Assert.False(validator.Validate(new ServiceJobAssignmentRequest("REPAIR", 1, DateTimeOffset.UtcNow.AddDays(-1), "Inspect")).IsValid);
        Assert.False(validator.Validate(new ServiceJobAssignmentRequest("REPAIR", 1, DateTimeOffset.UtcNow.AddDays(1), " ")).IsValid);
    }
}
