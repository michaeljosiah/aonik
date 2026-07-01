using Aonik.Commerce.Contracts.Api.Sourcing;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class UpdateSupplierEndpoint : Endpoint<UpdateSupplierRequest, SupplierDto>
{
    private readonly ISupplierService _suppliers;

    public UpdateSupplierEndpoint(ISupplierService suppliers) => _suppliers = suppliers;

    public override void Configure()
    {
        Put("/commerce/admin/suppliers/{supplierId:guid}");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary = "Update a supplier's master data. Omit isActive to leave the stored active state unchanged.");
    }

    public override async Task HandleAsync(UpdateSupplierRequest req, CancellationToken ct)
    {
        var supplierId = Route<Guid>("supplierId");
        var result = await _suppliers.UpdateAsync(
            new UpdateSupplierCommand(supplierId, req.Name, req.Currency, req.PartyId, req.LeadTimeDays, req.PaymentTerms, req.IsActive), ct);
        await Send.OkAsync(result, ct);
    }
}
