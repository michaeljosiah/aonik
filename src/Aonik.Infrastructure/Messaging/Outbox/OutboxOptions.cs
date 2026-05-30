namespace Aonik.Infrastructure.Messaging.Outbox;

/// <summary>
/// Tunables for the transactional outbox processor, bound from the "Outbox"
/// configuration section in the Worker host.
/// </summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Maximum number of messages claimed per polling sweep.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Dispatch attempts before a message is dead-lettered.</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>Idle delay between polling sweeps when the previous sweep found no full batch.</summary>
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>Base of the exponential backoff applied between retries.</summary>
    public int BaseBackoffSeconds { get; set; } = 30;

    /// <summary>Ceiling on the exponential backoff.</summary>
    public int MaxBackoffSeconds { get; set; } = 1800;

    /// <summary>Grace period after host start before the first sweep, so startup migrations settle.</summary>
    public int StartupDelaySeconds { get; set; } = 5;
}
