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

public sealed class VerifyPayoutBeneficiaryRequestValidator : Validator<VerifyPayoutBeneficiaryRequest>
{
    public VerifyPayoutBeneficiaryRequestValidator()
    {
        RuleFor(x => x.CustomerPartyId).RequiredId();
        RuleFor(x => x.DestinationType)
            .RequiredText(64)
            .Must(BeSupportedRail)
            .WithMessage("Destination type must be Bank, MobileMoney, or Wallet.");
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.Country).CountryCode();
        RuleFor(x => x.AccountName).RequiredText(256);

        RuleFor(x => x.AccountNumber).OptionalText(64);
        RuleFor(x => x.BankCode).OptionalText(32);
        RuleFor(x => x.BranchCode).OptionalText(32);
        RuleFor(x => x.MobileNetwork).OptionalText(64);
        RuleFor(x => x.Msisdn).OptionalText(32);
        RuleFor(x => x.WalletId).OptionalText(128);
        RuleFor(x => x.ProviderCode).OptionalText(64);

        RuleFor(x => x.BeneficiaryPartyId).ValidIdWhenSupplied();
        RuleFor(x => x.BeneficiaryDisplayName).OptionalText(256);
        RuleFor(x => x.BeneficiaryPartyType).RequiredText(64);
        RuleFor(x => x.RelationshipTypeCode).RequiredText(64);
        RuleFor(x => x.Notes).OptionalText(1024);

        When(x => IsRail(x.DestinationType, "Bank"), () =>
        {
            RuleFor(x => x.AccountNumber).NotEmpty().WithMessage("Account number is required for bank beneficiaries.");
            RuleFor(x => x.BankCode).NotEmpty().WithMessage("Bank code is required for bank beneficiaries.");
        });

        When(x => IsRail(x.DestinationType, "MobileMoney"), () =>
        {
            RuleFor(x => x.Msisdn).NotEmpty().WithMessage("MSISDN is required for mobile-money beneficiaries.");
            RuleFor(x => x.MobileNetwork).NotEmpty().WithMessage("Mobile network is required for mobile-money beneficiaries.");
        });

        When(x => IsRail(x.DestinationType, "Wallet"), () =>
        {
            RuleFor(x => x.WalletId).NotEmpty().WithMessage("Wallet id is required for wallet beneficiaries.");
        });
    }

    private static bool BeSupportedRail(string? value)
        => IsRail(value, "Bank") || IsRail(value, "MobileMoney") || IsRail(value, "Wallet");

    private static bool IsRail(string? value, string rail)
        => string.Equals(value?.Trim(), rail, StringComparison.OrdinalIgnoreCase);
}
