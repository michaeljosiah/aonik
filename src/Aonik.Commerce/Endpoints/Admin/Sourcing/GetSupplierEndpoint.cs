using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class GetSupplierEndpoint : EndpointWithoutRequest<SupplierDto>
{
    private readonly ISupplierService _suppliers;

    public GetSupplierEndpoint(ISupplierService suppliers) => _suppliers = suppliers;

    public override void Configure()
    {
        Get("/commerce/admin/suppliers/{supplierId:guid}");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Get a supplier by id.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var supplierId = Route<Guid>("supplierId");
        var result = await _suppliers.GetAsync(supplierId, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(result, ct);
    }
}
