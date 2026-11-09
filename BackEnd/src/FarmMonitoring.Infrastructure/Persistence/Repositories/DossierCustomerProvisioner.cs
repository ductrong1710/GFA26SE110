using System.Security.Cryptography;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

// Called only inside the reviewing administrator's locked dossier transaction.
public sealed class DossierCustomerProvisioner(AppDbContext db, IPasswordService passwords)
{
    public async Task EnsureAsync(Consultation consultation, DeviceProposal proposal, int administratorId,
        DateTimeOffset now, CancellationToken ct)
    {
        if (consultation.OwnerId is int owner && consultation.FarmId is int farm)
        {
            if (!await FarmAccessQuery.AssignedFarmIds(db, owner).ContainsAsync(farm, ct) ||
                !await db.Farms.AnyAsync(x => x.Id == farm && x.IsActive, ct))
                throw new BusinessRuleException("The linked customer and farm must remain active and authorized.");
            if (proposal.DossierCustomerEmail is not null && !await db.Users.AnyAsync(x => x.Id == owner && x.Email == proposal.DossierCustomerEmail, ct))
                throw new BusinessRuleException("The linked owner does not match the submitted customer. Return the dossier for correction.");
            return;
        }
        if (proposal.DossierCustomerEmail is not string email || proposal.DossierCustomerName is not string name)
            throw new BusinessRuleException("Link a customer and farm or return the dossier for correction.");
        // Serializes account creation for this normalized email, including concurrent dossiers.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({email}, 741))", ct);
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Email == email, ct);
        if (user is not null && (!user.IsActive || user.UserRoles.Count != 1 || user.UserRoles.Single().Role.Name != RoleNames.FarmOwner))
            throw new BusinessRuleException("This email cannot be used as an active customer account.");
        if (user is null)
        {
            user = new User { Email = email, FullName = name, CreatedAt = now };
            // No password is shared with the engineer. The owner can set one through password recovery.
            user.PasswordHash = passwords.Hash(user, Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            user.UserRoles.Add(new UserRole { RoleId = await db.Roles.Where(x => x.Name == RoleNames.FarmOwner).Select(x => x.Id).SingleAsync(ct) });
            db.Users.Add(user);
        }
        var survey = await db.FarmSurveys.SingleAsync(x => x.Id == proposal.SurveyId, ct);
        var newFarm = new Farm { Name = survey.FarmName, Description = survey.FarmLocation, CreatedAt = now };
        db.Farms.Add(newFarm);
        db.UserFarms.Add(new UserFarm { User = user, Farm = newFarm, CreatedAt = now });
        consultation.Owner = user; consultation.Farm = newFarm;
        consultation.CustomerName = name; consultation.Email = email;
        consultation.History.Add(new() { UserId = administratorId, Action = ConsultationAction.CUSTOMER_BOUND,
            Status = consultation.Status, CreatedAt = now, Notes = "Administrator approved the customer and farm from the installation dossier." });
        await db.SaveChangesAsync(ct);
    }
}
