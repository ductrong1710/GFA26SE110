namespace FarmMonitoring.Application.Common;

public sealed class BusinessRuleException(string message) : Exception(message);
