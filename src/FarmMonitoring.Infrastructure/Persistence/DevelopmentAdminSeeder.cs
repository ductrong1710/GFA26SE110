using System.ComponentModel.DataAnnotations;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Constants;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence;

public sealed class DevelopmentAdminSeeder(AppDbContext db, IPasswordService passwords, TimeProvider clock)
{
    public async Task SeedAsync(string? email, string? password, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(email) && string.IsNullOrEmpty(password))
            return;
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 255
            || !new EmailAddressAttribute().IsValid(email.Trim()) || string.IsNullOrWhiteSpace(password)
            || password.Length is < 12 or > 1024)
            throw new InvalidOperationException("Development admin seed requires a valid email and a password of 12 to 1024 characters.");

        // Serialize optional bootstrap across simultaneous development instances.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(731829104)", ct);
        if (await db.UserRoles.AnyAsync(x => x.Role.Name == RoleNames.FarmAdministrator, ct))
            return;
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == normalizedEmail, ct))
            throw new InvalidOperationException("The seed email already belongs to an account. Bootstrap will not change existing accounts.");

        var role = await db.Roles.SingleAsync(x => x.Name == RoleNames.FarmAdministrator, ct);
        var user = new User { Email = normalizedEmail, FullName = "Development Administrator", CreatedAt = clock.GetUtcNow() };
        user.PasswordHash = passwords.Hash(user, password);
        user.UserRoles.Add(new UserRole { RoleId = role.Id });
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
