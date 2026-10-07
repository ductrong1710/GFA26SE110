namespace FarmMonitoring.Domain.Constants;

public static class RoleNames
{
    public const string FarmAdministrator = "FarmAdministrator";
    public const string FarmOwner = "FarmOwner";
    public const string FarmEngineer = "FarmEngineer";

    public static bool IsSupported(string? role) => role is FarmAdministrator or FarmOwner or FarmEngineer;
}
