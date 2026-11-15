using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed partial class ConsultationRepository
{
    public async Task<IReadOnlyList<SiteVisitOffice>> ListOfficesAsync(CancellationToken ct)
    {
        var offices = await db.Set<SiteVisitOfficeLocation>().AsNoTracking()
            .OrderBy(office => office.Name).ThenBy(office => office.Id).ToArrayAsync(ct);
        return offices.Select(OfficeView).ToArray();
    }

    public async Task<SiteVisitOffice> AddOfficeAsync(int adminId, string name, string mapsUrl, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var admin = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = {adminId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (admin is not { IsActive: true } || !await db.UserRoles.AnyAsync(role => role.UserId == adminId && role.Role.Name == RoleNames.FarmAdministrator, ct))
            throw AccessDeniedException.OutsideScope();

        var office = new SiteVisitOfficeLocation { Name = name, MapsUrl = mapsUrl };
        db.Add(office);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return OfficeView(office);
    }

    private static SiteVisitOffice OfficeView(SiteVisitOfficeLocation office) =>
        new($"SITE_OFFICE_{office.Id}", office.Name, office.MapsUrl);
}
