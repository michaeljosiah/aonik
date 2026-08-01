using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Aonik.PersonalFinance.Services;

/// <summary>How much the anonymous invite preview discloses about the shared entities (Spec 061 §5).</summary>
public enum InvitePreviewDisclosure
{
    /// <summary>Return the shared entities' names ("Ama shared: Mum, Surulere flat"). Warmer; the default.</summary>
    Names,

    /// <summary>Return only a count ("2 people &amp; places") — the tenant-level dial-back if name abuse appears.</summary>
    Counts,
}

/// <summary>
/// Server-side switches for the anonymous circle-invite preview (Spec 061). Bound from the
/// <c>PersonalFinance:Circle</c> config section, with safe defaults so an unconfigured deployment
/// still previews (Names) and rate-limits.
/// </summary>
public sealed class CircleInviteOptions
{
    /// <summary>
    /// Where these settings live now that sharing is a platform capability (Spec 086 §14).
    /// </summary>
    public const string SectionName = "Platform:Sharing";

    /// <summary>
    /// The section they lived in before, still bound first so a deployment that sets it keeps
    /// working.
    /// </summary>
    /// <remarks>
    /// Both are bound, legacy first and the new one over it, so an operator who has set either gets
    /// what they configured and one who has set both gets the new one. Silently ignoring the old
    /// section would be the worst outcome available: a tenant that had dialled disclosure back to
    /// Counts would quietly start disclosing names again. A startup warning names the deployments
    /// that still need moving; removing the fallback is a follow-up.
    /// </remarks>
    public const string LegacySectionName = "PersonalFinance:Circle";

    /// <summary>Names (default) | Counts — the single disclosure dial (§5, §14).</summary>
    public InvitePreviewDisclosure PreviewDisclosure { get; set; } = InvitePreviewDisclosure.Names;

    /// <summary>Max anonymous preview reads per client IP within <see cref="PreviewRateLimitWindowSeconds"/>. 0 disables.</summary>
    public int PreviewRateLimitPerIp { get; set; } = 30;

    /// <summary>Max anonymous preview reads per token within the window (blunts hammering one token). 0 disables.</summary>
    public int PreviewRateLimitPerToken { get; set; } = 10;

    /// <summary>The fixed window, in seconds, for both ceilings.</summary>
    public int PreviewRateLimitWindowSeconds { get; set; } = 60;
}

/// <summary>
/// Per-IP and per-token fixed-window rate limiter for the anonymous invite preview (Spec 061 §10).
/// In-memory (per instance) — defence in depth over the already high-entropy, 7-day-scoped tokens,
/// to blunt token farming and enumeration, not a hard boundary.
/// </summary>
public interface IInvitePreviewRateLimiter
{
    /// <summary>True to allow the read; false when either the per-IP or per-token ceiling is exceeded in the window.</summary>
    bool ShouldAllow(string clientIp, string token);
}

internal sealed class InvitePreviewRateLimiter : IInvitePreviewRateLimiter
{
    private readonly IMemoryCache _cache;
    private readonly CircleInviteOptions _options;

    public InvitePreviewRateLimiter(IMemoryCache cache, IOptions<CircleInviteOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public bool ShouldAllow(string clientIp, string token)
    {
        var window = TimeSpan.FromSeconds(Math.Max(1, _options.PreviewRateLimitWindowSeconds));

        // Check BOTH so neither short-circuits the other's count: a request must be under the IP
        // ceiling AND the token ceiling. Evaluated eagerly (no &&) so both counters advance on every hit.
        var ipOk = Hit($"circle-preview-rl:ip:{clientIp}", _options.PreviewRateLimitPerIp, window);
        var tokenOk = Hit($"circle-preview-rl:token:{token}", _options.PreviewRateLimitPerToken, window);
        return ipOk && tokenOk;
    }

    private bool Hit(string key, int ceiling, TimeSpan window)
    {
        if (ceiling <= 0)
        {
            return true; // a non-positive ceiling disables that dimension
        }

        // Fixed window: the first hit for a key creates a counter with an absolute expiry; hits within
        // the window increment it. The counter is a boxed object so Interlocked keeps the increment atomic.
        // GetOrCreate itself isn't atomic under a burst of first-hits, but the only effect is a rare
        // UNDER-count — acceptable for a defence-in-depth limiter that must never block a legitimate first read.
        var counter = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = window;
            return new Counter();
        })!;

        return Interlocked.Increment(ref counter.Count) <= ceiling;
    }

    private sealed class Counter
    {
        public int Count;
    }
}
