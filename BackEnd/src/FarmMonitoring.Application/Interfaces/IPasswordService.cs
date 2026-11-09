using FarmMonitoring.Application.Features.Auth;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IPasswordService
{
    string Hash(User user, string password);
    PasswordCheck Verify(User? user, string password);
}
