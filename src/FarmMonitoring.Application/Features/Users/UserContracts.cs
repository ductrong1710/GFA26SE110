namespace FarmMonitoring.Application.Features.Users;

public sealed record CreateUserRequest(string Email, string FullName, string Password, string[] Roles);
public sealed record UpdateUserRequest(string Email, string FullName);
public sealed record UserStatusRequest(bool? IsActive);
public sealed record UserRolesRequest(string[] Roles);
public sealed record ManagedUserResponse(int Id, string Email, string FullName, bool IsActive,
    IReadOnlyList<string> Roles, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
