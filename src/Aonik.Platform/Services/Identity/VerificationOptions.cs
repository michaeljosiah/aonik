namespace Aonik.Platform.Services.Identity;

internal class VerificationOptions
{
    public int CodeLength { get; set; } = 6;
    public int CodeTtlMinutes { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;
    public string HashKey { get; set; } = string.Empty;
    public VerificationRateLimitOptions RateLimits { get; set; } = new();
}

internal class VerificationRateLimitOptions
{
    public int WindowMinutes { get; set; } = 15;
    public int MaxPerUserChannel { get; set; } = 5;
    public int MaxPerTargetChannel { get; set; } = 10;
}
