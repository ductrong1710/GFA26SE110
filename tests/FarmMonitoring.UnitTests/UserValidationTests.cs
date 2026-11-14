using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Users;

namespace FarmMonitoring.UnitTests;

public class UserValidationTests
{
    [Theory]
    [InlineData("FarmAdministrator", false)]
    [InlineData("FarmOwner", true)]
    [InlineData("FarmEngineer", true)]
    [InlineData("Technician", true)]
    [InlineData("UavDeviceOperator", false)]
    [InlineData("UnknownRole", false)]
    public void Human_roles_follow_the_supported_role_contract(string role, bool accepted)
    {
        Assert.Equal(accepted && role != "FarmAdministrator", new CreateUserValidator().Validate(new CreateUserRequest("user@example.com", "User", "Test-password-123!", [role])).IsValid);
        Assert.Equal(accepted && role != "FarmAdministrator", new UserRolesValidator().Validate(new UserRolesRequest([role])).IsValid);
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
    public void Valid_account_and_single_role_are_accepted()
    {
        Assert.True(new CreateUserValidator().Validate(new CreateUserRequest(" X@example.com ", "User", "Test-password-123!", ["FarmOwner"])).IsValid);
        Assert.False(new UserRolesValidator().Validate(new UserRolesRequest([])).IsValid);
        Assert.True(new UserStatusValidator().Validate(new UserStatusRequest(false)).IsValid);
    }

    [Fact]
    public void User_profile_update_accepts_one_supported_non_admin_role_or_omits_roles()
    {
        var validator = new UpdateUserValidator();
        Assert.True(validator.Validate(new UpdateUserRequest("user@example.com", "User")).IsValid);
        Assert.True(validator.Validate(new UpdateUserRequest("user@example.com", "User", ["FarmEngineer"])).IsValid);
        Assert.False(validator.Validate(new UpdateUserRequest("user@example.com", "User", ["FarmAdministrator"])).IsValid);
        Assert.False(validator.Validate(new UpdateUserRequest("user@example.com", "User", ["FarmOwner", "FarmEngineer"])).IsValid);
        Assert.False(validator.Validate(new UpdateUserRequest("user@example.com", "User", ["UnknownRole"])).IsValid);
    }

    [Theory]
    [InlineData("12345", false)]
    [InlineData("123456", true)]
    public void Creating_user_requires_a_password_of_at_least_six_characters(string password, bool expected) =>
        Assert.Equal(expected, new CreateUserValidator().Validate(
            new CreateUserRequest("user@example.com", "User", password, ["FarmOwner"])).IsValid);

    [Fact]
    public void Creating_user_requires_exactly_one_role()
    {
        var validator = new CreateUserValidator();
        foreach (var roles in new string[]?[] { null, [], ["FarmOwner", "FarmOwner"], ["UavDeviceOperator"] })
            Assert.False(validator.Validate(new CreateUserRequest("user@example.com", "User", "Test-password-123!", roles!)).IsValid);
        Assert.False(validator.Validate(new CreateUserRequest("user@example.com", "User", "Test-password-123!", ["FarmOwner", "FarmEngineer"])).IsValid);
    }
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void Pagination_rejects_unbounded_or_overflowing_queries(int page, int size) =>
        Assert.False(new PageQueryValidator().Validate(new PageQuery() { Page = page, PageSize = size }).IsValid);
}
