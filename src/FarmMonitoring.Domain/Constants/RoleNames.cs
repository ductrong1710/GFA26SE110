namespace FarmMonitoring.Domain.Constants;

public static class RoleNames
{
    public const string FarmAdministrator = "FarmAdministrator";
    public const string FarmOwner = "FarmOwner";
    public const string FarmEngineer = "FarmEngineer";

    public const string UavDeviceOperator = "UavDeviceOperator";

    public static bool IsSupported(string? role) => role is FarmAdministrator or FarmOwner or FarmEngineer or UavDeviceOperator;
}
