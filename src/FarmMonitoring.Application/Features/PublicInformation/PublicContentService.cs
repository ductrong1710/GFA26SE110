using FarmMonitoring.Application.Common;
using FluentValidation;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Features.PublicInformation;

public sealed record PublicEntry(int Id, string Kind, string Title, string Body, decimal? EstimatedMonthlyRentVnd, bool HasImage, decimal? ReplacementPriceVnd = null, int PriceRevision = 1);
public sealed record PublicImage(string ContentType, byte[] Content);

public sealed record ManagedEntry(int Id, string Kind, string Title, string Body, decimal? EstimatedMonthlyRentVnd,
    bool HasImage, bool Published, int Revision, decimal? ReplacementPriceVnd = null);
public sealed record PublicContentRequest(int ExpectedRevision, string Kind, string Title, string Body, decimal? EstimatedMonthlyRentVnd,
    EvidenceImageData? Image, bool RemoveImage, decimal? ReplacementPriceVnd = null);
public sealed record PublicationRequest(int ExpectedRevision);

public interface IPublicContentRepository
{
    Task<PagedResult<ManagedEntry>> ManagedAsync(PageQuery page, CancellationToken ct);
    Task<ManagedEntry> SaveAsync(int? id, PublicContentRequest request, PublicContentKind kind, EvidenceImage? image, CancellationToken ct);
    Task<ManagedEntry> PublishAsync(int id, int revision, bool publish, CancellationToken ct);
    Task<PagedResult<PublicEntry>> PublishedAsync(PageQuery page, CancellationToken ct);
    Task<PublicImage?> PublishedImageAsync(int id, CancellationToken ct);
}

public sealed class PublicContentService(IPublicContentRepository repository, IValidator<PageQuery> pages)
{
    public async Task<PagedResult<PublicEntry>> PublishedAsync(PageQuery page, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(page, ct);
        return await repository.PublishedAsync(page, ct);
    }
    public async Task<PagedResult<ManagedEntry>> ManagedAsync(PageQuery page, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(page, ct);
        return await repository.ManagedAsync(page, ct);
    }
    public Task<ManagedEntry> SaveAsync(int? id, PublicContentRequest request, CancellationToken ct)
    {
        if (request.ReplacementPriceVnd is decimal price &&
            (request.Kind is not ("DEVICE" or "COMPONENT") || price < 0 || price >= 10000000000000000m || decimal.Round(price,2) != price))
            throw new ValidationException("Replacement prices require DEVICE/COMPONENT and a nonnegative VND amount with up to two decimals.");
        if (!Enum.TryParse<PublicContentKind>(request.Kind, out var kind) || kind.ToString() != request.Kind || !Enum.IsDefined(kind))
            throw new ValidationException("Choose a supported public information category.");
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200 ||
            string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 4000)
            throw new ValidationException("Enter a title (up to 200 characters) and content (up to 4000 characters).");
        if (request.EstimatedMonthlyRentVnd is decimal rent && (kind != PublicContentKind.DEVICE || rent < 0 ||
            rent >= 10000000000000000m || decimal.Round(rent, 2) != rent))
            throw new ValidationException("Only devices can have an estimated monthly rent, with up to two decimal places.");
        if (id == null && request.ExpectedRevision != 0 || id != null && request.ExpectedRevision < 1)
            throw new ValidationException("Supply the current revision.");
        if (request.RemoveImage && request.Image != null) throw new ValidationException("Choose either image replacement or removal.");
        var image = request.Image == null ? null : EvidenceImages.Decode([request.Image]).Single();
        return repository.SaveAsync(id, request, kind, image, ct);
    }
    public Task<ManagedEntry> PublishAsync(int id, PublicationRequest request, bool publish, CancellationToken ct) =>
        repository.PublishAsync(id, request.ExpectedRevision, publish, ct);

    public async Task<PublicImage> ImageAsync(int id, CancellationToken ct) =>
        await repository.PublishedImageAsync(id, ct) ?? throw new NotFoundException("Public image not found.");
}
