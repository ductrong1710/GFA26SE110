namespace FarmMonitoring.Application.Features.Auth;

public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record UserResponse(int Id, string Email, string FullName, IReadOnlyList<string> Roles);
public sealed record AuthResponse(string AccessToken, string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt, int ExpiresIn, UserResponse User);
public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt, int ExpiresIn);
public sealed record RefreshTokenMaterial(string Token, string Hash, DateTimeOffset ExpiresAt);
public sealed record PasswordCheck(bool Succeeded, bool NeedsRehash);
