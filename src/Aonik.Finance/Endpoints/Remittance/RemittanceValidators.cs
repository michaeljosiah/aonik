using Aonik.Finance.Contracts.Api.Remittance;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints.Remittance;

// ────────────────────────────────────────────────────────────────────
// Validators for the remittance request DTOs. FastEndpoints auto-discovers
// Validator<T> classes and runs them before HandleAsync, returning 400 with
// FluentValidation errors so bad input is rejected at the API boundary.
// ────────────────────────────────────────────────────────────────────

public sealed class RemittanceQuoteRequestValidator : Validator<RemittanceQuoteRequest>
{
    public RemittanceQuoteRequestValidator()
    {
        RuleFor(x => x.CustomerPartyId).RequiredId();

        RuleFor(x => x.OriginCountry).NotEmpty().Length(2);
        RuleFor(x => x.DestinationCountry).NotEmpty().Length(2);

        RuleFor(x => x.OriginCurrency).CurrencyCode();
        RuleFor(x => x.DestinationCurrency).CurrencyCode();

        RuleFor(x => x)
            .Must(r => r.OriginAmount.HasValue ^ r.DestinationAmount.HasValue)
            .WithName("amount")
            .WithMessage("Exactly one of originAmount or destinationAmount must be provided.");
        RuleFor(x => x.OriginAmount).GreaterThan(0).When(x => x.OriginAmount.HasValue);
        RuleFor(x => x.DestinationAmount).GreaterThan(0).When(x => x.DestinationAmount.HasValue);

        RuleFor(x => x.ServiceCode).OptionalText(64);
        RuleFor(x => x.CustomerTier).OptionalText(64);
        RuleFor(x => x.PurposeCode).OptionalText(64);
    }
}

public sealed class ConfirmRemittanceRequestValidator : Validator<ConfirmRemittanceRequest>
{
    public ConfirmRemittanceRequestValidator()
    {
        RuleFor(x => x.PricingQuoteId).RequiredId();
        RuleFor(x => x.CustomerPartyId).RequiredId();
        RuleFor(x => x.DestinationExternalAccountId).RequiredId();
        RuleFor(x => x.PurposeCode).RequiredText(64);
        RuleFor(x => x.Narration).OptionalText(256);
        RuleFor(x => x.ProviderCode).OptionalText(64);
    }
}
