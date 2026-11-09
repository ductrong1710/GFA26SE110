using FarmMonitoring.Application.Features.TechnicalSupport;

namespace FarmMonitoring.UnitTests;

public sealed class ServiceReportTests
{
    private static ServiceReportRequest Valid() => new(2, "Loose connector", "Secured connector", "None", "Connection stable", "Retested on site", true, true);

    [Fact]
    public void Reports_require_explicit_test_results_but_preserve_failures()
    {
        var validator = new ServiceReportValidator();
        Assert.True(validator.Validate(Valid()).IsValid);
        Assert.True(validator.Validate(Valid() with { ConnectivityPassed = false, OperationPassed = false }).IsValid);
        Assert.False(validator.Validate(Valid() with { ConnectivityPassed = null }).IsValid);
        Assert.False(validator.Validate(Valid() with { OperationPassed = null }).IsValid);
        Assert.False(validator.Validate(Valid() with { TestResults = " " }).IsValid);
    }

    [Fact]
    public void Reports_require_bounded_work_evidence_and_revision()
    {
        var validator = new ServiceReportValidator();
        Assert.False(validator.Validate(Valid() with { ExpectedRevision = 0 }).IsValid);
        Assert.False(validator.Validate(Valid() with { ExpectedRevision = int.MaxValue }).IsValid);
        Assert.False(validator.Validate(Valid() with { Cause = " " }).IsValid);
        Assert.False(validator.Validate(Valid() with { Work = " " }).IsValid);
        Assert.False(validator.Validate(Valid() with { Components = " " }).IsValid);
        Assert.False(validator.Validate(Valid() with { Notes = new string('x', 4001) }).IsValid);
        Assert.False(validator.Validate(Valid() with { TestResults = new string('x', 2001) }).IsValid);
    }
}
