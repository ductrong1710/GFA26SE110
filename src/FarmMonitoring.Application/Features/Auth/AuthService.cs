using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Domain.Constants;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Auth;

public sealed class AuthService(
    IAuthRepository repository, IPasswordService passwords, ITokenService tokens,
    TimeProvider clock, IValidator<LoginRequest> loginValidator, IValidator<RefreshRequest> refreshValidator)
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);
        var user = await repository.GetUserByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        var check = passwords.Verify(user, request.Password);
        if (user is null || !check.Succeeded || !user.IsActive || !user.UserRoles.Any(x => RoleNames.IsSupported(x.Role.Name)))
            throw new AuthException("Invalid email or password.");

        var now = clock.GetUtcNow();
        if (check.NeedsRehash)
        {
            user.PasswordHash = passwords.Hash(user, request.Password);
            user.UpdatedAt = now;
        }
        var refresh = tokens.CreateRefreshToken(now);
        await repository.SaveLoginAsync(NewStoredToken(refresh, now, user.Id), ct);
        return CreateResponse(user, refresh, now);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        await refreshValidator.ValidateAndThrowAsync(request, ct);
        var now = clock.GetUtcNow();
        var refresh = tokens.CreateRefreshToken(now);
        var user = await repository.RotateAsync(tokens.HashRefreshToken(request.RefreshToken), NewStoredToken(refresh, now), ct);
        if (user is null)
            throw new AuthException("Invalid or expired refresh token.");
        return CreateResponse(user, refresh, clock.GetUtcNow());
    }

    public async Task LogoutAsync(int userId, RefreshRequest request, CancellationToken ct)
    {
        await refreshValidator.ValidateAndThrowAsync(request, ct);
        if (!await repository.RevokeAsync(userId, tokens.HashRefreshToken(request.RefreshToken), ct))
            throw new AuthException("Invalid or expired refresh token.");
    }

    public async Task<UserResponse> GetCurrentUserAsync(int userId, CancellationToken ct)
    {
        var user = await repository.GetUserByIdAsync(userId, ct);
        if (user is null || !user.IsActive || !user.UserRoles.Any(x => RoleNames.IsSupported(x.Role.Name)))
            throw new AuthException("Authentication required.");
        return ToResponse(user);
    }

    private AuthResponse CreateResponse(User user, RefreshTokenMaterial refresh, DateTimeOffset now)
    {
        var access = tokens.CreateAccessToken(user, now);
        return new AuthResponse(access.Token, refresh.Token, access.ExpiresAt, access.ExpiresIn, ToResponse(user));
    }

    private static RefreshToken NewStoredToken(RefreshTokenMaterial material, DateTimeOffset now, int userId = 0) =>
        new() { UserId = userId, TokenHash = material.Hash, CreatedAt = now, ExpiresAt = material.ExpiresAt };

    private static UserResponse ToResponse(User user) => new(user.Id, user.Email, user.FullName,
        user.UserRoles.Select(x => x.Role.Name).Where(RoleNames.IsSupported).Distinct().Order().ToArray());
}
