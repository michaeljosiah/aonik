using Aonik.Authorization;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Endpoints.Payments;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints.Remittance;

public record RegisterOperatorBeneficiaryRequest(VerifyPayoutBeneficiaryRequest Beneficiary);

public sealed class RegisterOperatorBeneficiaryRequestValidator : Validator<RegisterOperatorBeneficiaryRequest>
{
    public RegisterOperatorBeneficiaryRequestValidator()
    {
        RuleFor(request => request.Beneficiary).NotNull().SetValidator(new VerifyPayoutBeneficiaryRequestValidator());
        When(request => request.Beneficiary is not null, () =>
        {
            RuleFor(request => request.Beneficiary.BeneficiaryPartyType).Must(value => value is "Person" or "Business");
            RuleFor(request => request.Beneficiary.MobileNetwork).OptionalText(50);
            // Raw inputs stay transient and must exceed the displayed last four characters.
            When(request => string.Equals(request.Beneficiary.DestinationType?.Trim(), "Bank", StringComparison.OrdinalIgnoreCase), () =>
                RuleFor(request => request.Beneficiary.AccountNumber).Must(value => value is not null && value.Trim().Length >= 5));
            When(request => string.Equals(request.Beneficiary.DestinationType?.Trim(), "MobileMoney", StringComparison.OrdinalIgnoreCase), () =>
                RuleFor(request => request.Beneficiary.Msisdn).Must(value => value is not null && value.Trim().Length >= 5));
            When(request => string.Equals(request.Beneficiary.DestinationType?.Trim(), "Wallet", StringComparison.OrdinalIgnoreCase), () =>
                RuleFor(request => request.Beneficiary.WalletId).Must(value => value is not null && value.Trim().Length >= 5));
        });
    }
}

/// <summary>Human-operated partner registration; no funds are moved.</summary>
public sealed class RegisterOperatorBeneficiaryEndpoint(IOperatorPayoutBeneficiaryService service)
    : Endpoint<RegisterOperatorBeneficiaryRequest, PayoutBeneficiaryResponse>
{
    public override void Configure()
    {
        Post("/remittance/beneficiaries/verify");
        Policies("AdminWritePolicy");
        this.RequiresPermission("Payment.Create");
        this.RequiresPermission("Customers.Write");
    }

    public override async Task HandleAsync(RegisterOperatorBeneficiaryRequest req, CancellationToken ct)
    {
        // Do not expose a provider exception that might echo the transient raw rail request.
        try
        {
            var response = await service.RegisterForCustomerAsync(req.Beneficiary, ct);
            await Send.CreatedAtAsync<ListOperatorBeneficiariesEndpoint>(
                routeValues: new { customerPartyId = response.CustomerPartyId }, responseBody: response, cancellation: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Aonik.SharedKernel.Abstractions.PermissionDeniedException) { throw; }
        catch (Aonik.SharedKernel.Abstractions.NotFoundException) { throw; }
        catch (Exception)
        {
            AddError("The destination could not be verified and saved. Check the details and the configured payout provider, then try again.");
            await Send.ErrorsAsync(422, ct);
        }
    }
}
