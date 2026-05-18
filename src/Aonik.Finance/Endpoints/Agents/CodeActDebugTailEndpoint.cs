using Aonik.Finance.Agents.CodeAct;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Agents;

/// <summary>
/// Returns the last <c>execute_code</c> response captured by the AcaSessions
/// provider so a developer can inspect raw stdout/stderr without waiting for
/// log shipping. Admin-only; intended for diagnostic use during the Spec 025
/// rollout. Safe to keep — never leaks the nonce signing key.
/// </summary>
internal sealed class CodeActDebugTailEndpoint : EndpointWithoutRequest<AcaSessionsCodeActSandboxProvider.AcaSessionsExecutionDiagnostic?>
{
    public override void Configure()
    {
        Get("/ai/codeact/debug-tail");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Last execute_code result captured by the AcaSessions provider";
            s.Description = "Diagnostic endpoint for the Spec 025 sandbox rollout. Returns the most recent ACA Dynamic Sessions execute_code response (status + stdout + stderr + timing) so a developer can see what the Python sandbox actually printed without scraping container logs.";
            s.Response(200, "Last diagnostic, or null if no execute_code has run yet");
        });
        // No WithTags — keep parity with the CodeActCallbackEndpoint until
        // a Tags helper is in place. Swagger discovery still works.
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(AcaSessionsCodeActSandboxProvider.LastExecution, ct);
}
