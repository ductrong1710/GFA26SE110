using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.PublicInformation;
using Microsoft.EntityFrameworkCore;
using FarmMonitoring.Domain.Entities;
using System.Linq.Expressions;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class PublicContentRepository(AppDbContext db) : IPublicContentRepository
{
    private static readonly Expression<Func<PublicContent, ManagedEntry>> Summary = x =>
        new ManagedEntry(x.Id, x.Kind.ToString(), x.Title, x.Body, x.EstimatedMonthlyRentVnd, x.ImageType != null, x.Published, x.Revision);
    public async Task<PagedResult<ManagedEntry>> ManagedAsync(PageQuery page, CancellationToken ct)
    {
        var rows = db.PublicContent.AsNoTracking();
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.Id).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize)
            .Select(Summary).ToArrayAsync(ct);
        return new(items, count, page.Page, page.PageSize);
    }

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
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Reload the current content revision."); }
        return new(row.Id, row.Kind.ToString(), row.Title, row.Body, row.EstimatedMonthlyRentVnd, row.ImageType != null, row.Published, row.Revision);
    }
    public async Task<PagedResult<PublicEntry>> PublishedAsync(PageQuery page, CancellationToken ct)
    {
        var published = db.PublicContent.AsNoTracking().Where(x => x.Published);
        var total = await published.CountAsync(ct);
        var entries = await published.OrderBy(x => x.Id).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize)
            .Select(x => new PublicEntry(x.Id, x.Kind.ToString(), x.Title, x.Body, x.EstimatedMonthlyRentVnd, x.ImageType != null))
            .ToArrayAsync(ct);
        return new(entries, total, page.Page, page.PageSize);
    }
    public Task<PublicImage?> PublishedImageAsync(int id, CancellationToken ct) =>
        db.PublicContent.AsNoTracking().Where(x => x.Id == id && x.Published && x.ImageType != null)
            .Select(x => new PublicImage(x.ImageType!, x.Image!)).SingleOrDefaultAsync(ct);
}
