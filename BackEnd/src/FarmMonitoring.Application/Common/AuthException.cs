namespace FarmMonitoring.Application.Common;

public sealed class AuthException(string message) : Exception(message);
