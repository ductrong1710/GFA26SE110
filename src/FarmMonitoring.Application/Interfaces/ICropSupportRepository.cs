using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.CropSupport;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;
public interface ICropSupportRepository
{
    Task<PagedResult<SupportingCropSeason>> SeasonsAsync(int id, int actorId, PageQuery query, CancellationToken ct);
    Task<PagedResult<CropSensorReading>> ReadingsAsync(int id, int actorId, PageQuery query, CancellationToken ct);
    Task ApplyAsync(int id, int actorId, CropConsultationAction action, CropConsultationRequest? request, List<EvidenceImage> images, DateTimeOffset now, CancellationToken ct);
    Task<PagedResult<CropConditionSummary>> ListAsync(int actorId, PageQuery query, CancellationToken ct);
    Task<CropConditionDetail> GetAsync(int id, int actorId, CancellationToken ct);
    Task<int> CreateAsync(int actorId, CreateCropConditionRequest request, List<EvidenceImage> images, DateTimeOffset now, CancellationToken ct);
}
