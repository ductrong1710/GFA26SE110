using System.Security.Cryptography;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Consultations;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

// Called only inside the assigned engineer's locked dossier transaction.
public sealed class DossierCustomerProvisioner(AppDbContext db, IPasswordService passwords)
{
    public async Task EnsureAsync(Consultation consultation, int surveyId, DossierCustomerRequest? customer,
        DateTimeOffset now, CancellationToken ct)
    {
        if (consultation.OwnerId is int owner && consultation.FarmId is int farm)
        {
            if (!await FarmAccessQuery.AssignedFarmIds(db, owner).ContainsAsync(farm, ct) ||
                !await db.Farms.AnyAsync(x => x.Id == farm && x.IsActive, ct))
                throw new BusinessRuleException("The linked customer and farm must remain active and authorized.");
            return;
        }
        if (customer is null) throw new BusinessRuleException("Enter the customer name and email before sending the dossier.");
        var email = customer.Email.Trim().ToLowerInvariant();
        // Serializes account creation for this normalized email, including concurrent dossiers.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({email}, 741))", ct);
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleOrDefaultAsync(x => x.Email == email, ct);
        if (user is not null && (!user.IsActive || user.UserRoles.Count != 1 || user.UserRoles.Single().Role.Name != RoleNames.FarmOwner))
            throw new BusinessRuleException("This email cannot be used as an active customer account.");
        if (user is null)
        {
            user = new User { Email = email, FullName = customer.FullName.Trim(), CreatedAt = now };
            // No password is shared with the engineer. The owner can set one through password recovery.
            user.PasswordHash = passwords.Hash(user, Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            user.UserRoles.Add(new UserRole { RoleId = await db.Roles.Where(x => x.Name == RoleNames.FarmOwner).Select(x => x.Id).SingleAsync(ct) });
            db.Users.Add(user);
        }
        var survey = await db.FarmSurveys.SingleAsync(x => x.Id == surveyId, ct);
        var newFarm = new Farm { Name = survey.FarmName, Description = survey.FarmLocation, CreatedAt = now };
        db.Farms.Add(newFarm);
        db.UserFarms.Add(new UserFarm { User = user, Farm = newFarm, CreatedAt = now });
        consultation.Owner = user; consultation.Farm = newFarm;
        consultation.CustomerName = customer.FullName.Trim(); consultation.Email = email;
        consultation.History.Add(new() { UserId = consultation.EngineerId, Action = ConsultationAction.CUSTOMER_BOUND,
            Status = consultation.Status, CreatedAt = now, Notes = "Engineer recorded the customer and farm with the submitted installation dossier." });
        await db.SaveChangesAsync(ct);
    }
}
