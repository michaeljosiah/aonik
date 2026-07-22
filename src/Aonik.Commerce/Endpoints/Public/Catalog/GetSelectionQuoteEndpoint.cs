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
        StorefrontCacheHeaders.Apply(HttpContext);

        var slug = Route<string>("slug") ?? string.Empty;
        var product = await _products.GetProductBySlugAsync(slug, ct);
        if (product is null || product.Status != ProductStatuses.Active)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Currency is required for a *quote*. A product can combine independently authored option
        // groups and a surcharge in different currencies, so without a target there is nothing to
        // check them against (V10) and any total would be a sum of denominations wearing one
        // label. Callers that only want validation and the canonical form use the option
        // catalogue plus IOptionSelectionService.NormalizeAsync internally, not this endpoint.
        if (string.IsNullOrWhiteSpace(req.Currency))
        {
            throw new OptionValidationException(
                "V10",
                "A quote requires a currency; supply the storefront currency so option amounts can be validated against it.");
        }

        var result = await _selections.NormalizeAndPriceAsync(product.Id, req.Selection, req.Currency, ct);
        await Send.OkAsync(result, ct);
    }
}
