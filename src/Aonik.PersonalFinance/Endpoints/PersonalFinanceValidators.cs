using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints;

// ────────────────────────────────────────────────────────────────────
// Validators for the Finance.PersonalFinance feature DTOs (the
// Payabo-facing personal finance model). Internal-visibility endpoint
// request DTOs that live next to their endpoint files are validated in
// PersonalFinanceEndpointsValidators.cs.
// ────────────────────────────────────────────────────────────────────

// ── Household ───────────────────────────────────────────────────────

public sealed class CreateHouseholdRequestValidator : Validator<CreateHouseholdRequest>
{
    public CreateHouseholdRequestValidator() => RuleFor(x => x.Name).RequiredText(128);
}

public sealed class TransferOwnershipRequestValidator : Validator<TransferOwnershipRequest>
{
    public TransferOwnershipRequestValidator() => RuleFor(x => x.NewOwnerUserId).RequiredId();
}

public sealed class ShareAccountWithHouseholdRequestValidator : Validator<ShareAccountWithHouseholdRequest>
{
    public ShareAccountWithHouseholdRequestValidator() => RuleFor(x => x.HouseholdId).RequiredId();
}

// ── Personal Account ────────────────────────────────────────────────

public sealed class CreatePersonalAccountRequestValidator : Validator<CreatePersonalAccountRequest>
{
    public CreatePersonalAccountRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(256);
        RuleFor(x => x.AccountType).RequiredText(64);
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.InstitutionName).MaximumLength(256);
        RuleFor(x => x.ExternalReference).MaximumLength(256);
        RuleFor(x => x.AccountSubtype).MaximumLength(64);
        RuleFor(x => x.Last4)
            .Length(4).Matches("^[0-9]{4}$").WithMessage("Last4 must be 4 digits.")
            .When(x => !string.IsNullOrEmpty(x.Last4));
        RuleFor(x => x.StartingBalance)
            .Must(b => !b.HasValue || (b.Value >= -1_000_000_000m && b.Value <= 1_000_000_000m))
            .WithMessage("Starting balance is out of supported range.");
    }
}

public sealed class UpdatePersonalAccountRequestValidator : Validator<UpdatePersonalAccountRequest>
{
    public UpdatePersonalAccountRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(256);
        RuleFor(x => x.AccountType).RequiredText(64);
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.InstitutionName).MaximumLength(256);
        RuleFor(x => x.ExternalReference).MaximumLength(256);
        RuleFor(x => x.AccountSubtype).MaximumLength(64);
        RuleFor(x => x.Last4)
            .Length(4).Matches("^[0-9]{4}$").WithMessage("Last4 must be 4 digits.")
            .When(x => !string.IsNullOrEmpty(x.Last4));
        RuleFor(x => x.Status).RequiredText(64);
        RuleFor(x => x.CurrentBalance)
            .Must(b => !b.HasValue || (b.Value >= -1_000_000_000m && b.Value <= 1_000_000_000m))
            .WithMessage("CurrentBalance is out of supported range.");
    }
}

// ── Account Link ────────────────────────────────────────────────────

public sealed class PersonalCreateAccountLinkSessionRequestValidator : Validator<CreateAccountLinkSessionRequest>
{
    public PersonalCreateAccountLinkSessionRequestValidator()
    {
        RuleFor(x => x.Provider).RequiredText(64);
        RuleFor(x => x.Mode)
            .NotEmpty()
            .Must(m => m is "connect" or "update" or "reauth")
            .WithMessage("Mode must be one of: connect, update, reauth.");
        RuleFor(x => x.ConnectionId).ValidIdWhenSupplied();
        RuleFor(x => x.AndroidPackageName).MaximumLength(256);
        RuleFor(x => x.RedirectUri).MaximumLength(2048);
        RuleFor(x => x.CountryCode)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
        RuleFor(x => x.ClientName).MaximumLength(128);
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+[1-9]\d{7,14}$").WithMessage("PhoneNumber must be in E.164 format.")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}

public sealed class PersonalExchangeAccountLinkSessionRequestValidator : Validator<ExchangeAccountLinkSessionRequest>
{
    public PersonalExchangeAccountLinkSessionRequestValidator()
    {
        RuleFor(x => x.AccountLinkSessionId).RequiredId();
        RuleFor(x => x.TemporaryCode).RequiredText(2048);
    }
}

public sealed class PlaidAccountLinkWebhookRequestValidator : Validator<PlaidAccountLinkWebhookRequest>
{
    public PlaidAccountLinkWebhookRequestValidator()
    {
        RuleFor(x => x.WebhookType).RequiredText(64);
        RuleFor(x => x.WebhookCode).RequiredText(64);
        RuleFor(x => x.ItemId).MaximumLength(256);
        RuleFor(x => x.Environment).MaximumLength(64);
    }
}

// ── Personal Transactions ───────────────────────────────────────────

public sealed class CreateManualPersonalTransactionRequestValidator : Validator<CreateManualPersonalTransactionRequest>
{
    public CreateManualPersonalTransactionRequestValidator()
    {
        RuleFor(x => x.PersonalAccountId).ValidIdWhenSupplied();
        RuleFor(x => x.OccurredAt)
            .GreaterThan(DateTime.UtcNow.AddYears(-50))
            .LessThan(DateTime.UtcNow.AddYears(1));
        RuleFor(x => x.Amount)
            .Must(a => Math.Abs(a) <= 1_000_000_000m)
            .WithMessage("Amount exceeds maximum supported value.");
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.Merchant).MaximumLength(256);
        RuleFor(x => x.Description).MaximumLength(1024);
        RuleFor(x => x.Category).MaximumLength(64);
        RuleFor(x => x.Notes).MaximumLength(2048);
        RuleFor(x => x.Tags)
            .Must(t => t == null || t.Count <= 30).WithMessage("Tags may include at most 30 entries.");
    }
}

public sealed class UpdateManualPersonalTransactionRequestValidator : Validator<UpdateManualPersonalTransactionRequest>
{
    public UpdateManualPersonalTransactionRequestValidator()
    {
        // This DTO is bound by a PATCH endpoint that accepts partial updates,
        // so absent fields deserialize to defaults (Currency=null, Amount=0,
        // OccurredAt=default(DateTime)). Validation rules therefore only fire
        // when the field appears non-default — strict checks happen at the
        // service layer where the merge logic knows what was actually
        // supplied.
        RuleFor(x => x.PersonalAccountId).ValidIdWhenSupplied();
        RuleFor(x => x.Amount)
            .Must(a => Math.Abs(a) <= 1_000_000_000m)
            .WithMessage("Amount exceeds maximum supported value.");
        RuleFor(x => x.Currency)
            .Length(3).Matches("^[A-Za-z]{3}$").WithMessage("Currency code must be 3 letters.")
            .When(x => !string.IsNullOrEmpty(x.Currency));
        RuleFor(x => x.Merchant).MaximumLength(256);
        RuleFor(x => x.Description).MaximumLength(1024);
        RuleFor(x => x.Category).MaximumLength(64);
        RuleFor(x => x.Notes).MaximumLength(2048);
        RuleFor(x => x.Tags)
            .Must(t => t == null || t.Count <= 30).WithMessage("Tags may include at most 30 entries.");
    }
}

public sealed class ListPersonalTransactionsRequestValidator : Validator<ListPersonalTransactionsRequest>
{
    public ListPersonalTransactionsRequestValidator()
    {
        RuleFor(x => x.PersonalAccountId).ValidIdWhenSupplied();
        RuleFor(x => x.FinancialContextId).ValidIdWhenSupplied();
        RuleFor(x => x.Category).MaximumLength(64);
        RuleFor(x => x.Search).MaximumLength(256);
        RuleFor(x => x.Page).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 500);
    }
}

// ── Categorisation Rules ────────────────────────────────────────────

public sealed class CreateCategorisationRuleRequestValidator : Validator<CreateCategorisationRuleRequest>
{
    public CreateCategorisationRuleRequestValidator()
    {
        RuleFor(x => x.Pattern).RequiredText(512);
        RuleFor(x => x.Category).RequiredText(64);
        RuleFor(x => x.SubCategory).MaximumLength(64);
        RuleFor(x => x.Priority).InclusiveBetween(0, 1_000_000);
        RuleFor(x => x.MatchType)
            .NotEmpty()
            .Must(m => PersonalFinanceValidatorConstants.MatchTypes.Contains(m))
            .WithMessage($"MatchType must be one of: {string.Join(", ", PersonalFinanceValidatorConstants.MatchTypes)}.");
        RuleFor(x => x.MinAmount).NonNegativeMoney();
        RuleFor(x => x.MaxAmount).NonNegativeMoney();
        RuleFor(x => x.AppliesToAccountId).ValidIdWhenSupplied();
        RuleFor(x => x.Scope)
            .NotEmpty()
            .Must(s => PersonalFinanceValidatorConstants.RuleScopes.Contains(s))
            .WithMessage($"Scope must be one of: {string.Join(", ", PersonalFinanceValidatorConstants.RuleScopes)}.");
    }
}

public sealed class UpdateCategorisationRuleRequestValidator : Validator<UpdateCategorisationRuleRequest>
{
    public UpdateCategorisationRuleRequestValidator()
    {
        RuleFor(x => x.Pattern).RequiredText(512);
        RuleFor(x => x.Category).RequiredText(64);
        RuleFor(x => x.SubCategory).MaximumLength(64);
        RuleFor(x => x.Priority).InclusiveBetween(0, 1_000_000);
        RuleFor(x => x.MatchType)
            .NotEmpty()
            .Must(m => PersonalFinanceValidatorConstants.MatchTypes.Contains(m))
            .WithMessage($"MatchType must be one of: {string.Join(", ", PersonalFinanceValidatorConstants.MatchTypes)}.");
        RuleFor(x => x.MinAmount).NonNegativeMoney();
        RuleFor(x => x.MaxAmount).NonNegativeMoney();
        RuleFor(x => x.AppliesToAccountId).ValidIdWhenSupplied();
        RuleFor(x => x.Scope)
            .NotEmpty()
            .Must(s => PersonalFinanceValidatorConstants.RuleScopes.Contains(s))
            .WithMessage($"Scope must be one of: {string.Join(", ", PersonalFinanceValidatorConstants.RuleScopes)}.");
        RuleFor(x => x.ApprovalStatus)
            .NotEmpty()
            .Must(s => PersonalFinanceValidatorConstants.ApprovalStatuses.Contains(s))
            .WithMessage($"ApprovalStatus must be one of: {string.Join(", ", PersonalFinanceValidatorConstants.ApprovalStatuses)}.");
    }
}

public sealed class ClassificationReviewQueueRequestValidator : Validator<ClassificationReviewQueueRequest>
{
    public ClassificationReviewQueueRequestValidator()
    {
        RuleFor(x => x.PersonalAccountId).ValidIdWhenSupplied();
        RuleFor(x => x.Page).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 500);
    }
}

public sealed class OverrideTransactionClassificationRequestValidator : Validator<OverrideTransactionClassificationRequest>
{
    public OverrideTransactionClassificationRequestValidator()
    {
        RuleFor(x => x.Category).RequiredText(64);
        RuleFor(x => x.Notes).MaximumLength(2048);
        RuleFor(x => x.RulePattern).MaximumLength(512);
        RuleFor(x => x.RulePriority).InclusiveBetween(0, 1_000_000);
        // RuleMatchType nullability/allowed-values are enforced by the
        // service so it can return a domain-specific error response when
        // CreateRuleFromCorrection is true but RuleMatchType is missing.
        RuleFor(x => x.RuleMatchType).MaximumLength(32);
    }
}

// ── Spending Narrative ──────────────────────────────────────────────

public sealed class GeneratePersonalSpendingNarrativeRequestValidator : Validator<GeneratePersonalSpendingNarrativeRequest>
{
    public GeneratePersonalSpendingNarrativeRequestValidator()
    {
        RuleFor(x => x.PeriodStart)
            .GreaterThan(DateTime.UtcNow.AddYears(-5))
            .LessThan(DateTime.UtcNow.AddDays(1));
        RuleFor(x => x.PeriodEnd)
            .GreaterThan(DateTime.UtcNow.AddYears(-5))
            .LessThan(DateTime.UtcNow.AddDays(1))
            .GreaterThanOrEqualTo(x => x.PeriodStart)
            .WithMessage("PeriodEnd must be on or after PeriodStart.");
        RuleFor(x => x.PersonalAccountId).ValidIdWhenSupplied();
    }
}

// ── Financial Life Graph ────────────────────────────────────────────

public sealed class CreateFinancialLifeGraphNodeRequestValidator : Validator<CreateFinancialLifeGraphNodeRequest>
{
    public CreateFinancialLifeGraphNodeRequestValidator()
    {
        RuleFor(x => x.NodeType).RequiredText(64);
        RuleFor(x => x.DisplayName).RequiredText(256);
        RuleFor(x => x.MetadataJson).MaximumLength(64_000);
        RuleFor(x => x.SourceEntity).MaximumLength(64);
        RuleFor(x => x.SourceId).ValidIdWhenSupplied();
        RuleFor(x => x.HouseholdId).ValidIdWhenSupplied();
        RuleFor(x => x.AiRunId).ValidIdWhenSupplied();
    }
}

public sealed class CreateFinancialLifeGraphEdgeRequestValidator : Validator<CreateFinancialLifeGraphEdgeRequest>
{
    public CreateFinancialLifeGraphEdgeRequestValidator()
    {
        RuleFor(x => x.FromNodeKey).RequiredText(128);
        RuleFor(x => x.Predicate).RequiredText(64);
        RuleFor(x => x.ToNodeKey).RequiredText(128);
        RuleFor(x => x.MetadataJson).MaximumLength(64_000);
        RuleFor(x => x.HouseholdId).ValidIdWhenSupplied();
        RuleFor(x => x.AiRunId).ValidIdWhenSupplied();
    }
}

public sealed class ProposeRecurringMerchantGraphAnnotationsRequestValidator : Validator<ProposeRecurringMerchantGraphAnnotationsRequest>
{
    public ProposeRecurringMerchantGraphAnnotationsRequestValidator()
    {
        RuleFor(x => x.AiRunId).RequiredId();
        RuleFor(x => x.MinOccurrences).InclusiveBetween(1, 10_000);
        RuleFor(x => x.WithinDays).InclusiveBetween(1, 3650);
    }
}

public sealed class RejectFinancialLifeGraphProposalRequestValidator : Validator<RejectFinancialLifeGraphProposalRequest>
{
    public RejectFinancialLifeGraphProposalRequestValidator()
        => RuleFor(x => x.Reason).MaximumLength(2048);
}

// ── Financial Context (Spaces) ──────────────────────────────────────

public sealed class CreateFinancialContextRequestValidator : Validator<CreateFinancialContextRequest>
{
    public CreateFinancialContextRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(256);
        RuleFor(x => x.ContextType).RequiredText(64);
        RuleFor(x => x.RelatedPartyId).ValidIdWhenSupplied();
        RuleFor(x => x.Notes).MaximumLength(2048);
        RuleFor(x => x.MetadataJson).MaximumLength(16_000);
    }
}

public sealed class UpdateFinancialContextRequestValidator : Validator<UpdateFinancialContextRequest>
{
    public UpdateFinancialContextRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(256);
        RuleFor(x => x.ContextType).RequiredText(64);
        RuleFor(x => x.RelatedPartyId).ValidIdWhenSupplied();
        RuleFor(x => x.Notes).MaximumLength(2048);
        RuleFor(x => x.MetadataJson).MaximumLength(16_000);
    }
}

public sealed class AddFundingSourceRequestValidator : Validator<AddFundingSourceRequest>
{
    public AddFundingSourceRequestValidator()
    {
        RuleFor(x => x.PersonalAccountId).RequiredId();
    }
}

public sealed class AssignTransactionContextRequestValidator : Validator<AssignTransactionContextRequest>
{
    public AssignTransactionContextRequestValidator()
        => RuleFor(x => x.FinancialContextId).ValidIdWhenSupplied();
}

// ── Bills ───────────────────────────────────────────────────────────

public sealed class CreateBillRequestValidator : Validator<CreateBillRequest>
{
    public CreateBillRequestValidator()
    {
        RuleFor(x => x.Payee).RequiredText(256);
        RuleFor(x => x.Frequency).RequiredText(32);
        RuleFor(x => x.NextDueDate)
            .GreaterThan(DateTime.UtcNow.AddYears(-1))
            .LessThan(DateTime.UtcNow.AddYears(10));
        RuleFor(x => x.ExpectedAmount).PositiveMoney();
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.PaidFromAccountId).ValidIdWhenSupplied();
    }
}

// ── Budgets ─────────────────────────────────────────────────────────

// ── CareEntities (Spec 043) ─────────────────────────────────────────

public sealed class CreateCareEntityRequestValidator : Validator<CreateCareEntityRequest>
{
    public CreateCareEntityRequestValidator()
    {
        RuleFor(x => x.Kind)
            .NotEmpty()
            .Must(k => k is "person" or "asset")
            .WithMessage("Kind must be 'person' or 'asset'.");
        RuleFor(x => x.Name).RequiredText(120);
        RuleFor(x => x.CountryCode).CountryCode();
        RuleFor(x => x.AssetType)
            .NotEmpty().When(x => x.Kind == "asset")
            .WithMessage("An asset must have an assetType.");
        RuleFor(x => x.AssetType)
            .Empty().When(x => x.Kind == "person")
            .WithMessage("A person cannot have an assetType.");
        RuleFor(x => x.AssetType).MaximumLength(32);
        RuleFor(x => x.Relationship).MaximumLength(80);
        RuleFor(x => x.Emoji).MaximumLength(16);
        RuleFor(x => x.PhotoDocumentId).ValidIdWhenSupplied();
        RuleFor(x => x.Attributes)
            .Must(a => a == null || a.Count <= 50)
            .WithMessage("Attributes may include at most 50 entries.");
    }
}

// ── PaymentLogs (Spec 045) ──────────────────────────────────────────

public sealed class CreatePaymentLogRequestValidator : Validator<CreatePaymentLogRequest>
{
    private static readonly string[] Channels = ["bank", "wise", "cash", "other"];
    private static readonly string[] Origins =
        ["manual", "captureImage", "captureText", "captureVoice", "markDone", "plaidDetected"];

    public CreatePaymentLogRequestValidator()
    {
        RuleFor(x => x.CareEntityId).RequiredId();
        RuleFor(x => x.CommitmentId).ValidIdWhenSupplied();
        RuleFor(x => x.CommitmentCycleId).ValidIdWhenSupplied();
        RuleFor(x => x.Amount).PositiveMoney();
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.ApproxGbp).NonNegativeMoney();
        RuleFor(x => x.Channel)
            .NotEmpty()
            .Must(c => Channels.Contains(c))
            .WithMessage($"Channel must be one of: {string.Join(", ", Channels)}.");
        RuleFor(x => x.Origin)
            .NotEmpty()
            .Must(o => Origins.Contains(o))
            .WithMessage($"Origin must be one of: {string.Join(", ", Origins)}.");
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public sealed class CreateBudgetRequestValidator : Validator<CreateBudgetRequest>
{
    public CreateBudgetRequestValidator() => RuleFor(x => x.CategoryId).MaximumLength(128);
}

public sealed class UpdateBudgetAmountRequestValidator : Validator<UpdateBudgetAmountRequest>
{
    public UpdateBudgetAmountRequestValidator() => RuleFor(x => x.TotalAllocated).NonNegativeMoney();
}

// ── Commitments ─────────────────────────────────────────────────────

public sealed class CreateCommitmentFromTransactionRequestValidator : Validator<CreateCommitmentFromTransactionRequest>
{
    public CreateCommitmentFromTransactionRequestValidator()
    {
        RuleFor(x => x.TransactionId).RequiredId();
        RuleFor(x => x.CommitmentType).RequiredText(64);
        RuleFor(x => x.DisplayName).RequiredText(256);
        RuleFor(x => x.Frequency).RequiredText(32);
        RuleFor(x => x.NextDueDate)
            .GreaterThan(DateTime.UtcNow.AddYears(-1))
            .LessThan(DateTime.UtcNow.AddYears(10));
        RuleFor(x => x.ExpectedAmount).PositiveMoney();
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.PaidFromAccountId).ValidIdWhenSupplied();
        RuleFor(x => x.Notes).MaximumLength(2048);
        RuleFor(x => x.DebtType).MaximumLength(64);
        RuleFor(x => x.AccountReference).MaximumLength(256);
    }
}

// ── Shared constants ────────────────────────────────────────────────

// ── Support commitments (Spec 044) ──────────────────────────────────

public sealed class CreateSupportCommitmentRequestValidator : Validator<CreateSupportCommitmentRequest>
{
    public CreateSupportCommitmentRequestValidator()
    {
        RuleFor(x => x.CareEntityId).RequiredId();
        RuleFor(x => x.DisplayName).RequiredText(256);
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.ExpectedAmount).PositiveMoney();
        RuleFor(x => x.RhythmUnit)
            .NotEmpty()
            .Must(u => PersonalFinanceValidatorConstants.RhythmUnits.Any(r => string.Equals(r, u, StringComparison.OrdinalIgnoreCase)))
            .WithMessage($"RhythmUnit must be one of: {string.Join(", ", PersonalFinanceValidatorConstants.RhythmUnits)}.");
        RuleFor(x => x.RhythmInterval).InclusiveBetween(1, 365);
        RuleFor(x => x.AnchorDay).InclusiveBetween(1, 31).When(x => x.AnchorDay.HasValue);
        RuleFor(x => x.ReminderDaysBefore).InclusiveBetween(0, 365).When(x => x.ReminderDaysBefore.HasValue);
        RuleFor(x => x.PaidFromAccountId).ValidIdWhenSupplied();
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class UpdateSupportCommitmentRequestValidator : Validator<UpdateSupportCommitmentRequest>
{
    public UpdateSupportCommitmentRequestValidator()
    {
        RuleFor(x => x.DisplayName).RequiredText(256);
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.ExpectedAmount).PositiveMoney();
        RuleFor(x => x.RhythmUnit)
            .NotEmpty()
            .Must(u => PersonalFinanceValidatorConstants.RhythmUnits.Any(r => string.Equals(r, u, StringComparison.OrdinalIgnoreCase)))
            .WithMessage($"RhythmUnit must be one of: {string.Join(", ", PersonalFinanceValidatorConstants.RhythmUnits)}.");
        RuleFor(x => x.RhythmInterval).InclusiveBetween(1, 365);
        RuleFor(x => x.AnchorDay).InclusiveBetween(1, 31).When(x => x.AnchorDay.HasValue);
        RuleFor(x => x.ReminderDaysBefore).InclusiveBetween(0, 365).When(x => x.ReminderDaysBefore.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class MarkCommitmentDoneRequestValidator : Validator<MarkCommitmentDoneRequest>
{
    private static readonly string[] Channels = ["bank", "wise", "cash", "other"];

    public MarkCommitmentDoneRequestValidator()
    {
        RuleFor(x => x.Amount).PositiveMoney();
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.ApproxGbp).NonNegativeMoney();
        RuleFor(x => x.Channel)
            .NotEmpty()
            .Must(c => Channels.Contains(c))
            .WithMessage($"Channel must be one of: {string.Join(", ", Channels)}.");
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public sealed class SkipCommitmentRequestValidator : Validator<SkipCommitmentRequest>
{
    public SkipCommitmentRequestValidator() => RuleFor(x => x.Reason).MaximumLength(500);
}

public sealed class SnoozeCommitmentRequestValidator : Validator<SnoozeCommitmentRequest>
{
    public SnoozeCommitmentRequestValidator()
        => RuleFor(x => x.Until)
            .GreaterThan(DateTime.UtcNow.AddDays(-1))
            .WithMessage("Snooze date must not be in the past.");
}

internal static class PersonalFinanceValidatorConstants
{
    internal static readonly string[] MatchTypes = ["exact", "contains", "startsWith", "endsWith", "regex"];
    internal static readonly string[] RuleScopes = ["account", "user", "household"];
    internal static readonly string[] ApprovalStatuses = ["Pending", "Approved", "Rejected"];
    internal static readonly string[] RhythmUnits = ["Weekly", "Monthly", "Quarterly", "Termly", "Yearly", "OneOff"];
}
