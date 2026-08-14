using Aonik.Finance.Contracts.Api.Ledger;
using Aonik.Finance.Contracts.Api.Orders;
using Aonik.Finance.Contracts.Api.Payments;
using Aonik.Finance.Contracts.Api.Pricing;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Models.Partners;
using Aonik.Finance.Endpoints.Billing;
using Aonik.Finance.Endpoints.Insights;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints;

// ────────────────────────────────────────────────────────────────────
// Validators for the Finance module's core feature DTOs (Ledger,
// Orders, Payments, Pricing, Catalog, Partners, Billing, Insights).
// The Accounts / bank-linking + PersonalFinance validators live in
// Aonik.PersonalFinance (Spec 027 S5, #118/#126).
// ────────────────────────────────────────────────────────────────────

// ── Ledger ──────────────────────────────────────────────────────────

public sealed class CreateLedgerRequestValidator : Validator<CreateLedgerRequest>
{
    public CreateLedgerRequestValidator()
    {
        RuleFor(x => x.BaseCurrency).CurrencyCode();
    }
}

public sealed class CreateLedgerAccountRequestValidator : Validator<CreateLedgerAccountRequest>
{
    public CreateLedgerAccountRequestValidator()
    {
        RuleFor(x => x.LedgerId).RequiredId();
        RuleFor(x => x.Name).RequiredText(256);
        RuleFor(x => x.Code).RequiredText(64);
        // AccountType is a free-form classification (Income, Asset, Liability,
        // etc.). The set of legal values is owned by the ledger service and
        // its catalog, not the API contract — keep validation to length only.
        RuleFor(x => x.AccountType).RequiredText(64);
    }
}

public sealed class AddJournalEntryRequestValidator : Validator<AddJournalEntryRequest>
{
    public AddJournalEntryRequestValidator()
    {
        RuleFor(x => x.LedgerId).RequiredId();
        RuleFor(x => x.Reference).MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(1024);
        RuleFor(x => x.Lines)
            .NotNull().WithMessage("Lines collection is required.")
            .Must(l => l != null && l.Count >= 2).WithMessage("Journal entry requires at least 2 lines (double-entry).")
            .Must(l => l == null || l.Count <= 200).WithMessage("Journal entry may have at most 200 lines.");
        RuleForEach(x => x.Lines).SetValidator(new AddJournalEntryLineRequestValidator());
    }
}

public sealed class AddJournalEntryLineRequestValidator : Validator<AddJournalEntryLineRequest>
{
    public AddJournalEntryLineRequestValidator()
    {
        RuleFor(x => x.AccountId).RequiredId();
        RuleFor(x => x.Direction)
            .NotEmpty()
            .Must(d => d is "Debit" or "Credit")
            .WithMessage("Direction must be 'Debit' or 'Credit'.");
        RuleFor(x => x.Amount).PositiveMoney();
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.Narration).MaximumLength(512);
    }
}

public sealed class ListLedgerAccountsRequestValidator : Validator<ListLedgerAccountsRequest>
{
    public ListLedgerAccountsRequestValidator() => RuleFor(x => x.LedgerId).ValidIdWhenSupplied();
}

public sealed class ListJournalEntriesRequestValidator : Validator<ListJournalEntriesRequest>
{
    public ListJournalEntriesRequestValidator() => RuleFor(x => x.LedgerId).ValidIdWhenSupplied();
}

// ── Orders ──────────────────────────────────────────────────────────

public sealed class CreateBillPaymentOrderRequestValidator : Validator<CreateBillPaymentOrderRequest>
{
    public CreateBillPaymentOrderRequestValidator()
    {
        RuleFor(x => x.PayerPartyId).RequiredId();
        RuleFor(x => x.OriginCountry).CountryCode();
        RuleFor(x => x.OriginCurrency).CurrencyCode();
        RuleFor(x => x.PurposeCode).MaximumLength(64);
        RuleFor(x => x.Notes).MaximumLength(2048);
        RuleFor(x => x.Items)
            .Must(i => i == null || i.Count <= 100)
            .WithMessage("Order may not contain more than 100 items.");
        RuleForEach(x => x.Items).SetValidator(new CreateBillPaymentItemRequestValidator()).When(x => x.Items != null);
    }
}

public sealed class CreateBillPaymentItemRequestValidator : Validator<CreateBillPaymentItemRequest>
{
    public CreateBillPaymentItemRequestValidator()
    {
        RuleFor(x => x.BillerId).RequiredId();
        RuleFor(x => x.ServiceId).RequiredId();
        RuleFor(x => x.ServiceCode).RequiredText(64);
        RuleFor(x => x.ServiceFieldValues)
            .NotNull().WithMessage("ServiceFieldValues is required (may be empty).")
            .Must(v => v == null || v.Count <= 100)
            .WithMessage("ServiceFieldValues may have at most 100 entries.");
        RuleFor(x => x.ReceiverPartyId).ValidIdWhenSupplied();
        RuleFor(x => x.NewReceiver).SetValidator(new CreateReceiverRequestValidator()!).When(x => x.NewReceiver != null);
        RuleFor(x => x.RelationshipTypeCode).MaximumLength(64);
        RuleFor(x => x.OriginAmount).PositiveMoney();
        RuleFor(x => x.DestinationAmount).PositiveMoney();
        RuleFor(x => x.DestinationCurrency).CurrencyCode();
        RuleFor(x => x.DestinationCountry).CountryCode();
        RuleFor(x => x.PricingQuoteId).RequiredId();
        RuleFor(x => x.PurposeCode).MaximumLength(64);
        RuleFor(x => x.Notes).MaximumLength(2048);
        RuleFor(x => x)
            .Must(x => x.OriginAmount.HasValue || x.DestinationAmount.HasValue)
            .WithMessage("Either OriginAmount or DestinationAmount must be supplied.");
    }
}

public sealed class CreateReceiverRequestValidator : Validator<CreateReceiverRequest>
{
    public CreateReceiverRequestValidator()
    {
        RuleFor(x => x.DisplayName).RequiredText(256);
        RuleFor(x => x.PartyType)
            .NotEmpty()
            .Must(t => t is "Person" or "Organization")
            .WithMessage("PartyType must be 'Person' or 'Organization'.");
        RuleFor(x => x.FirstName).MaximumLength(128);
        RuleFor(x => x.LastName).MaximumLength(128);
        RuleFor(x => x.Phone)
            .MaximumLength(32)
            .Matches(@"^\+[1-9]\d{7,14}$").WithMessage("Phone must be in E.164 format.")
            .When(x => !string.IsNullOrEmpty(x.Phone));
        RuleFor(x => x.Email!).Email().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.CountryCode)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
    }
}

public sealed class UpdateBillPaymentItemRequestValidator : Validator<UpdateBillPaymentItemRequest>
{
    public UpdateBillPaymentItemRequestValidator()
    {
        RuleFor(x => x.ServiceFieldValues)
            .Must(v => v == null || v.Count <= 100)
            .WithMessage("ServiceFieldValues may have at most 100 entries.");
        RuleFor(x => x.ReceiverPartyId).ValidIdWhenSupplied();
        RuleFor(x => x.RelationshipTypeCode).MaximumLength(64);
        RuleFor(x => x.OriginAmount).PositiveMoney();
        RuleFor(x => x.DestinationAmount).PositiveMoney();
        RuleFor(x => x.PricingQuoteId).ValidIdWhenSupplied();
        RuleFor(x => x.PurposeCode).MaximumLength(64);
        RuleFor(x => x.Notes).MaximumLength(2048);
    }
}

public sealed class CancelOrderRequestValidator : Validator<CancelOrderRequest>
{
    public CancelOrderRequestValidator() => RuleFor(x => x.Reason).MaximumLength(2048);
}

public sealed class ListOrdersRequestValidator : Validator<ListOrdersRequest>
{
    public ListOrdersRequestValidator()
    {
        RuleFor(x => x.PageNumber).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 200);
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.OrderType).MaximumLength(64);
        RuleFor(x => x.Search).MaximumLength(256);
        RuleFor(x => x.PayerPartyId).ValidIdWhenSupplied();
    }
}

public sealed class CreateGuestBillPaymentDraftRequestValidator : Validator<CreateGuestBillPaymentDraftRequest>
{
    public CreateGuestBillPaymentDraftRequestValidator()
    {
        RuleFor(x => x.BillerId).RequiredId();
        RuleFor(x => x.ServiceId).RequiredId();
        RuleFor(x => x.ServiceCode).RequiredText(64);
        RuleFor(x => x.ServiceName).RequiredText(256);
        RuleFor(x => x.BillerName).MaximumLength(256);
        RuleFor(x => x.CountryCode).CountryCode();
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.ServiceFieldValues)
            .NotNull().WithMessage("ServiceFieldValues is required (may be empty).")
            .Must(v => v == null || v.Count <= 100)
            .WithMessage("ServiceFieldValues may have at most 100 entries.");
        RuleFor(x => x.ValidationMode).MaximumLength(64);
        RuleFor(x => x.AccountHolderName).MaximumLength(256);
        RuleFor(x => x.RequestedAmount).PositiveMoney();
        RuleFor(x => x.Channel).MaximumLength(64);
    }
}

// ── Payments ────────────────────────────────────────────────────────

public sealed class CreatePaymentIntentRequestValidator : Validator<CreatePaymentIntentRequest>
{
    public CreatePaymentIntentRequestValidator()
    {
        RuleFor(x => x.Amount).PositiveMoney();
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.Reference).RequiredText(128);
        RuleFor(x => x.OrderId).RequiredId();
        RuleFor(x => x.InvoiceId).ValidIdWhenSupplied();
        RuleFor(x => x.PaymentMethodType)
            .MaximumLength(50)
            .When(x => x.PaymentMethodType != null);
    }
}

public sealed class CreatePublicPaymentIntentRequestValidator : Validator<CreatePublicPaymentIntentRequest>
{
    public CreatePublicPaymentIntentRequestValidator()
    {
        RuleFor(x => x.OrderId).RequiredId();
        RuleFor(x => x.Provider).RequiredText(64);
        RuleFor(x => x.PaymentMethodType).RequiredText(64);
        RuleFor(x => x.ReturnUrl).MaximumLength(2048);
        RuleFor(x => x.CancelUrl).MaximumLength(2048);
    }
}

// ── Pricing ─────────────────────────────────────────────────────────

public sealed class PricingQuoteRequestValidator : Validator<PricingQuoteRequest>
{
    public PricingQuoteRequestValidator()
    {
        RuleFor(x => x.OriginCurrency).CurrencyCode();
        RuleFor(x => x.DestinationCurrency).CurrencyCode();
        RuleFor(x => x.OriginCountry).CountryCode();
        RuleFor(x => x.DestinationCountry).CountryCode();
        RuleFor(x => x.ServiceCode).RequiredText(64);
        RuleFor(x => x.DestinationAmount).PositiveMoney();
        RuleFor(x => x.OriginAmount).PositiveMoney();
        RuleFor(x => x.CustomerId).ValidIdWhenSupplied();
        RuleFor(x => x.CustomerTier).MaximumLength(64);
        RuleFor(x => x.QuoteContext).MaximumLength(128);
        RuleFor(x => x)
            .Must(x => x.OriginAmount.HasValue || x.DestinationAmount.HasValue)
            .WithMessage("Either OriginAmount or DestinationAmount must be supplied.");
    }
}

public sealed class CreateFxQuoteRequestValidator : Validator<CreateFxQuoteRequest>
{
    public CreateFxQuoteRequestValidator()
    {
        RuleFor(x => x.BaseCurrency).CurrencyCode();
        RuleFor(x => x.TargetCurrency).CurrencyCode();
        RuleFor(x => x.Rate).PositiveMoney();
        RuleFor(x => x.ExpiresAt)
            .GreaterThan(DateTime.UtcNow.AddDays(-1))
            .WithMessage("ExpiresAt must be in the future or recent.");
        RuleFor(x => x.Provider).MaximumLength(128);
        RuleFor(x => x.MetadataJson).MaximumLength(16_000);
    }
}

public sealed class UpdateFxQuoteRequestValidator : Validator<UpdateFxQuoteRequest>
{
    public UpdateFxQuoteRequestValidator()
    {
        RuleFor(x => x.Rate).PositiveMoney();
        RuleFor(x => x.Provider).MaximumLength(128);
        RuleFor(x => x.MetadataJson).MaximumLength(16_000);
    }
}

// ── Billing list ────────────────────────────────────────────────────

internal sealed class ListInvoicesRequestValidator : Validator<ListInvoicesRequest>
{
    public ListInvoicesRequestValidator() => RuleFor(x => x.Status).MaximumLength(64);
}

// ── Insights ────────────────────────────────────────────────────────

public sealed class GetMySpaceSummaryRequestValidator : Validator<GetMySpaceSummaryRequest>
{
    public GetMySpaceSummaryRequestValidator()
    {
        RuleFor(x => x.Currency)
            .Length(3).Matches("^[A-Z]{3}$").WithMessage("Currency must be 3 uppercase letters (ISO-4217).")
            .When(x => !string.IsNullOrEmpty(x.Currency));
    }
}

// ── Catalog ─────────────────────────────────────────────────────────

public sealed class CatalogBillerListRequestValidator : Validator<CatalogBillerListRequest>
{
    public CatalogBillerListRequestValidator()
    {
        RuleFor(x => x.CountryCode)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
        RuleFor(x => x.CategoryId).ValidIdWhenSupplied();
        RuleFor(x => x.Search).MaximumLength(256);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PageSize).InclusiveBetween(0, 200);
    }
}

public sealed class CatalogCategoryListRequestValidator : Validator<CatalogCategoryListRequest>
{
    public CatalogCategoryListRequestValidator()
    {
        RuleFor(x => x.CountryCode)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
    }
}

public sealed class CatalogServiceFieldValidationRequestValidator : Validator<CatalogServiceFieldValidationRequest>
{
    public CatalogServiceFieldValidationRequestValidator()
    {
        RuleFor(x => x.FieldValues)
            .NotNull().WithMessage("FieldValues is required.")
            .Must(v => v != null && v.Count <= 100)
            .WithMessage("FieldValues may have at most 100 entries.");
    }
}

public sealed class CreateCatalogBillerCategoryRequestValidator : Validator<CreateCatalogBillerCategoryRequest>
{
    public CreateCatalogBillerCategoryRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(256);
        RuleFor(x => x.CountryCode).CountryCode();
        RuleFor(x => x.Description).MaximumLength(2048);
        RuleFor(x => x.IconUrl).MaximumLength(2048);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 1_000_000);
    }
}

public sealed class UpdateCatalogBillerCategoryRequestValidator : Validator<UpdateCatalogBillerCategoryRequest>
{
    public UpdateCatalogBillerCategoryRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(256);
        RuleFor(x => x.Description).MaximumLength(2048);
        RuleFor(x => x.IconUrl).MaximumLength(2048);
        RuleFor(x => x.SortOrder)
            .InclusiveBetween(0, 1_000_000)
            .When(x => x.SortOrder.HasValue);
    }
}

public sealed class CreateCatalogBillerRequestValidator : Validator<CreateCatalogBillerRequest>
{
    public CreateCatalogBillerRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(256);
        RuleFor(x => x.CountryCode).CountryCode();
        RuleFor(x => x.CategoryId).RequiredId();
        RuleFor(x => x.CorrespondentPartnerId).ValidIdWhenSupplied();
        RuleFor(x => x.Description).MaximumLength(2048);
        RuleFor(x => x.LogoUrl).MaximumLength(2048);
        RuleFor(x => x.BannerUrl).MaximumLength(2048);
        RuleFor(x => x.SupportPhone)
            .MaximumLength(32)
            .When(x => !string.IsNullOrEmpty(x.SupportPhone));
        RuleFor(x => x.SupportEmail!).Email().When(x => !string.IsNullOrEmpty(x.SupportEmail));
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 1_000_000);
    }
}

public sealed class UpdateCatalogBillerRequestValidator : Validator<UpdateCatalogBillerRequest>
{
    public UpdateCatalogBillerRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(256);
        RuleFor(x => x.CategoryId).ValidIdWhenSupplied();
        RuleFor(x => x.CorrespondentPartnerId).ValidIdWhenSupplied();
        RuleFor(x => x.Description).MaximumLength(2048);
        RuleFor(x => x.LogoUrl).MaximumLength(2048);
        RuleFor(x => x.BannerUrl).MaximumLength(2048);
        RuleFor(x => x.SupportPhone)
            .MaximumLength(32)
            .When(x => !string.IsNullOrEmpty(x.SupportPhone));
        RuleFor(x => x.SupportEmail!).Email().When(x => !string.IsNullOrEmpty(x.SupportEmail));
        RuleFor(x => x.SortOrder)
            .InclusiveBetween(0, 1_000_000)
            .When(x => x.SortOrder.HasValue);
    }
}

// ── Partners ────────────────────────────────────────────────────────

public sealed class ListPartnersRequestValidator : Validator<ListPartnersRequest>
{
    public ListPartnersRequestValidator()
    {
        RuleFor(x => x.PageNumber).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 200);
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.CountryCode)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
        RuleFor(x => x.Search).MaximumLength(256);
    }
}

public sealed class CreatePartnerRequestValidator : Validator<CreatePartnerRequest>
{
    public CreatePartnerRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(256);
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.CapabilitiesJson).MaximumLength(16_000);
        RuleFor(x => x.OperatingHoursJson).MaximumLength(16_000);
    }
}

public sealed class UpdatePartnerRequestValidator : Validator<UpdatePartnerRequest>
{
    public UpdatePartnerRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(256);
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.CapabilitiesJson).MaximumLength(16_000);
        RuleFor(x => x.OperatingHoursJson).MaximumLength(16_000);
    }
}

public sealed class CreatePartnerConnectorRequestValidator : Validator<CreatePartnerConnectorRequest>
{
    public CreatePartnerConnectorRequestValidator()
    {
        RuleFor(x => x.ConnectorType).RequiredText(128);
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.CredentialsRef).MaximumLength(512);
        RuleFor(x => x.ConfigJson).MaximumLength(16_000);
    }
}

public sealed class UpdatePartnerConnectorRequestValidator : Validator<UpdatePartnerConnectorRequest>
{
    public UpdatePartnerConnectorRequestValidator()
    {
        RuleFor(x => x.ConnectorType).MaximumLength(128);
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.CredentialsRef).MaximumLength(512);
        RuleFor(x => x.ConfigJson).MaximumLength(16_000);
    }
}
