using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class ListSuppliersEndpoint : EndpointWithoutRequest<IReadOnlyList<SupplierDto>>
{
    private readonly ISupplierService _suppliers;

    public ListSuppliersEndpoint(ISupplierService suppliers) => _suppliers = suppliers;

    public override void Configure()
    {
        Get("/commerce/admin/suppliers");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "List the tenant's suppliers, ordered by name (active only unless includeInactive=true).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var includeInactive = Query<bool?>("includeInactive", isRequired: false) ?? false;
        var result = await _suppliers.ListAsync(includeInactive, ct);
        await Send.OkAsync(result, ct);
    }
}
