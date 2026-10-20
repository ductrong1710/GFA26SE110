namespace FarmMonitoring.Application.Interfaces;

public interface IPasswordResetQueue
{
    void Enqueue(string normalizedEmail);
}
