using Aonik.Authorization;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Contracts.Services.Remittance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Remittance;

public sealed class ListOperatorRemittanceDestinationsEndpoint(IOperatorRemittanceService service)
    : EndpointWithoutRequest<PayoutBeneficiaryListResponse>
{
    public override void Configure()
    {
        Get("/remittance/customers/{customerPartyId:guid}/destinations");
        Policies("AdminReadPolicy");
        this.RequiresPermission("Payment.Read");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var customerId = Route<Guid>("customerPartyId");
        var destinations = await service.ListDestinationsForCustomerAsync(customerId, ct);
        await Send.OkAsync(new PayoutBeneficiaryListResponse(customerId, destinations), ct);
    }
}
