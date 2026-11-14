using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.PublicInformation;
using Microsoft.EntityFrameworkCore;
using FarmMonitoring.Domain.Entities;
using System.Linq.Expressions;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class PublicContentRepository(AppDbContext db, TimeProvider clock) : IPublicContentRepository
{
    public PublicContentRepository(AppDbContext db) : this(db,TimeProvider.System) { }
    private static readonly Expression<Func<PublicContent, ManagedEntry>> Summary = x =>
        new ManagedEntry(x.Id, x.Kind.ToString(), x.Title, x.Body, x.EstimatedMonthlyRentVnd, x.ImageType != null, x.Published, x.Revision,
            x.Kind == PublicContentKind.COMPONENT ? x.ReplacementPriceVnd : null);
    public async Task<PagedResult<ManagedEntry>> ManagedAsync(PageQuery page, PublicContentKind? kind, CancellationToken ct)
    {
        var rows = db.PublicContent.AsNoTracking();
        rows = kind.HasValue
            ? rows.Where(x => x.Kind == kind.Value)
            : rows.Where(x => x.Kind != PublicContentKind.CONTACT);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.Id).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize)
            .Select(Summary).ToArrayAsync(ct);
        return new(items, count, page.Page, page.PageSize);
    }

    public async Task<IReadOnlyList<ManagedEntry>> ManagedContactsAsync(CancellationToken ct) =>
        await db.PublicContent.AsNoTracking().Where(x => x.Kind == PublicContentKind.CONTACT)
            .OrderByDescending(x => x.Id).Select(Summary).ToArrayAsync(ct);

    public Task<PublicContentKind?> KindAsync(int id, CancellationToken ct) =>
        db.PublicContent.AsNoTracking().Where(x => x.Id == id).Select(x => (PublicContentKind?)x.Kind).SingleOrDefaultAsync(ct);

    private async Task<PublicContent> CurrentAsync(int id, int revision, CancellationToken ct)
    {
        var row = await db.PublicContent.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Content not found.");
        if (row.Revision != revision || revision == int.MaxValue) throw new ConflictException("Reload the current content revision.");
        row.Revision++;
        return row;
    }
    public async Task<ManagedEntry> SaveAsync(int? id, PublicContentRequest request, PublicContentKind kind, EvidenceImage? image, CancellationToken ct)
    {
        var row = id.HasValue ? await CurrentAsync(id.Value, request.ExpectedRevision, ct) : new PublicContent();
        if (!id.HasValue) db.PublicContent.Add(row);
        row.Kind = kind;
        row.Title = request.Title.Trim();
        row.Body = request.Body.Trim();
        row.EstimatedMonthlyRentVnd = request.EstimatedMonthlyRentVnd;
        row.ReplacementPriceVnd = kind == PublicContentKind.COMPONENT ? request.ReplacementPriceVnd : null;
        if (image != null || request.RemoveImage)
        {
            row.Image = image?.Content;
            row.ImageType = image?.ContentType;
        }
        return await CommitAsync(row, ct);
    }
    public async Task<ManagedEntry> PublishAsync(int id, int revision, bool publish, CancellationToken ct)
    {
        var row = await CurrentAsync(id, revision, ct);
        if (row.Published == publish) throw new ConflictException("Content already has the requested publication status.");
        row.Published = publish;
        return await CommitAsync(row, ct);
    }
    private async Task<ManagedEntry> CommitAsync(PublicContent row, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try {
            await db.SaveChangesAsync(ct);
            db.Set<PublicPriceVersion>().Add(new() { PublicContentId=row.Id,Revision=row.Revision,Title=row.Title,MonthlyRentVnd=row.EstimatedMonthlyRentVnd,
                ReplacementPriceVnd=row.ReplacementPriceVnd,Published=row.Published,RecordedAt=clock.GetUtcNow() });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Reload the current content revision."); }
        return new(row.Id, row.Kind.ToString(), row.Title, row.Body, row.EstimatedMonthlyRentVnd, row.ImageType != null, row.Published, row.Revision,
            row.Kind == PublicContentKind.COMPONENT ? row.ReplacementPriceVnd : null);
    }
    public async Task<PagedResult<PublicEntry>> PublishedAsync(PageQuery page, CancellationToken ct)
    {
        return await PublishedAsync(page, null, ct);
    }

    public async Task<PagedResult<PublicEntry>> PublishedAsync(PageQuery page, PublicContentKind kind, CancellationToken ct)
    {
        return await PublishedAsync(page, (PublicContentKind?)kind, ct);
    }

    public async Task<IReadOnlyList<PublicEntry>> PublishedAsync(PublicContentKind kind, CancellationToken ct) =>
        await db.PublicContent.AsNoTracking().Where(x => x.Published && x.Kind == kind).OrderBy(x => x.Id)
            .Select(x => new PublicEntry(x.Id, x.Kind.ToString(), x.Title, x.Body, x.EstimatedMonthlyRentVnd,
                x.ImageType != null, x.Kind == PublicContentKind.COMPONENT ? x.ReplacementPriceVnd : null, x.Revision)).ToArrayAsync(ct);

    private async Task<PagedResult<PublicEntry>> PublishedAsync(PageQuery page, PublicContentKind? kind, CancellationToken ct)
    {
        var published = db.PublicContent.AsNoTracking().Where(x => x.Published);
        published = kind.HasValue
            ? published.Where(x => x.Kind == kind.Value)
            : published.Where(x => x.Kind != PublicContentKind.CONTACT);
        var total = await published.CountAsync(ct);
        var entries = await published.OrderBy(x => x.Id).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize)
            .Select(x => new PublicEntry(x.Id, x.Kind.ToString(), x.Title, x.Body, x.EstimatedMonthlyRentVnd, x.ImageType != null,
                x.Kind == PublicContentKind.COMPONENT ? x.ReplacementPriceVnd : null, x.Revision))
            .ToArrayAsync(ct);
        return new(entries, total, page.Page, page.PageSize);
    }
    public Task<PublicImage?> PublishedImageAsync(int id, CancellationToken ct) =>
        db.PublicContent.AsNoTracking().Where(x => x.Id == id && x.Published && x.ImageType != null)
            .Select(x => new PublicImage(x.ImageType!, x.Image!)).SingleOrDefaultAsync(ct);
}
