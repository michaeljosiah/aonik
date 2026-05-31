using Aonik.Finance.Contracts.Services.Payments;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints.Payments;

// ────────────────────────────────────────────────────────────────────
// Validators for the payout-beneficiary request DTOs. FastEndpoints
// auto-discovers Validator<T> classes and runs them before HandleAsync,
// returning a 400 with FluentValidation errors so bad input is rejected
// at the API boundary rather than surfacing as a 500 deeper in the service.
// ────────────────────────────────────────────────────────────────────

public sealed class SavePayoutBeneficiaryRequestValidator : Validator<SavePayoutBeneficiaryRequest>
{
    public SavePayoutBeneficiaryRequestValidator()
    {
        RuleFor(x => x.CustomerPartyId).RequiredId();
        RuleFor(x => x.DestinationType).RequiredText(64);
        RuleFor(x => x.AccountName).RequiredText(256);
        RuleFor(x => x.Currency).CurrencyCode();

        // Only the masked identifier is ever accepted — never the raw account
        // number / MSISDN / wallet id (Spec 031 sensitive-data rule).
        RuleFor(x => x.MaskedAccountIdentifier).RequiredText(64);

        RuleFor(x => x.BankCode).OptionalText(32);
        RuleFor(x => x.BranchCode).OptionalText(32);
        RuleFor(x => x.MobileNetwork).OptionalText(64);
        RuleFor(x => x.ProviderBeneficiaryId).OptionalText(256);

        RuleFor(x => x.PartnerId).ValidIdWhenSupplied();
        RuleFor(x => x.ConnectorId).ValidIdWhenSupplied();

        // When an existing recipient party is supplied it must be a real id;
        // when omitted the service creates the party from the display details.
        RuleFor(x => x.BeneficiaryPartyId).ValidIdWhenSupplied();
        RuleFor(x => x.BeneficiaryDisplayName).OptionalText(256);
        RuleFor(x => x.BeneficiaryPartyType).RequiredText(64);
        RuleFor(x => x.RelationshipTypeCode).RequiredText(64);

        RuleFor(x => x.Notes).OptionalText(1024);
    }
}
