using Aonik.Finance.Agents.CodeAct;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Agents;

/// <summary>
/// Returns the last <c>execute_code</c> response captured by the AcaSessions
/// provider so a developer can inspect raw stdout/stderr without waiting for
/// log shipping. Admin-only; intended for diagnostic use during the Spec 025
/// rollout. Safe to keep — never leaks the nonce signing key.
/// </summary>
internal sealed class CodeActDebugTailEndpoint : EndpointWithoutRequest<CodeActDebugTailResponse>
{
    public override void Configure()
    {
        Get("/ai/codeact/debug-tail");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Last execute_code result + last token claims captured by the AcaSessions provider";
            s.Description = "Diagnostic endpoint for the Spec 025 sandbox rollout. Returns the most recent ACA Dynamic Sessions execute_code response and the subset of JWT claims (aud/iss/oid/appid/exp) of the token used — so a developer can see what the Python sandbox printed AND which managed identity was actually authenticating.";
            s.Response(200, "Last diagnostic; null when no execute_code has run yet");
        });
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(new CodeActDebugTailResponse(
            AcaSessionsCodeActSandboxProvider.LastExecution,
            AcaSessionsClient.LastTokenClaimsForDiagnostic), ct);
}

public sealed record CodeActDebugTailResponse(
    AcaSessionsCodeActSandboxProvider.AcaSessionsExecutionDiagnostic? LastExecution,
    string? LastTokenClaims);
