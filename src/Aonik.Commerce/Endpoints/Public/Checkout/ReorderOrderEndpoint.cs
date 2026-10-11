using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions;

using Aonik.Commerce.Services.Catalog;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

public sealed class ReorderOrderEndpoint(IOrderReorderService reorder, ICurrentPartyResolver parties)
    : EndpointWithoutRequest<BoxCartDto>
{
    public override void Configure()
    {
        Post("/commerce/storefront/orders/{orderId:guid}/reorder");
        Policies("AdminUserPolicy");
        Summary(summary => summary.Summary = "Start a fresh box from your purchased dishes using today's catalogue.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partyId = await parties.GetCurrentPartyIdAsync(ct);
        HttpContext.Response.Headers.CacheControl = "no-store";
        ReorderSelectionRequest? request = null;
        if (HttpContext.Request.ContentLength > 0 || HttpContext.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            try { request = await HttpContext.Request.ReadFromJsonAsync<ReorderSelectionRequest>(ct); }
            catch (System.Text.Json.JsonException) { throw new StorefrontValidationException("Choose valid purchased dishes."); }
            if (request?.Selections is null) throw new StorefrontValidationException("Choose valid purchased dishes.");
        }
        var box = partyId is not { } party ? null : request is null
            ? await reorder.ReorderAsync(Route<Guid>("orderId"), party, ct)
            : await reorder.ReorderSelectedAsync(Route<Guid>("orderId"), party, request.Selections, ct);
        if (box is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(box, ct);
    }
}

public sealed class PreviewReorderOrderEndpoint(IOrderReorderService reorder, ICurrentPartyResolver parties) : EndpointWithoutRequest<ReorderPreviewDto>
{
    public override void Configure() { Get("/commerce/storefront/orders/{orderId:guid}/reorder-preview"); Policies("AdminUserPolicy"); }
    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var party = await parties.GetCurrentPartyIdAsync(ct);
        var preview = party is { } id ? await reorder.PreviewAsync(Route<Guid>("orderId"), id, ct) : null;
        if (preview is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(preview, ct);
    }
}
