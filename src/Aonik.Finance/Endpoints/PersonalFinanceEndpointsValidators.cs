using Aonik.Finance.Endpoints.Admin.Accounts;
using Aonik.Finance.Endpoints.Admin.PersonalFinance;
using Aonik.Finance.Endpoints.Billing;
using Aonik.Finance.Endpoints.Insights;
using Aonik.Finance.Endpoints.PersonalFinance;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints;

// ────────────────────────────────────────────────────────────────────
// Validators for internal-visibility endpoint-level request DTOs that
// live next to their endpoint files (Endpoints/PersonalFinance/*,
// Endpoints/Admin/*, etc.). Same assembly = visibility OK.
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

internal sealed class AdminListAccountTransactionsRequestValidator : Validator<ListAccountTransactionsRequest>
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
        RuleFor(x => x.PersonalAccountId).ValidIdWhenSupplied();
        RuleFor(x => x.Top).InclusiveBetween(1, 500);
        RuleFor(x => x)
            .Must(x => !x.PeriodStart.HasValue || !x.PeriodEnd.HasValue || x.PeriodStart <= x.PeriodEnd)
            .WithMessage("PeriodStart must be less than or equal to PeriodEnd.");
    }
}

internal sealed class UpcomingObligationsRequestValidator : Validator<UpcomingObligationsRequest>
{
    public UpcomingObligationsRequestValidator()
        => RuleFor(x => x.WithinDays).InclusiveBetween(1, 3650);
}
