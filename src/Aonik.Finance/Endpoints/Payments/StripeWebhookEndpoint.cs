using System.Text;

using Aonik.Finance.Contracts.Services.Payments;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Payments;

public sealed class StripeWebhookEndpoint(IStripeWebhookService webhooks) : EndpointWithoutRequest
{
    private const int MaximumBodyBytes = 256 * 1024;

    public override void Configure()
    {
        Post("/integrations/stripe/webhooks/{connectorId:guid}");
        AllowAnonymous();
        Summary(summary => summary.Summary = "Durably accept a signature-verified Stripe notification.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var signatures = HttpContext.Request.Headers["Stripe-Signature"];
        if (signatures.Count != 1 || string.IsNullOrWhiteSpace(signatures[0]) || signatures[0]!.Length > 4096)
        {
            await Send.ResultAsync(Results.BadRequest(new { error = "A single Stripe signature is required." }));
            return;
        }
        using var body = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await HttpContext.Request.Body.ReadAsync(buffer, ct)) > 0)
        {
            if (body.Length + read > MaximumBodyBytes)
            {
                await Send.ResultAsync(Results.StatusCode(StatusCodes.Status413PayloadTooLarge));
                return;
            }
            await body.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        string raw;
        try { raw = new UTF8Encoding(false, true).GetString(body.ToArray()); }
        catch (DecoderFallbackException)
        {
            await Send.ResultAsync(Results.BadRequest(new { error = "The notification must be UTF-8." }));
            return;
        }
        try { await webhooks.AcceptAsync(Route<Guid>("connectorId"), raw, signatures[0]!, ct); }
        catch (ArgumentException)
        {
            await Send.ResultAsync(Results.BadRequest(new { error = "Invalid Stripe notification." }));
            return;
        }
        await Send.OkAsync(cancellation: ct);
    }
}
