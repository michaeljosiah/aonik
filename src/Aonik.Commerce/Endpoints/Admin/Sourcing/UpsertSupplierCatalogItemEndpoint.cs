using Aonik.Commerce.Contracts.Api.Sourcing;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class UpsertSupplierCatalogItemEndpoint : Endpoint<UpsertSupplierIngredientRequest, SupplierIngredientDto>
{
    private readonly ISupplierService _suppliers;

    public UpsertSupplierCatalogItemEndpoint(ISupplierService suppliers) => _suppliers = suppliers;

    public override void Configure()
    {
        Put("/commerce/admin/suppliers/{supplierId:guid}/catalog");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary = "Upsert one supplier price-list row (supplier x ingredient): the pack we buy in (PackSize, in the ingredient's base unit), the pack price, and the supplier's SKU.");
    }

    public override async Task HandleAsync(UpsertSupplierIngredientRequest req, CancellationToken ct)
    {
        var supplierId = Route<Guid>("supplierId");
        var result = await _suppliers.UpsertCatalogItemAsync(
            new UpsertSupplierIngredientCommand(supplierId, req.IngredientId, req.PackSize, req.PackPrice, req.Currency, req.Sku, req.LeadTimeDays), ct);
        await Send.OkAsync(result, ct);
    }
}
