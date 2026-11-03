using Aonik.Authorization;
using Aonik.Finance.Contracts.Services.Payments;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Remittance;

public sealed class RemoveOperatorDestinationEndpoint(IOperatorPayoutBeneficiaryService service) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/remittance/customers/{customerPartyId:guid}/destinations/{destinationId:guid}");
        Policies("AdminWritePolicy");
        this.RequiresPermission("Payment.Create");
        this.RequiresPermission("Customers.Write");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await service.RemoveDestinationAsync(Route<Guid>("customerPartyId"), Route<Guid>("destinationId"), ct);
        await Send.NoContentAsync(ct);
    }
}
