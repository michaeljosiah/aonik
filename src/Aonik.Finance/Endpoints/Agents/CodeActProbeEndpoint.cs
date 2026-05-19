using Aonik.Finance.Agents.CodeAct;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Agents;

/// <summary>
/// Self-contained probe of the AcaSessions stack: invokes
/// <see cref="AcaSessionsClient.ExecuteAsync"/> with a trivial Python script
/// (<c>print("hello")</c>) and returns the result + the token claims in the
/// SAME response. Sidesteps the multi-replica problem with /debug-tail —
/// because the call happens INSIDE this request, the static
/// <c>LastTokenClaimsForDiagnostic</c> we read is guaranteed to be the one
/// the call just populated.
/// </summary>
/// <remarks>
/// Admin-only. Diagnostic-only — does NOT mint a nonce or call back, so
/// nothing in the API state changes besides the static diagnostic slots.
/// </remarks>
internal sealed class CodeActProbeEndpoint : EndpointWithoutRequest<CodeActProbeResponse>
{
    private readonly AcaSessionsClient _client;

    public CodeActProbeEndpoint(AcaSessionsClient client)
    {
        _client = client;
    }

    public override void Configure()
    {
        Post("/ai/codeact/probe");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Self-contained probe of the AcaSessions stack";
            s.Description = "Invokes ACA Sessions /executions with a trivial Python script and returns the response inline alongside the token claims — so the developer doesn't have to deal with the multi-replica problem that /debug-tail hits.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessionId = "codeact-probe-" + Guid.NewGuid().ToString("N");
        string? execError = null;
        AcaSessionsExecutionResult? result = null;
        try
        {
            result = await _client.ExecuteAsync(
                sessionIdentifier: sessionId,
                code: "print('probe ok')",
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            execError = $"{ex.GetType().Name}: {ex.Message}";
        }

        await Send.OkAsync(new CodeActProbeResponse(
            SessionIdentifier: sessionId,
            ExecuteResult: result,
            ExceptionMessage: execError,
            LastTokenClaims: AcaSessionsClient.LastTokenClaimsForDiagnostic), ct);
    }
}

public sealed record CodeActProbeResponse(
    string SessionIdentifier,
    AcaSessionsExecutionResult? ExecuteResult,
    string? ExceptionMessage,
    string? LastTokenClaims);
