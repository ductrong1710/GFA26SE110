using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.TechnicalSupport;
public sealed record ServiceReplacementRequest(int ExpectedRevision, int? SensorNodeId, int? UavId, int? GatewayId, bool OldDeviceRetrieved, string Note);
public sealed record ReplacementOption
{
    public int? SensorNodeId { get; init; }
    public int? UavId { get; init; }
    public int? GatewayId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
}
public sealed record ServiceReplacementResponse(int Id, int OldAssignmentId, int NewAssignmentId, string OldDeviceCode, string NewDeviceCode, int TechnicianId, DateTimeOffset CreatedAt, string Note)
{
    public static ServiceReplacementResponse From(ServiceDeviceReplacement x) => new(x.Id, x.OldAssignmentId, x.NewAssignmentId, x.OldDeviceCode, x.NewDeviceCode, x.TechnicianId, x.CreatedAt, x.Note);
}
public sealed class ServiceReplacementValidator : AbstractValidator<ServiceReplacementRequest>
{
    public ServiceReplacementValidator()
    {
        RuleFor(x => x.ExpectedRevision).InclusiveBetween(1, int.MaxValue - 1);
        RuleFor(x => x).Must(x => new[] { x.SensorNodeId, x.UavId, x.GatewayId }.Count(id => id.HasValue) == 1 && new[] { x.SensorNodeId, x.UavId, x.GatewayId }.All(id => id is null or > 0)).WithMessage("Select exactly one registered replacement device.");
        RuleFor(x => x.OldDeviceRetrieved).Equal(true).WithMessage("Confirm retrieval of the old device.");
        RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
    }
}
public sealed class ServiceReplacementService(IServiceReplacementRepository repository, ServiceJobService jobs, ICurrentUser current,
    IValidator<ServiceReplacementRequest> validator, IValidator<PageQuery> pages, TimeProvider clock)
{
    private int Actor => current.UserId ?? throw new AuthException("Authentication required.");
    public async Task<PagedResult<ReplacementOption>> OptionsAsync(int jobId, PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        return await repository.OptionsAsync(jobId, Actor, query, ct);
    }
    public async Task<ServiceJobDetail> ReplaceAsync(int jobId, ServiceReplacementRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await repository.ReplaceAsync(jobId, Actor, request, clock.GetUtcNow(), ct);
        return await jobs.GetAsync(jobId, ct);
    }
}
