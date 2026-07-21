using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Catalog;

/// <summary>
/// Prices a selection for an Active product before any cart exists (Spec 066 §11), so the
/// storefront can show authoritative deltas while the customer toggles options. POST carries the
/// body; the call creates no state.
/// </summary>
public class GetSelectionQuoteEndpoint : Endpoint<SelectionQuoteRequest, OptionSelectionResult>
{
    private readonly IProductService _products;
    private readonly IOptionSelectionService _selections;

    public GetSelectionQuoteEndpoint(IProductService products, IOptionSelectionService selections)
    {
        _products = products;
        _selections = selections;
    }

    public override void Configure()
    {
        Post("/commerce/catalog/products/{slug}/selection-quote");
        AllowAnonymous();
        Summary(s => s.Summary = "Validate and price an option selection for a storefront product.");
    }

    public override async Task HandleAsync(SelectionQuoteRequest req, CancellationToken ct)
    {
        var slug = Route<string>("slug") ?? string.Empty;
        var product = await _products.GetProductBySlugAsync(slug, ct);
        if (product is null || product.Status != ProductStatuses.Active)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Currency is optional: supplied means every amount must already be denominated in it
        // (V10); omitted means validate and canonicalise without binding money at all.
        var result = string.IsNullOrWhiteSpace(req.Currency)
            ? await _selections.NormalizeAsync(product.Id, req.Selection, ct)
            : await _selections.NormalizeAndPriceAsync(product.Id, req.Selection, req.Currency, ct);

        await Send.OkAsync(result, ct);
    }
}
