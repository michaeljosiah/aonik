using Aonik.Commerce.Contracts.Api.Sourcing;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class CreateSupplierEndpoint : Endpoint<CreateSupplierRequest, SupplierDto>
{
    private readonly ISupplierService _suppliers;

    public CreateSupplierEndpoint(ISupplierService suppliers) => _suppliers = suppliers;

    public override void Configure()
    {
        Post("/commerce/admin/suppliers");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Register a supplier (a counterparty we buy raw materials from).");
    }

    public override async Task HandleAsync(CreateSupplierRequest req, CancellationToken ct)
    {
        var result = await _suppliers.CreateAsync(
            new CreateSupplierCommand(req.Name, req.Currency, req.PartyId, req.LeadTimeDays, req.PaymentTerms), ct);
        await Send.OkAsync(result, ct);
    }
}
