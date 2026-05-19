namespace Aonik.Platform.Services.Identity;

/// <summary>
/// Bound from <c>UserLifecycle</c> in configuration. Drives the invite,
/// revoke, and delete flows added by Spec 026.
/// </summary>
public class UserLifecycleOptions
{
    /// <summary>
    /// Absolute base URL for the Admin UI as the invitee sees it.
    /// Used to assemble the invite link baked into the invitation email
    /// (e.g. <c>https://admin.aonik.dev</c>). Falls back to the request
    /// origin at send time if unset.
    /// </summary>
    public string AdminUiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Invite token lifetime in hours. Default 72 hours per spec §5.
    /// </summary>
    public int InviteTokenTtlHours { get; set; } = 72;

    /// <summary>
    /// Maximum email sends per invited user per 24 hours. Past this
    /// the <c>resend-invite</c> endpoint returns 429.
    /// </summary>
    public int MaxInviteSendsPer24Hours { get; set; } = 5;

    /// <summary>
    /// JWT-bearer lifetime upper bound in days. Drives the prune
    /// horizon for blocklist rows: once <c>RevokedUtc + this</c> is in
    /// the past, no token in circulation can still be subject to the
    /// blocklist, and the row can be safely pruned.
    /// Default 14 days per spec §17 O-4.
    /// </summary>
    public int BlocklistRetentionDays { get; set; } = 14;

    /// <summary>
    /// Cache TTL in seconds for the per-user blocklist lookup in the
    /// JWT validation pipeline. Lower = faster reaction to revoke;
    /// higher = lower DB load. Default 30 s per spec §7.
    /// </summary>
    public int BlocklistCacheTtlSeconds { get; set; } = 30;
}
