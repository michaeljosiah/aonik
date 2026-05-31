using Aonik.Finance.Contracts.Services.Payments;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Payments;

/// <summary>
/// GET /payments/payout-beneficiaries/{customerPartyId}
/// Lists a customer's saved payout beneficiaries (recipient party + saved rails).
/// </summary>
public class ListPayoutBeneficiariesEndpoint : EndpointWithoutRequest<PayoutBeneficiaryListResponse>
{
    private readonly IPayoutBeneficiaryService _payoutBeneficiaryService;

    public ListPayoutBeneficiariesEndpoint(IPayoutBeneficiaryService payoutBeneficiaryService)
    {
        _payoutBeneficiaryService = payoutBeneficiaryService;
    }

    public override void Configure()
    {
        Get("/payments/payout-beneficiaries/{customerPartyId}");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List a customer's payout beneficiaries";
            s.Description = "Returns every saved payout destination owned by the customer, with the recipient party and ownership relationship.";
            s.Response(200, "Beneficiaries retrieved successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var customerPartyId = Route<Guid>("customerPartyId");
        var beneficiaries = await _payoutBeneficiaryService.ListBeneficiariesAsync(customerPartyId, ct);
        await Send.OkAsync(new PayoutBeneficiaryListResponse(customerPartyId, beneficiaries), ct);
    }
}
