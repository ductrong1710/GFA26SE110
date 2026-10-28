using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Users;

namespace FarmMonitoring.UnitTests;

public class UserValidationTests
{
    [Theory]
    [InlineData("FarmAdministrator", true)]
    [InlineData("FarmOwner", true)]
    [InlineData("FarmEngineer", true)]
    [InlineData("UavDeviceOperator", true)]
    [InlineData("UnknownRole", false)]
    public void Human_roles_follow_the_supported_role_contract(string role, bool accepted)
    {
        Assert.Equal(accepted && role != "FarmAdministrator", new CreateUserValidator().Validate(new CreateUserRequest("user@example.com", "User", "Test-password-123!", [role])).IsValid);
        Assert.Equal(accepted, new UserRolesValidator().Validate(new UserRolesRequest([role])).IsValid);
    }
    [Fact]
    public void Missing_fields_and_unknown_or_duplicate_roles_are_rejected()
    {
        Assert.False(new CreateUserValidator().Validate(new CreateUserRequest(null!, null!, null!, null!)).IsValid);
        Assert.False(new CreateUserValidator().Validate(new CreateUserRequest("x@example.com", " ", "short", ["unknown"])).IsValid);
        Assert.False(new UserRolesValidator().Validate(new UserRolesRequest(["FarmAdministrator", "FarmAdministrator"])).IsValid);
        Assert.False(new UserRolesValidator().Validate(new UserRolesRequest([null!])).IsValid);
        Assert.False(new UserStatusValidator().Validate(new UserStatusRequest(null)).IsValid);
    }

    [Fact]
    public void Valid_account_and_role_removal_are_accepted()
    {
        Assert.True(new CreateUserValidator().Validate(new CreateUserRequest(" X@example.com ", "User", "Test-password-123!", ["FarmOwner"])).IsValid);
        Assert.True(new UserRolesValidator().Validate(new UserRolesRequest([])).IsValid);
        Assert.True(new UserStatusValidator().Validate(new UserStatusRequest(false)).IsValid);
    }

    [Fact]
    public void Creating_user_requires_exactly_one_role()
    {
        string[]?[] invalidRoles = [null, [], ["FarmOwner", "FarmEngineer"],
            ["FarmOwner", "UavDeviceOperator"], ["FarmOwner", "FarmOwner"]];
        foreach (var roles in invalidRoles)
        {
            var result = new CreateUserValidator().Validate(
                new CreateUserRequest("single@example.com", "Single role", "Test-password-123!", roles!));
            Assert.Contains(result.Errors, error => error.PropertyName == "roles" &&
                error.ErrorMessage == "Exactly one role is required when creating a user.");
        }
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void Pagination_rejects_unbounded_or_overflowing_queries(int page, int size) =>
        Assert.False(new PageQueryValidator().Validate(new PageQuery() { Page = page, PageSize = size }).IsValid);
}

