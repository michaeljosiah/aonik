using Aonik.Commerce.Contracts.Api.Inventory;
using Aonik.Commerce.Endpoints.Public.Catalog;
using Aonik.Commerce.Services.Inventory;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Inventory;

public class GetAvailabilityEndpoint : EndpointWithoutRequest<InventoryAvailabilityResponse>
{
    private readonly IInventoryService _inventory;

    public GetAvailabilityEndpoint(IInventoryService inventory) => _inventory = inventory;

    public override void Configure()
    {
        Get("/commerce/catalog/variants/{variantId:guid}/availability");
        AllowAnonymous();
        Summary(s => s.Summary = "Available units for a variant.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // A tenant-specific catalog read on a tenant-less path — the same shared-cache hazard as
        // every other anonymous catalog surface (Spec 070 A14).
        StorefrontCacheHeaders.Apply(HttpContext);

        var variantId = Route<Guid>("variantId");
        var available = await _inventory.GetAvailableAsync(variantId, ct);
        await Send.OkAsync(new InventoryAvailabilityResponse(variantId, available), ct);
    }
}
