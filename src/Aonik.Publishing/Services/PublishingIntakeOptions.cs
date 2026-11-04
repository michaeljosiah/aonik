namespace Aonik.Publishing.Services;

public sealed class PublishingIntakeOptions
{
    public string PrivateRoot { get; set; } = string.Empty;
    public long MaxCreatorRetainedBytes { get; set; }
    public long MaxTenantRetainedBytes { get; set; }
    public int MaxCreatorPackages { get; set; } = 20;
    public int MaxTenantPackages { get; set; } = 200;
    public int MaxCreatorNamespaces { get; set; } = 100;
    public int MaxSessionsPerCreatorPerDay { get; set; } = 100;
    public int SingleShotThresholdBytes { get; set; } = 32 * 1024 * 1024;
    public int PartSizeBytes { get; set; } = 8 * 1024 * 1024;
    public int SessionHours { get; set; } = 48;
    public int AssemblyMinutes { get; set; } = 10;
    public int CleanupGraceHours { get; set; } = 24;
    public int MaxValidationAttempts { get; set; } = 3;
}
