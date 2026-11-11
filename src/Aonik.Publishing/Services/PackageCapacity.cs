namespace Aonik.Publishing.Services;

/// <summary>Versioned ceilings shared by intake, transport, evidence and queue leases.</summary>
public sealed record PackageCapacity(long PackageBytes, int Assets, long AssetBytes, int ChunkBytes,
    int MemoryMiB, int Cpu, int VerifySeconds, int TransportSeconds, int JobSeconds, int LeaseSeconds,
    int SessionSeconds, int ReportBytes, int ReviewBytes)
{
    public const string LargeProfile = "large-v1";
    public const int MaximumReportBytes = 1024 * 1024;
    public const int MaximumReviewBytes = 2 * 1024 * 1024;

    public static PackageCapacity Standard { get; } = new(32L * 1024 * 1024, 1024, 16L * 1024 * 1024,
        1024 * 1024, 2048, 1, 45, 180, 240, 300, 300, 256 * 1024, 512 * 1024);
    public static PackageCapacity Large { get; } = new(1536L * 1024 * 1024, 2048, 64L * 1024 * 1024,
        4 * 1024 * 1024, 4096, 2, 180, 900, 1080, 1200, 1200, MaximumReportBytes, MaximumReviewBytes);

    public static bool IsKnown(string? profile) => profile is null or LargeProfile;
    public static PackageCapacity Resolve(string? profile) => profile switch
    {
        null => Standard,
        LargeProfile => Large,
        _ => throw new ArgumentException("Unknown package capacity profile.", nameof(profile)),
    };
}
