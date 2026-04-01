namespace Aonik.Platform.Entities.Operations;

public static class AzureMonitorAlertProviders
{
    public const string AzureMonitor = "AzureMonitor";
}

public static class AzureMonitorAlertStatuses
{
    public const string Received = "Received";
    public const string Processing = "Processing";
    public const string Processed = "Processed";
    public const string Failed = "Failed";
    public const string Ignored = "Ignored";
}

public static class AzureMonitorAlertTypes
{
    public const string PlatformAvailabilityAlert = "PlatformAvailabilityAlert";
    public const string PlatformPerformanceAlert = "PlatformPerformanceAlert";
    public const string PlatformSecurityAlert = "PlatformSecurityAlert";
    public const string PlatformOperationsAlert = "PlatformOperationsAlert";
    public const string PlatformAvailabilityResolved = "PlatformAvailabilityResolved";
    public const string PlatformPerformanceResolved = "PlatformPerformanceResolved";
    public const string PlatformSecurityResolved = "PlatformSecurityResolved";
    public const string PlatformOperationsResolved = "PlatformOperationsResolved";
}

public static class AzureMonitorAlertConditions
{
    public const string Fired = "Fired";
    public const string Resolved = "Resolved";
}
