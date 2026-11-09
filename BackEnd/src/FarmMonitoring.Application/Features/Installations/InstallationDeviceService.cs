using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Application.Common;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Installations;
public sealed record DeviceAllocationRequest(int ProposalLineId, int? SensorNodeId, int? UavId, int? GatewayId);
public sealed record JobDevicesRequest(int ExpectedRevision, string Note, DeviceAllocationRequest[] Devices);
public sealed record AllocatedDeviceResponse(int Id, int ProposalLineId, int? SensorNodeId, int? UavId, int? GatewayId, string Code, string Name);
public sealed class JobDevicesValidator : AbstractValidator<JobDevicesRequest>
{
    public JobDevicesValidator()
    {
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(1, int.MaxValue - 1);
        RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Devices).NotNull().Must(x => x is { Length: > 0 and <= 1000 }).WithMessage("Provide 1 to 1000 allocated devices.");
        RuleForEach(x => x.Devices).NotNull().ChildRules(d => {
            d.RuleFor(x => x.ProposalLineId).GreaterThan(0);
            d.RuleFor(x => x).Must(x => new[] { x.SensorNodeId, x.UavId, x.GatewayId }.Count(id => id.HasValue) == 1 && new[] { x.SensorNodeId, x.UavId, x.GatewayId }.All(id => id is null or > 0))
                .WithMessage("Select exactly one registered device.");
        });
    }
}
public sealed class InstallationDeviceService(IInstallationDeviceRepository repository, InstallationJobService jobs,
    ICurrentUser current, IValidator<JobDevicesRequest> validator, TimeProvider clock)
{
    public async Task<IReadOnlyList<AllocatedDeviceResponse>> GetAsync(int jobId, CancellationToken ct)
    {
        await jobs.GetAsync(jobId, ct);
        return await repository.GetAsync(jobId, ct);
    }
    public async Task<IReadOnlyList<AllocatedDeviceResponse>> SetAsync(int jobId, JobDevicesRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await repository.SetAsync(jobId, current.UserId ?? throw new AuthException("Authentication required."), request, clock.GetUtcNow(), ct);
        return await GetAsync(jobId, ct);
    }
}
