using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FluentValidation;

namespace FarmMonitoring.Application.Features.TechnicalSupport;

public sealed record CreateDeviceIncidentRequest(int RentalDeviceAssignmentId, string Description, int? MissionId = null, EvidenceImageData[]? Images = null);
public sealed record DeviceIncidentSummary(int Id, int FarmId, int RentalDeviceAssignmentId, string DeviceCode, string Description, DateTimeOffset ReportedAt, string Status, int? MissionId);
public sealed record DeviceIncidentDetail(int Id, int FarmId, int RentalDeviceAssignmentId, string DeviceCode, string Description, DateTimeOffset ReportedAt, string Status, int? MissionId, int ReporterId, EvidenceImageData[] Images, int? ServiceJobId);
public sealed class CreateDeviceIncidentValidator : AbstractValidator<CreateDeviceIncidentRequest>
{
    public CreateDeviceIncidentValidator()
    {
        RuleFor(x => x.RentalDeviceAssignmentId).GreaterThan(0);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.MissionId).GreaterThan(0).When(x => x.MissionId.HasValue);
    }
}
public sealed class DeviceIncidentService(IDeviceIncidentRepository repository, ICurrentUser current, TimeProvider clock,
    IValidator<PageQuery> pages, IValidator<CreateDeviceIncidentRequest> creates)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    public async Task<PagedResult<DeviceIncidentSummary>> ListAsync(PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.ListAsync(Actor, query, ct);
    }
    public Task<DeviceIncidentDetail> GetAsync(int id, CancellationToken ct) => repository.GetAsync(id, Actor, ct);
    public async Task<DeviceIncidentDetail> CreateAsync(CreateDeviceIncidentRequest request, CancellationToken ct)
    {
        await creates.ValidateAndThrowAsync(request, ct);
        var images = EvidenceImages.Decode(request.Images);
        var id = await repository.CreateAsync(Actor, request, images, clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }
}
