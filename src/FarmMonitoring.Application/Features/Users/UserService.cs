using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Users;

public sealed class UserService(IUserRepository repository, IPasswordService passwords, TimeProvider clock,
    IValidator<CreateUserRequest> createValidator, IValidator<UpdateUserRequest> updateValidator,
    IValidator<UserStatusRequest> statusValidator, IValidator<UserRolesRequest> rolesValidator,
    IValidator<PageQuery> pageValidator)
{
    public async Task<PagedResult<ManagedUserResponse>> ListAsync(PageQuery query, CancellationToken ct)
    {
        await pageValidator.ValidateAndThrowAsync(query, ct);
        return await repository.ListAsync(query, ct);
    }

    public async Task<ManagedUserResponse> GetAsync(int id, CancellationToken ct) => ToResponse(await RequireAsync(id, ct));

    public async Task<ManagedUserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        var user = new User { Email = NormalizeEmail(request.Email), FullName = request.FullName.Trim(), CreatedAt = clock.GetUtcNow() };
        user.PasswordHash = passwords.Hash(user, request.Password);
        foreach (var role in await repository.GetRolesAsync(request.Roles, ct))
            user.UserRoles.Add(new UserRole { RoleId = role.Id, Role = role });
        repository.Add(user);
        await repository.SaveAsync(ct);
        return ToResponse(user);
    }

    public async Task<ManagedUserResponse> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        var user = await RequireAsync(id, ct);
        user.Email = NormalizeEmail(request.Email);
        user.FullName = request.FullName.Trim();
        return await SaveAsync(user, ct);
    }

    public async Task<ManagedUserResponse> SetStatusAsync(int id, UserStatusRequest request, CancellationToken ct)
    {
        await statusValidator.ValidateAndThrowAsync(request, ct);
        var user = await RequireAsync(id, ct);
        user.IsActive = request.IsActive!.Value;
        return await SaveAsync(user, ct);
    }

    public async Task<ManagedUserResponse> SetRolesAsync(int id, UserRolesRequest request, CancellationToken ct)
    {
        await rolesValidator.ValidateAndThrowAsync(request, ct);
        var roles = await repository.GetRolesAsync(request.Roles, ct);
        var user = await repository.ReplaceRolesAsync(id, roles, clock.GetUtcNow(), ct)
            ?? throw new NotFoundException("User not found.");
        return ToResponse(user);
    }

    private async Task<User> RequireAsync(int id, CancellationToken ct) =>
        await repository.FindAsync(id, ct) ?? throw new NotFoundException("User not found.");

    private async Task<ManagedUserResponse> SaveAsync(User user, CancellationToken ct)
    {
        user.UpdatedAt = clock.GetUtcNow();
        await repository.SaveAsync(ct);
        return ToResponse(user);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static ManagedUserResponse ToResponse(User user) => new(user.Id, user.Email, user.FullName, user.IsActive,
        user.UserRoles.Select(x => x.Role.Name).Order().ToArray(), user.CreatedAt, user.UpdatedAt);
}
