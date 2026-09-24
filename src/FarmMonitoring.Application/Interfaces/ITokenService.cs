using FarmMonitoring.Application.Features.Auth;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(User user, DateTimeOffset now);
    RefreshTokenMaterial CreateRefreshToken(DateTimeOffset now);
    string HashRefreshToken(string token);
}
