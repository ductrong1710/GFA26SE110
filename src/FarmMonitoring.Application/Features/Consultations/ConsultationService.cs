using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Consultations;

public sealed class ConsultationService(IConsultationRepository repository, ICurrentUser current,
    IAuthRepository users, TimeProvider clock, IValidator<SubmitConsultationRequest> submitValidator,
    IValidator<FollowUpRequest> followUpValidator, IValidator<PageQuery> pageValidator, IValidator<SurveyRequest> surveyValidator,
    IFarmAccessRepository farms)
{
    public async Task<ConsultationReceipt> SubmitAsync(SubmitConsultationRequest request, CancellationToken ct)
    {
        await submitValidator.ValidateAndThrowAsync(request, ct);
        var consultation = new Consultation
        {
            CustomerName = request.CustomerName.Trim(), Phone = request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            FarmLocation = request.FarmLocation.Trim(), FarmArea = request.FarmArea,
            ServiceNeed = request.ServiceNeed.Trim(), Content = request.Content.Trim(),
            Status = ConsultationStatus.NEW, CreatedAt = clock.GetUtcNow()
        };
        consultation.History.Add(new() { Action = ConsultationAction.SUBMITTED, Status = consultation.Status, CreatedAt = consultation.CreatedAt });
        await repository.CreateAsync(consultation, ct);
        return new(consultation.Id, consultation.Status.ToString());
    }

    public async Task<PagedResult<ConsultationSummary>> ListAsync(ConsultationQuery query, CancellationToken ct)
    {
        var actor = await RequireReaderAsync(ct);
        await pageValidator.ValidateAndThrowAsync(query, ct);
        return await repository.ListAsync(actor.Id, actor.Admin, query, ct);
    }

    public async Task<ConsultationResponse> GetAsync(int id, CancellationToken ct)
    {
        var actor = await RequireReaderAsync(ct);
        var row = await repository.GetAsync(id, ct) ?? throw new NotFoundException("Consultation not found.");
        if (!actor.Admin && row.EngineerId != actor.Id) throw new AccessDeniedException("Only the assigned engineer or administrator can access this consultation.");
        return new(row.Id, row.CustomerName, row.Phone, row.Email, row.FarmLocation, row.FarmArea,
            row.ServiceNeed, row.Content, row.Status.ToString(), row.EngineerId, row.CreatedAt,
            row.History.OrderBy(x => x.Id).Select(x => new ConsultationHistoryResponse(x.Id, x.UserId,
                x.Action.ToString(), x.Status.ToString(), x.Notes, x.ContactResult, x.NextAction, x.CreatedAt)).ToArray(), row.OwnerId, row.FarmId);
    }

    public async Task<ConsultationResponse> ClaimAsync(int id, CancellationToken ct)
    {
        await repository.ClaimAsync(id, await RequireEngineerAsync(ct), clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }

    public async Task<ConsultationResponse> FollowUpAsync(int id, FollowUpRequest request, CancellationToken ct)
    {
        var engineer = await RequireEngineerAsync(ct);
        await followUpValidator.ValidateAndThrowAsync(request, ct);
        await repository.FollowUpAsync(id, engineer, request, clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }

    private async Task<int> RequireEngineerAsync(CancellationToken ct)
    {
        var id = current.UserId ?? throw new AuthException("Authentication required.");
        var user = await users.GetUserByIdAsync(id, ct);
        if (user is not { IsActive: true } || !user.UserRoles.Any(x => x.Role.Name == RoleNames.FarmEngineer))
            throw new AccessDeniedException("Agricultural Engineer access required.");
        return id;
    }

    public async Task<SurveyResponse> RecordSurveyAsync(int id, SurveyRequest request, CancellationToken ct)
    {
        var engineer = await RequireEngineerAsync(ct);
        await surveyValidator.ValidateAndThrowAsync(request, ct);
        var survey = new FarmSurvey
        {
            ConsultationId = id, EngineerId = engineer, FarmName = request.FarmName.Trim(), FarmLocation = request.FarmLocation.Trim(),
            FarmArea = request.FarmArea, MonitoringNeeds = request.MonitoringNeeds.Trim(), Notes = Optional(request.Notes), CreatedAt = clock.GetUtcNow(),
            Images = EvidenceImages.Decode(request.Images),
            Zones = request.Zones.Select(x => new SurveyZone { Name = x.Name.Trim(), Crop = Optional(x.Crop), CropAgeDays = x.CropAgeDays,
                Symptoms = Optional(x.Symptoms), InstallationLocation = x.InstallationLocation?.Trim() ?? "" }).ToList()
        };
        await repository.AddSurveyAsync(survey, request.ExpectedRevision, ct);
        return ToSurveyResponse(survey);
    }

    public async Task<PagedResult<SurveySummary>> ListSurveysAsync(int id, PageQuery query, CancellationToken ct)
    {
        await EnsureRecordReadAsync(id, ct);
        await pageValidator.ValidateAndThrowAsync(query, ct);
        return await repository.ListSurveysAsync(id, query, ct);
    }

    public async Task<SurveyResponse> GetSurveyAsync(int id, int surveyId, CancellationToken ct)
    {
        await EnsureRecordReadAsync(id, ct);
        return ToSurveyResponse(await repository.GetSurveyAsync(id, surveyId, ct) ?? throw new NotFoundException("Survey not found."));
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<PagedResult<ConsultationSummary>> FarmConsultationsAsync(int farmId, PageQuery query, CancellationToken ct)
    {
        var id = current.UserId ?? throw new AuthException("Authentication required.");
        var user = await users.GetUserByIdAsync(id, ct);
        if (user is not { IsActive: true }) throw new AccessDeniedException("Access denied.");
        var admin = user.UserRoles.Any(x => x.Role.Name == RoleNames.FarmAdministrator);
        if (!admin && !await farms.IsUserAssignedToFarmAsync(id, farmId, ct)) throw new AccessDeniedException("Access to this farm is denied.");
        await pageValidator.ValidateAndThrowAsync(query, ct);
        return await repository.FarmConsultationsAsync(farmId, id, admin, query, ct);
    }

    public async Task<ConsultationResponse> BindCustomerAsync(int id, CustomerFarmRequest request, CancellationToken ct)
    {
        var actor = await RequireReaderAsync(ct);
        if (!actor.Admin) throw new AccessDeniedException("Administrator access required.");
        if (request.OwnerId <= 0 || request.FarmId <= 0) throw new ValidationException("Positive ownerId and farmId are required.");
        await repository.BindCustomerAsync(id, actor.Id, request, clock.GetUtcNow(), ct);
        return await GetAsync(id, ct);
    }

    private async Task<(int Id, bool Admin)> RequireReaderAsync(CancellationToken ct)
    {
        var id = current.UserId ?? throw new AuthException("Authentication required.");
        var user = await users.GetUserByIdAsync(id, ct);
        if (user is not { IsActive: true }) throw new AccessDeniedException("Access denied.");
        var admin = user.UserRoles.Any(x => x.Role.Name == RoleNames.FarmAdministrator);
        if (!admin && !user.UserRoles.Any(x => x.Role.Name == RoleNames.FarmEngineer)) throw new AccessDeniedException("Access denied.");
        return (id, admin);
    }

    public async Task EnsureRecordReadAsync(int consultationId, CancellationToken ct)
    {
        var id = current.UserId ?? throw new AuthException("Authentication required.");
        var user = await users.GetUserByIdAsync(id, ct);
        if (user is not { IsActive: true }) throw new AccessDeniedException("Access denied.");
        var row = await repository.GetAsync(consultationId, ct) ?? throw new NotFoundException("Consultation not found.");
        var roles = user.UserRoles.Select(x => x.Role.Name).ToArray();
        if (roles.Contains(RoleNames.FarmAdministrator) || roles.Contains(RoleNames.FarmEngineer) && row.EngineerId == id) return;
        if (roles.Contains(RoleNames.FarmOwner) && row.OwnerId == id && row.FarmId is int farmId && await farms.IsUserAssignedToFarmAsync(id, farmId, ct)) return;
        throw new AccessDeniedException("Access to this survey is denied.");
    }
    private static SurveyResponse ToSurveyResponse(FarmSurvey x) => new(x.Id, x.ConsultationId, x.Revision, x.EngineerId,
        x.FarmName, x.FarmLocation, x.FarmArea, x.MonitoringNeeds, x.Notes, x.CreatedAt,
        x.Zones.OrderBy(z => z.Id).Select(z => new SurveyZoneResponse(z.Id, z.Name, z.Crop, z.CropAgeDays, z.Symptoms, z.InstallationLocation)).ToArray(), EvidenceImages.Encode(x.Images));
}
