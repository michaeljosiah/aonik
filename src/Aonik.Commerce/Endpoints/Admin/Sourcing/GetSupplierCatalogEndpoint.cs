using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class GetSupplierCatalogEndpoint : EndpointWithoutRequest<IReadOnlyList<SupplierIngredientDto>>
{
    private readonly ISupplierService _suppliers;

    public GetSupplierCatalogEndpoint(ISupplierService suppliers) => _suppliers = suppliers;

    public override void Configure()
    {
        Get("/commerce/admin/suppliers/{supplierId:guid}/catalog");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "List a supplier's catalog (price list): the ingredients it sells us, with pack size, pack price, and the derived per-base-unit price.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var supplierId = Route<Guid>("supplierId");
        var result = await _suppliers.ListCatalogAsync(supplierId, ct);
        await Send.OkAsync(result, ct);
    }
}
