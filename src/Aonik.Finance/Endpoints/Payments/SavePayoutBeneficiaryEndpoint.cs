using Aonik.Finance.Contracts.Services.Payments;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Payments;

/// <summary>
/// POST /payments/payout-beneficiaries
/// Saves a payout destination for a customer and ensures the customer→recipient
/// relationship edge and the recipient's Beneficiary role exist (idempotently).
/// </summary>
public class SavePayoutBeneficiaryEndpoint : Endpoint<SavePayoutBeneficiaryRequest, PayoutBeneficiaryResponse>
{
    private readonly IPayoutBeneficiaryService _payoutBeneficiaryService;

    public SavePayoutBeneficiaryEndpoint(IPayoutBeneficiaryService payoutBeneficiaryService)
    {
        _payoutBeneficiaryService = payoutBeneficiaryService;
    }

    public override void Configure()
    {
        Post("/payments/payout-beneficiaries");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Save a payout beneficiary";
            s.Description = "Persists a saved payout destination for a customer, creating the recipient party (when not supplied), the customer→recipient relationship, and the recipient's Beneficiary role. Stores only a masked identifier plus the connector's reusable token — never the raw account number / MSISDN / wallet id.";
            s.Response(201, "Beneficiary saved successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(SavePayoutBeneficiaryRequest req, CancellationToken ct)
    {
        var response = await _payoutBeneficiaryService.SaveBeneficiaryAsync(req, ct);

        await Send.CreatedAtAsync<ListPayoutBeneficiariesEndpoint>(
            routeValues: new { customerPartyId = response.CustomerPartyId },
            responseBody: response,
            cancellation: ct);
    }
}
