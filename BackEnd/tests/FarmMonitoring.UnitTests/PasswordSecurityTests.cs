using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FarmMonitoring.UnitTests;

public class PasswordSecurityTests
{
    [Fact]
    public void Password_hashes_are_salted_and_verify_without_accepting_wrong_password()
    {
        var service = new PasswordService();
        var user = new User();
        user.PasswordHash = service.Hash(user, "a-test-password");
        Assert.NotEqual(user.PasswordHash, service.Hash(user, "a-test-password"));
        Assert.True(service.Verify(user, "a-test-password").Succeeded);
        Assert.False(service.Verify(user, "wrong").Succeeded);
        Assert.False(service.Verify(null, "a-test-password").Succeeded);
    }

    [Fact]
    public void Older_hash_requires_rehash_after_successful_verification()
    {
        var user = new User();
        user.PasswordHash = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions { IterationCount = 1000 }))
            .HashPassword(user, "a-test-password");
        var result = new PasswordService().Verify(user, "a-test-password");
        Assert.True(result.Succeeded);
        Assert.True(result.NeedsRehash);
    }
}
