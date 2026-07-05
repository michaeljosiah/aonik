using Aonik.PersonalFinance.Contracts.Models.Accounts;
using Aonik.PersonalFinance.Endpoints.Admin;
using Aonik.PersonalFinance.Endpoints.Admin.Accounts;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.PersonalFinance.Endpoints;

// ────────────────────────────────────────────────────────────────────
// Validators for internal-visibility endpoint-level request DTOs that
// live next to their endpoint files (Endpoints/PersonalFinance/*,
// Endpoints/Admin/*, Endpoints/Admin/Accounts/*). Same assembly = visibility OK.
// ────────────────────────────────────────────────────────────────────

// ── Admin / PersonalFinance ─────────────────────────────────────────

internal sealed class AdminListAccountsRequestValidator : Validator<AdminListAccountsRequest>
{
    public AdminListAccountsRequestValidator() => RuleFor(x => x.UserId).RequiredId();
}

internal sealed class AdminListBudgetsRequestValidator : Validator<AdminListBudgetsRequest>
{
    public AdminListBudgetsRequestValidator() => RuleFor(x => x.UserId).RequiredId();
}

internal sealed class AdminListCommitmentsRequestValidator : Validator<AdminListCommitmentsRequest>
{
    public AdminListCommitmentsRequestValidator()
    {
        RuleFor(x => x.UserId).RequiredId();
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.Type).MaximumLength(64);
        RuleFor(x => x.Page).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 500);
    }
}

internal sealed class AdminListTransactionsRequestValidator : Validator<AdminListTransactionsRequest>
{
    public AdminListTransactionsRequestValidator()
    {
        RuleFor(x => x.UserId).RequiredId();
        RuleFor(x => x.PersonalAccountId).ValidIdWhenSupplied();
        RuleFor(x => x.Category).MaximumLength(64);
        RuleFor(x => x.Search).MaximumLength(256);
        RuleFor(x => x.Page).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 500);
    }
}

internal sealed class AdminBindPersonalFinancePartyToUserRequestValidator : Validator<AdminBindPersonalFinancePartyToUserRequest>
{
    public AdminBindPersonalFinancePartyToUserRequestValidator()
    {
        RuleFor(x => x.PartyId).RequiredId();
        RuleFor(x => x.TargetUserId).ValidIdWhenSupplied();
    }
}

internal sealed class AdminGetFinancialLifeGraphRequestValidator : Validator<AdminGetFinancialLifeGraphRequest>
{
    public AdminGetFinancialLifeGraphRequestValidator() => RuleFor(x => x.UserId).RequiredId();
}

internal sealed class RebuildCustomerInsightAiSummaryRequestValidator : Validator<RebuildCustomerInsightAiSummaryRequest>
{
    public RebuildCustomerInsightAiSummaryRequestValidator() => RuleFor(x => x.SnapshotId).RequiredId();
}

internal sealed class RebuildCustomerInsightSnapshotRequestValidator : Validator<RebuildCustomerInsightSnapshotRequest>
{
    public RebuildCustomerInsightSnapshotRequestValidator() => RuleFor(x => x.UserId).RequiredId();
}

internal sealed class EnsurePersonalProfileRequestValidator : Validator<EnsurePersonalProfileRequest>
{
    public EnsurePersonalProfileRequestValidator()
    {
        RuleFor(x => x.UserId).RequiredId();
        RuleFor(x => x.TenantId).RequiredId();
        RuleFor(x => x.PartyId).RequiredId();
    }
}

// ── Admin / Accounts ────────────────────────────────────────────────

internal sealed class ListAccountConnectionsRequestValidator : Validator<ListAccountConnectionsRequest>
{
    public ListAccountConnectionsRequestValidator() { /* boolean only */ }
}

internal sealed class AdminListAccountTransactionsRequestValidator
    : Validator<Admin.Accounts.ListAccountTransactionsRequest>
{
    public AdminListAccountTransactionsRequestValidator()
    {
        RuleFor(x => x.ExternalAccountId).ValidIdWhenSupplied();
        RuleFor(x => x.ConnectionId).ValidIdWhenSupplied();
        RuleFor(x => x.ReconciliationStatus).MaximumLength(64);
        RuleFor(x => x.PageNumber).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 500);
    }
}

// ── Billing list + Insights validators relocated to FinanceValidators.cs
//    (ListInvoicesRequest / GetMySpaceSummaryRequest stay in Aonik.Finance).

// ── PersonalFinance endpoint DTOs ───────────────────────────────────

internal sealed class AccountLinkSummaryRequestValidator : Validator<AccountLinkSummaryRequest>
{
    public AccountLinkSummaryRequestValidator() { /* boolean only */ }
}

internal sealed class ListAccountLinksRequestValidator : Validator<ListAccountLinksRequest>
{
    public ListAccountLinksRequestValidator() { /* boolean only */ }
}

internal sealed class ListPersonalAccountsRequestValidator : Validator<ListPersonalAccountsRequest>
{
    public ListPersonalAccountsRequestValidator() { /* boolean only */ }
}

internal sealed class ListFinancialContextsRequestValidator : Validator<ListFinancialContextsRequest>
{
    public ListFinancialContextsRequestValidator() { /* boolean only */ }
}

internal sealed class ArchiveBillRequestValidator : Validator<ArchiveBillRequest>
{
    public ArchiveBillRequestValidator() => RuleFor(x => x.BillId).RequiredId();
}

internal sealed class GetBillRequestValidator : Validator<GetBillRequest>
{
    public GetBillRequestValidator() => RuleFor(x => x.BillId).RequiredId();
}

internal sealed class GetUpcomingBillsRequestValidator : Validator<GetUpcomingBillsRequest>
{
    public GetUpcomingBillsRequestValidator() => RuleFor(x => x.Days).InclusiveBetween(1, 365);
}

internal sealed class ListBillsRequestValidator : Validator<ListBillsRequest>
{
    public ListBillsRequestValidator() => RuleFor(x => x.Status).MaximumLength(64);
}

internal sealed class UpdateBillRouteRequestValidator : Validator<UpdateBillRouteRequest>
{
    public UpdateBillRouteRequestValidator()
    {
        RuleFor(x => x.BillId).RequiredId();
        RuleFor(x => x.Payee).RequiredText(256);
        RuleFor(x => x.Frequency).RequiredText(32);
        RuleFor(x => x.NextDueDate)
            .GreaterThan(DateTime.UtcNow.AddYears(-1))
            .LessThan(DateTime.UtcNow.AddYears(10));
        RuleFor(x => x.ExpectedAmount).PositiveMoney();
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.PaidFromAccountId).ValidIdWhenSupplied();
        RuleFor(x => x.Status).RequiredText(64);
    }
}

internal sealed class GetCustomerInsightAiSummaryRequestValidator : Validator<GetCustomerInsightAiSummaryRequest>
{
    public GetCustomerInsightAiSummaryRequestValidator()
        => RuleFor(x => x.SnapshotId).RequiredId();
}

internal sealed class GetCustomerInsightSnapshotByIdRequestValidator : Validator<GetCustomerInsightSnapshotByIdRequest>
{
    public GetCustomerInsightSnapshotByIdRequestValidator()
        => RuleFor(x => x.SnapshotId).RequiredId();
}

internal sealed class GetCustomerInsightSnapshotHistoryRequestValidator : Validator<GetCustomerInsightSnapshotHistoryRequest>
{
    public GetCustomerInsightSnapshotHistoryRequestValidator()
        => RuleFor(x => x.Take).InclusiveBetween(1, 500);
}

internal sealed class GetFinancialContextSummaryRequestValidator : Validator<GetFinancialContextSummaryRequest>
{
    public GetFinancialContextSummaryRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.From <= x.To)
            .WithMessage("From must be less than or equal to To.");
    }
}

internal sealed class GetMerchantHistoryRequestValidator : Validator<GetMerchantHistoryRequest>
{
    public GetMerchantHistoryRequestValidator() => RuleFor(x => x.Merchant).RequiredText(256);
}

internal sealed class InviteHouseholdMemberEndpointRequestValidator : Validator<InviteHouseholdMemberEndpointRequest>
{
    private static readonly string[] HouseholdRoles = ["Owner", "Manager", "Member", "Viewer"];

    public InviteHouseholdMemberEndpointRequestValidator()
    {
        RuleFor(x => x.UserId).RequiredId();
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(r => HouseholdRoles.Contains(r))
            .WithMessage($"Role must be one of: {string.Join(", ", HouseholdRoles)}.");
        RuleFor(x => x.Permissions)
            .Must(p => p == null || p.Count <= 64)
            .WithMessage("Permissions may include at most 64 entries.");
    }
}

internal sealed class ListCommitmentsRequestValidator : Validator<ListCommitmentsRequest>
{
    public ListCommitmentsRequestValidator()
    {
        RuleFor(x => x.Type).MaximumLength(64);
        RuleFor(x => x.VerificationStatus).MaximumLength(64);
        RuleFor(x => x.Status).MaximumLength(64);
        RuleFor(x => x.AccountId).ValidIdWhenSupplied();
        RuleFor(x => x.Search).MaximumLength(256);
        RuleFor(x => x.Page).PageNumber();
        RuleFor(x => x.PageSize).PageSize(1, 500);
    }
}

internal sealed class RejectCommitmentRequestValidator : Validator<RejectCommitmentRequest>
{
    public RejectCommitmentRequestValidator() => RuleFor(x => x.Reason).MaximumLength(2048);
}

internal sealed class SpendingInsightsRequestValidator : Validator<SpendingInsightsRequest>
{
    public SpendingInsightsRequestValidator()
    {
        // Cross-field period-range ordering is enforced by the insights
        // service (which returns 422 with a domain-specific message). Keep
        // the validator focused on per-field bounds.
        RuleFor(x => x.PersonalAccountId).ValidIdWhenSupplied();
        RuleFor(x => x.Top).InclusiveBetween(1, 500);
    }
}

internal sealed class UpcomingObligationsRequestValidator : Validator<UpcomingObligationsRequest>
{
    public UpcomingObligationsRequestValidator()
        => RuleFor(x => x.WithinDays).InclusiveBetween(1, 3650);
}

// ── Accounts (Tenant-Scoped Bank Linking) ───────────────────────────
// Relocated from Aonik.Finance's FinanceValidators.cs (Spec 027 S5, #118/#126)
// to co-locate with the moved AccountLink endpoints. Validate the PF Accounts
// contract DTOs (Aonik.PersonalFinance.Contracts.Models.Accounts).

public sealed class CreateAccountLinkSessionRequestValidator : Validator<CreateAccountLinkSessionRequest>
{
    public CreateAccountLinkSessionRequestValidator()
    {
        RuleFor(x => x.Provider).RequiredText(64);
        RuleFor(x => x.Mode)
            .NotEmpty()
            .Must(m => m is "connect" or "update" or "reauth")
            .WithMessage("Mode must be one of: connect, update, reauth.");
        RuleFor(x => x.ConnectionId).ValidIdWhenSupplied();
        RuleFor(x => x.CountryCode)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
        RuleFor(x => x.ClientName).MaximumLength(128);
    }
}

public sealed class ExchangeAccountLinkSessionRequestValidator : Validator<ExchangeAccountLinkSessionRequest>
{
    public ExchangeAccountLinkSessionRequestValidator()
    {
        RuleFor(x => x.SessionId).RequiredId();
        RuleFor(x => x.TemporaryCode).RequiredText(2048);
    }
}

public sealed class CreateAccountRequestValidator : Validator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(256);
        RuleFor(x => x.AccountType).RequiredText(64);
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.Country)
            .Length(2).Matches("^[A-Z]{2}$")
            .When(x => !string.IsNullOrEmpty(x.Country));
        RuleFor(x => x.InstitutionName).MaximumLength(256);
        RuleFor(x => x.Last4)
            .Length(4).Matches("^[0-9]{4}$").WithMessage("Last4 must be 4 digits.")
            .When(x => !string.IsNullOrEmpty(x.Last4));
        RuleFor(x => x.Notes).MaximumLength(2048);
    }
}

public sealed class CreateAccountTransactionRequestValidator : Validator<CreateAccountTransactionRequest>
{
    public CreateAccountTransactionRequestValidator()
    {
        RuleFor(x => x.AccountId).RequiredId();
        RuleFor(x => x.OccurredAt)
            .GreaterThan(DateTime.UtcNow.AddYears(-50))
            .LessThan(DateTime.UtcNow.AddYears(1));
        RuleFor(x => x.Amount)
            .Must(a => a != 0).WithMessage("Amount must be non-zero.")
            .Must(a => Math.Abs(a) <= 1_000_000_000m).WithMessage("Amount exceeds maximum supported value.");
        RuleFor(x => x.Currency).CurrencyCode();
        RuleFor(x => x.Counterparty).MaximumLength(256);
        RuleFor(x => x.Description).MaximumLength(1024);
        RuleFor(x => x.Reference).MaximumLength(256);
        RuleFor(x => x.Category).MaximumLength(64);
        RuleFor(x => x.Notes).MaximumLength(2048);
    }
}

public sealed class PlaidAccountWebhookRequestValidator : Validator<PlaidAccountWebhookRequest>
{
    public PlaidAccountWebhookRequestValidator()
    {
        RuleFor(x => x.WebhookType).RequiredText(64);
        RuleFor(x => x.WebhookCode).RequiredText(64);
        RuleFor(x => x.ItemId).MaximumLength(256);
        RuleFor(x => x.Environment).MaximumLength(64);
    }
}
