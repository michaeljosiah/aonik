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
    /// Client ID of a user-assigned managed identity to use when acquiring
    /// the <c>https://dynamicsessions.io/.default</c> token. When empty, falls
    /// back to the system-assigned identity. See
    /// <see cref="AcaSessionsClient"/> for why a user-assigned identity is
    /// required against ACA Sessions today.
    /// </summary>
    public string ManagedIdentityClientId { get; set; } = string.Empty;

    /// <summary>
    /// ACA Sessions data-plane API version. Pinned to <c>2024-02-02-preview</c>
    /// because that is the only version we've confirmed accepts BOTH user
    /// tokens AND managed-identity tokens against the <c>/code/execute</c>
    /// endpoint (see <see cref="AcaSessionsClient.ExecuteAsync"/> for the
    /// path/version compatibility matrix and why newer versions fail with
    /// HTTP 401 for MI tokens).
    /// </summary>
    public string DataPlaneApiVersion { get; set; } = "2024-02-02-preview";
}
