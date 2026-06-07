using Aonik.Finance.Contracts.Services.Payments;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Payments;

/// <summary>
/// POST /payments/payout-beneficiaries/verify
/// Verifies and registers a payout beneficiary with the selected partner, then stores only masked rails.
/// </summary>
public class VerifyPayoutBeneficiaryEndpoint
    : Endpoint<VerifyPayoutBeneficiaryRequest, PayoutBeneficiaryResponse>
{
    private readonly IPayoutBeneficiaryService _payoutBeneficiaryService;

    public VerifyPayoutBeneficiaryEndpoint(IPayoutBeneficiaryService payoutBeneficiaryService)
    {
        _payoutBeneficiaryService = payoutBeneficiaryService;
    }

    public override void Configure()
    {
        Post("/payments/payout-beneficiaries/verify");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Verify and register a payout beneficiary";
            s.Description = "Accepts raw rail details transiently, registers a provider recipient, and persists a verified saved destination with only masked rail data and the provider token.";
            s.Response(201, "Beneficiary verified and saved successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(VerifyPayoutBeneficiaryRequest req, CancellationToken ct)
    {
        var response = await _payoutBeneficiaryService.VerifyAndRegisterBeneficiaryAsync(req, ct);

        await Send.CreatedAtAsync<ListPayoutBeneficiariesEndpoint>(
            routeValues: new { customerPartyId = response.CustomerPartyId },
            responseBody: response,
            cancellation: ct);
    }
}
