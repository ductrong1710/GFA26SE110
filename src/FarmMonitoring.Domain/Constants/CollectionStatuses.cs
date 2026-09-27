namespace FarmMonitoring.Domain.Constants;

public static class CollectionStatuses
{
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";
    public const string Timeout = "TIMEOUT";
    public const string Skipped = "SKIPPED";
}
public static class SyncOutcomeStatuses
{
    public const string Accepted = "ACCEPTED";
    public const string Duplicate = "DUPLICATE";
    public const string Rejected = "REJECTED";
}
public static class SyncBatchStatuses
{
    public const string Completed = "COMPLETED";
    public const string Partial = "PARTIAL";
}
