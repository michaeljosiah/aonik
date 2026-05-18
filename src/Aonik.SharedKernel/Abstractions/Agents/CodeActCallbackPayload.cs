namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Decoded payload of a CodeAct callback nonce. Carries everything the
/// <c>POST /ai/codeact/call-tool/{nonce}</c> endpoint needs to validate the
/// caller and re-establish request scope before dispatching the named host
/// tool.
/// </summary>
/// <param name="RunId">
/// The sub-agent run that minted this nonce. Echoed back in trace headers.
/// </param>
/// <param name="SubAgentName">
/// Routes tool resolution to the matching
/// <c>PersonalFinanceTools.CreateForXxxSubAgent</c> slice. The endpoint
/// asserts this is one of the registered Spec 025 sub-agent names —
/// don't trust it as a free-form method router.
/// </param>
/// <param name="TenantId">Tenant scope to re-establish.</param>
/// <param name="UserId">User scope to re-establish (null when not impersonating).</param>
/// <param name="ToolWhitelist">
/// Tool names this nonce is allowed to invoke (matches the host-tool slice the
/// sandbox was constructed with). Enforced server-side regardless of what the
/// Python script asks for.
/// </param>
/// <param name="ExpiresAtUtc">UTC expiry. Past this point <c>TryValidate</c> rejects.</param>
/// <param name="Jti">
/// Unique nonce identifier. Used by the in-memory budget tracker to count
/// callbacks per nonce and enforce the per-execute_code budget.
/// </param>
public sealed record CodeActCallbackPayload(
    string RunId,
    string SubAgentName,
    Guid TenantId,
    Guid? UserId,
    IReadOnlyList<string> ToolWhitelist,
    DateTimeOffset ExpiresAtUtc,
    string Jti);
