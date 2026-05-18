namespace Aonik.Finance.Agents.CodeAct;

/// <summary>
/// Bound from <c>Ai:CodeAct:AcaSessions</c>. The deploy workflow forwards
/// any env var prefixed <c>AI__*</c> into <c>Ai:*</c> (see
/// <c>.github/workflows/cd-deploy.yml</c>), so production values land here
/// as <c>AI__CODEACT__ACASESSIONS__POOLMANAGEMENTENDPOINT</c> etc.
/// </summary>
public sealed class AcaSessionsOptions
{
    public const string SectionName = "Ai:CodeAct:AcaSessions";

    /// <summary>
    /// Pool management endpoint, e.g.
    /// <c>https://uksouth.dynamicsessions.io/subscriptions/&lt;sub&gt;/resourceGroups/&lt;rg&gt;/sessionPools/&lt;name&gt;</c>.
    /// </summary>
    public string PoolManagementEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Absolute base URL for our API as the Python sandbox sees it — used to
    /// build the callback URL baked into the per-execution preamble. e.g.
    /// <c>https://aonik-dev-api.&lt;defaultDomain&gt;</c>.
    /// </summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Sandbox session cooldown in seconds. Matches the pool's bicep value;
    /// the client only uses it for diagnostic logging today.
    /// </summary>
    public int SessionCooldownSeconds { get; set; } = 300;

    /// <summary>
    /// Nonce lifetime in seconds. Default 600s gives a comfortable margin
    /// over the longest sub-agent run we've observed.
    /// </summary>
    public int NonceTtlSeconds { get; set; } = 600;

    /// <summary>
    /// Total callbacks allowed per <c>execute_code</c> invocation. Caps the
    /// blast radius of a runaway Python loop.
    /// </summary>
    public int MaxCallbacksPerNonce { get; set; } = 30;

    /// <summary>
    /// ACA Sessions data-plane API version. Pinned here so we can bump
    /// without redeploying when Microsoft GA's a new version.
    /// </summary>
    public string DataPlaneApiVersion { get; set; } = "2025-10-02-preview";
}
