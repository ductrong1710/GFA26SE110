using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Application.Features.Installations;

namespace FarmMonitoring.UnitTests;

public class ConsultationValidationTests
{
    [Fact]
    public void Allocation_rejects_missing_or_ambiguous_device_identity()
    {
        var device = new DeviceAllocationRequest(1, 2, null, null);
        var request = new JobDevicesRequest(4, "Prepared", [device]);
        var validator = new JobDevicesValidator();
        Assert.True(validator.Validate(request).IsValid);
        Assert.False(validator.Validate(request with { Devices = null! }).IsValid);
        Assert.False(validator.Validate(request with { Devices = [null!] }).IsValid);
        Assert.False(validator.Validate(request with { Devices = [device with { UavId = 3 }] }).IsValid);
        Assert.False(validator.Validate(request with { Devices = [device with { SensorNodeId = null }] }).IsValid);
        Assert.False(validator.Validate(request with { ExpectedRevision = 0 }).IsValid);
    }

    [Fact]
    public void Installation_report_requires_explicit_device_checks_and_issue_details_for_failures()
    {
        var device = new InstalledDeviceRequest(1, 2, null, null, "Canal edge", true, true, true, "Reading received");
        var request = new InstallationReportRequest(4, "Handover completed", null, [device]);
        var validator = new InstallationReportValidator();
        Assert.True(validator.Validate(request).IsValid);
        Assert.False(validator.Validate(request with { Devices = null! }).IsValid);
        Assert.False(validator.Validate(request with { Devices = [null!] }).IsValid);
        Assert.False(validator.Validate(request with { Devices = [device with { UavId = 3 }] }).IsValid);
        Assert.False(validator.Validate(request with { Devices = [device with { ConnectivityPassed = null }] }).IsValid);
        Assert.False(validator.Validate(request with { Devices = [device with { OperationPassed = false }] }).IsValid);
        Assert.True(validator.Validate(request with { Issues = "Operation test failed", Devices = [device with { OperationPassed = false }] }).IsValid);
        Assert.False(validator.Validate(request with { Devices = [device with { TestResults = " " }] }).IsValid);
    }

    [Fact]
    public void Installation_assignment_requires_future_appointment_technician_and_audit_note()
    {
        var request = new JobAssignmentRequest(9, DateTimeOffset.UtcNow.AddDays(2), "Agreed appointment", 1);
        var validator = new JobAssignmentValidator(TimeProvider.System);
        Assert.True(validator.Validate(request).IsValid);
        Assert.False(validator.Validate(request with { TechnicianId = 0 }).IsValid);
        Assert.False(validator.Validate(request with { ScheduledAt = DateTimeOffset.UtcNow.AddDays(-1) }).IsValid);
        Assert.False(validator.Validate(request with { Note = " " }).IsValid);
        Assert.False(validator.Validate(request with { ExpectedRevision = -1 }).IsValid);
    }

    [Fact]
    public void Proposal_requires_device_lines_positive_quantities_and_future_installation()
    {
        var line = new ProposalLineRequest(1, "Sensor", 3, "Canal edge");
        var request = new ProposalRequest(0, 1, 6, DateTimeOffset.UtcNow.AddDays(7), null, [line]);
        var validator = new ProposalValidator(TimeProvider.System);
        Assert.True(validator.Validate(request).IsValid);
        Assert.False(validator.Validate(request with { Lines = null! }).IsValid);
        Assert.False(validator.Validate(request with { Lines = [] }).IsValid);
        Assert.False(validator.Validate(request with { Lines = [null!] }).IsValid);
        Assert.False(validator.Validate(request with { Lines = [line with { Quantity = 0 }] }).IsValid);
        Assert.False(validator.Validate(request with { Lines = [line with { Quantity = 1001 }] }).IsValid);
        Assert.False(validator.Validate(request with { Lines = [line with { Quantity = 600 }, line with { Quantity = 600 }] }).IsValid);
        Assert.False(validator.Validate(request with { Lines = [line with { SurveyZoneId = 0 }] }).IsValid);
        Assert.False(validator.Validate(request with { RentalMonths = 0 }).IsValid);
        Assert.False(validator.Validate(request with { RequestedInstallationAt = DateTimeOffset.UtcNow.AddDays(-1) }).IsValid);
        Assert.False(new ProposalConfirmationValidator().Validate(new ProposalConfirmationRequest(" ")).IsValid);
    }

    [Fact]
    public void Survey_rejects_null_duplicate_and_invalid_zones_but_preserves_unknown_values()
    {
        var zone = new SurveyZoneRequest("North", null, null, null, "Canal edge");
        var request = new SurveyRequest(0, "Farm", "Location", null, "Monitoring", null, [zone]);
        var validator = new SurveyValidator();
        Assert.True(validator.Validate(request).IsValid);
        Assert.False(validator.Validate(request with { Zones = null! }).IsValid);
        Assert.False(validator.Validate(request with { Zones = [null!] }).IsValid);
        Assert.False(validator.Validate(request with { Zones = [zone, zone with { Name = " north " }] }).IsValid);
        Assert.True(validator.Validate(request with { Zones = [zone with { CropAgeDays = null }] }).IsValid);
        Assert.False(validator.Validate(request with { Zones = [zone with { CropAgeDays = -1 }] }).IsValid);
        Assert.True(validator.Validate(request with { Zones = [zone with { InstallationLocation = "" }] }).IsValid);
        Assert.False(validator.Validate(request with { ExpectedRevision = -1 }).IsValid);
    }

    [Theory]
    [InlineData("-------", false)]
    [InlineData("(      )", false)]
    [InlineData("+84 901 234 567", true)]
    [InlineData("0901234567", true)]
    public void Contact_phone_requires_digits(string phone, bool valid)
    {
        var request = new SubmitConsultationRequest("Customer", phone, null, "Farm location", null, "Monitoring", "Please contact me");
        Assert.Equal(valid, new SubmitConsultationValidator().Validate(request).IsValid);
    }
}
