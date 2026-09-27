namespace FarmMonitoring.Application.Common;

public sealed class AccessDeniedException(string message) : Exception(message);
