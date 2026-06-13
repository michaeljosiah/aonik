using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Ai.Endpoints;

/// <summary>
/// <c>POST /ai/capture/parse</c> (Spec 047) — turns a captured image, text, or
/// audio transcript into a structured draft proposal. Consumer-reachable under
/// <c>AdminUserPolicy</c> (which includes <c>PersonalUser</c>), matching
/// <c>/ai/chat</c>. Persists nothing: the response is a proposal the user must
/// confirm before Spec 045 writes a PaymentLog.
/// </summary>
internal sealed class CaptureParseEndpoint : Endpoint<CaptureParseRequest, CaptureParseResponse>
{
    private readonly ICaptureParseService _captureParseService;

    public CaptureParseEndpoint(ICaptureParseService captureParseService)
    {
        _captureParseService = captureParseService;
    }

    public override void Configure()
    {
        Post("/ai/capture/parse");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Parse a captured image / text / transcript into a draft";
            s.Description =
                "Turns a receipt image, payment screenshot, forwarded message, or sentence — plus the " +
                "user's entity + open-commitment hints — into a structured paymentLog draft proposal. " +
                "Never auto-saved: the review screen is mandatory and only a user-confirmed draft becomes " +
                "a PaymentLog. Every parse is recorded as an AiRun; the raw payload is not stored.";
            s.Response(200, "Draft proposal (parsed, lowConfidence, or unparseable)");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation failed");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(CaptureParseRequest req, CancellationToken ct)
    {
        var response = await _captureParseService.ParseAsync(req, ct);
        await Send.OkAsync(response, ct);
    }
}
