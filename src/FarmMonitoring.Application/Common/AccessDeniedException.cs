namespace FarmMonitoring.Application.Common;

public sealed class AccessDeniedException(string message) : Exception(message)
{
    public static AccessDeniedException OutsideScope() => new("You do not have access to this resource.");
}
