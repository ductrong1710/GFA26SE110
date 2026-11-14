using FarmMonitoring.Application.Common;
using FluentValidation;
using FarmMonitoring.Domain.Entities;
using System.Text.Json.Serialization;

namespace FarmMonitoring.Application.Features.PublicInformation;

public sealed record PublicEntry(int Id, string Kind, string Title, string Body, decimal? EstimatedMonthlyRentVnd, bool HasImage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? ReplacementPriceVnd = null, int PriceRevision = 1);
public sealed record PublicImage(string ContentType, byte[] Content);

public sealed record ManagedEntry(int Id, string Kind, string Title, string Body, decimal? EstimatedMonthlyRentVnd,
    bool HasImage, bool Published, int Revision, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? ReplacementPriceVnd = null);
public sealed record PublicContentRequest(int ExpectedRevision, string Kind, string Title, string Body, decimal? EstimatedMonthlyRentVnd,
    EvidenceImageData? Image, bool RemoveImage, decimal? ReplacementPriceVnd = null);
public sealed record PublicationRequest(int ExpectedRevision);

public interface IPublicContentRepository
{
    Task<PagedResult<ManagedEntry>> ManagedAsync(PageQuery page, PublicContentKind? kind, CancellationToken ct);
    Task<IReadOnlyList<ManagedEntry>> ManagedContactsAsync(CancellationToken ct);
    Task<PublicContentKind?> KindAsync(int id, CancellationToken ct);
    Task<ManagedEntry> SaveAsync(int? id, PublicContentRequest request, PublicContentKind kind, EvidenceImage? image, CancellationToken ct);
    Task<ManagedEntry> PublishAsync(int id, int revision, bool publish, CancellationToken ct);
    Task<PagedResult<PublicEntry>> PublishedAsync(PageQuery page, CancellationToken ct);
    Task<PagedResult<PublicEntry>> PublishedAsync(PageQuery page, PublicContentKind kind, CancellationToken ct);
    Task<IReadOnlyList<PublicEntry>> PublishedAsync(PublicContentKind kind, CancellationToken ct);
    Task<PublicImage?> PublishedImageAsync(int id, CancellationToken ct);
}

public sealed class PublicContentService(IPublicContentRepository repository, IValidator<PageQuery> pages)
{
    public async Task<PagedResult<PublicEntry>> PublishedAsync(PageQuery page, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(page, ct);
        return await repository.PublishedAsync(page, ct);
    }
    public async Task<PagedResult<PublicEntry>> PublishedAsync(PageQuery page, PublicContentKind kind, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(page, ct);
        return await repository.PublishedAsync(page, kind, ct);
    }
    public Task<IReadOnlyList<PublicEntry>> PublishedAsync(PublicContentKind kind, CancellationToken ct) =>
        repository.PublishedAsync(kind, ct);
    public async Task<PagedResult<ManagedEntry>> ManagedAsync(PageQuery page, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(page, ct);
        return await repository.ManagedAsync(page, null, ct);
    }
    public async Task<PagedResult<ManagedEntry>> ManagedAsync(PageQuery page, PublicContentKind kind, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(page, ct);
        return await repository.ManagedAsync(page, kind, ct);
    }
    public Task<IReadOnlyList<ManagedEntry>> ManagedContactsAsync(CancellationToken ct) => repository.ManagedContactsAsync(ct);
    public Task<ManagedEntry> SaveAsync(int? id, PublicContentRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<PublicContentKind>(request.Kind, out var kind) || kind.ToString() != request.Kind || !Enum.IsDefined(kind))
            throw new ValidationException("Choose a supported public information category.");
        if (request.ReplacementPriceVnd is decimal price &&
            (kind != PublicContentKind.COMPONENT || price < 0 || price >= 10000000000000000m || decimal.Round(price,2) != price))
            throw new ValidationException("Replacement prices are only supported for COMPONENT entries and must be a nonnegative VND amount with up to two decimals.");
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
    public async Task<ManagedEntry> SaveContactAsync(int? id, PublicContentRequest request, CancellationToken ct)
    {
        if (request.Kind != nameof(PublicContentKind.CONTACT))
            throw new ValidationException("Contact endpoints only accept CONTACT entries.");
        if (id is int entryId)
        {
            var kind = await repository.KindAsync(entryId, ct) ?? throw new NotFoundException("Contact not found.");
            if (kind != PublicContentKind.CONTACT) throw new NotFoundException("Contact not found.");
        }
        return await SaveAsync(id, request, ct);
    }
    public async Task<ManagedEntry> SaveForKindAsync(int? id, PublicContentRequest request, PublicContentKind kind, CancellationToken ct)
    {
        if (kind == PublicContentKind.CONTACT || request.Kind != kind.ToString())
            throw new ValidationException("The content category does not match this endpoint.");
        if (id is int entryId)
        {
            var existingKind = await repository.KindAsync(entryId, ct) ?? throw new NotFoundException("Content not found.");
            if (existingKind != kind) throw new NotFoundException("Content not found.");
        }
        return await SaveAsync(id, request, ct);
    }
    public Task<ManagedEntry> PublishAsync(int id, PublicationRequest request, bool publish, CancellationToken ct) =>
        repository.PublishAsync(id, request.ExpectedRevision, publish, ct);

    public async Task<ManagedEntry> PublishContactAsync(int id, PublicationRequest request, bool publish, CancellationToken ct)
    {
        var kind = await repository.KindAsync(id, ct) ?? throw new NotFoundException("Contact not found.");
        if (kind != PublicContentKind.CONTACT) throw new NotFoundException("Contact not found.");
        return await repository.PublishAsync(id, request.ExpectedRevision, publish, ct);
    }

    public async Task<ManagedEntry> PublishForKindAsync(int id, PublicationRequest request, PublicContentKind kind, bool publish, CancellationToken ct)
    {
        var existingKind = await repository.KindAsync(id, ct) ?? throw new NotFoundException("Content not found.");
        if (existingKind != kind) throw new NotFoundException("Content not found.");
        return await repository.PublishAsync(id, request.ExpectedRevision, publish, ct);
    }

    public async Task<PublicImage> ImageAsync(int id, CancellationToken ct)
    {
        var kind = await repository.KindAsync(id, ct);
        if (kind != PublicContentKind.DEVICE) throw new NotFoundException("Public device image not found.");
        return await repository.PublishedImageAsync(id, ct) ?? throw new NotFoundException("Public device image not found.");
    }
}
