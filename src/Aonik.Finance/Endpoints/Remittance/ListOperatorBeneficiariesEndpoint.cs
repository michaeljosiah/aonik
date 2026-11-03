using Aonik.Authorization;
using Aonik.Finance.Contracts.Services.Payments;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Remittance;

public sealed class ListOperatorBeneficiariesEndpoint(IOperatorPayoutBeneficiaryService service)
    : EndpointWithoutRequest<OperatorBeneficiaryListResponse>
{
    public override void Configure()
    {
        Get("/remittance/customers/{customerPartyId:guid}/beneficiaries");
        Policies("AdminReadPolicy");
        this.RequiresPermission("Payment.Read");
        this.RequiresPermission("Customers.Read");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await service.ListForCustomerAsync(Route<Guid>("customerPartyId"), ct), ct);
    }
}
