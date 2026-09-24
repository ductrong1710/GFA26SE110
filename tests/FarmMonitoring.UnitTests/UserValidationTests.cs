using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Users;

namespace FarmMonitoring.UnitTests;

public class UserValidationTests
{
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
        Assert.True(new CreateUserValidator().Validate(new CreateUserRequest(" X@example.com ", "User", "Test-password-123!", ["UavDeviceOperator"])).IsValid);
        Assert.True(new UserRolesValidator().Validate(new UserRolesRequest([])).IsValid);
        Assert.True(new UserStatusValidator().Validate(new UserStatusRequest(false)).IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void Pagination_rejects_unbounded_or_overflowing_queries(int page, int size) =>
        Assert.False(new PageQueryValidator().Validate(new PageQuery() { Page = page, PageSize = size }).IsValid);
}

