using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Contracts.Services.Remittance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Partners;

/// <summary>Acknowledgement returned to a partner after a callback is ingested.</summary>
public record PartnerWebhookAck(bool Received);

/// <summary>
/// <c>POST /partners/webhooks/{providerCode}</c> — inbound partner callback sink. Anonymous: partners
/// cannot present an Aonik user token, so trust comes from the per-provider signature verified inside
/// the processor, not from an auth policy. The endpoint only reads the raw body + headers and hands an
/// envelope to the idempotent processor; it never mutates state directly. Spec 036 §9.1.
/// </summary>
public class PartnerWebhookEndpoint : EndpointWithoutRequest<PartnerWebhookAck>
{
    private readonly IRemittanceOrderService _remittanceService;

    public PartnerWebhookEndpoint(IRemittanceOrderService remittanceService)
    {
        _remittanceService = remittanceService;
    }

    public override void Configure()
    {
        Post("/partners/webhooks/{providerCode}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Ingest a partner webhook";
            s.Description = "Receives a partner money-movement callback, verifies its signature, dedupes it, and idempotently settles or reverses the matching remittance.";
            s.Response(200, "Callback received");
        });
        Options(x => x.WithTags("Partners"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var providerCode = Route<string>("providerCode") ?? string.Empty;

        using var reader = new StreamReader(HttpContext.Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        var headers = HttpContext.Request.Headers
            .ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        var envelope = new PartnerWebhookEnvelope(providerCode, headers, body);
        await _remittanceService.ProcessWebhookAsync(envelope, ct);

        await Send.OkAsync(new PartnerWebhookAck(true), ct);
    }
}
