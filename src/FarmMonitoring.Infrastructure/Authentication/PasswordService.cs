using System.Security.Cryptography;
using FarmMonitoring.Application.Features.Auth;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace FarmMonitoring.Infrastructure.Authentication;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> hasher = new();
    private readonly User dummy = new();
    private readonly string dummyHash;

    public PasswordService() => dummyHash = hasher.HashPassword(dummy, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public string Hash(User user, string password) => hasher.HashPassword(user, password);

    public PasswordCheck Verify(User? user, string password)
    {
        // Unknown users also perform a password hash verification.
        var result = hasher.VerifyHashedPassword(user ?? dummy, user?.PasswordHash ?? dummyHash, password);
        return new PasswordCheck(user is not null && result != PasswordVerificationResult.Failed,
            result == PasswordVerificationResult.SuccessRehashNeeded);
    }
}
