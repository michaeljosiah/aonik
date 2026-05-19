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
    /// the <c>https://dynamicsessions.io/.default</c> token. When empty,
    /// <see cref="AcaSessionsClient"/> falls back to the system-assigned
    /// identity. Set in cloud by Bicep so DefaultAzureCredential / IMDS
    /// resolves unambiguously to <c>apiPullIdentity</c> (the identity the
    /// session-pool RBAC grant targets); both flavours work as long as the
    /// principal holds Session Executor + Contributor on the pool.
    /// </summary>
    public string ManagedIdentityClientId { get; set; } = string.Empty;

    /// <summary>
    /// ACA Sessions data-plane API version paired with the <c>/code/execute</c>
    /// path. The <c>/executions</c> endpoint exposed by newer versions
    /// (≥ 2024-10-02-preview) expects a different request-body shape, so
    /// switching versions requires the matching code change in
    /// <see cref="AcaSessionsClient.ExecuteAsync"/> — don't bump this alone.
    /// </summary>
    public string DataPlaneApiVersion { get; set; } = "2024-02-02-preview";
}
