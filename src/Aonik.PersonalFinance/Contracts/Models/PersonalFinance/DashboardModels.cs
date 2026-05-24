namespace Aonik.Finance.Contracts.Models.PersonalFinance;

// ── Bill DTOs ───────────────────────────────────────────────────

public record CreateBillRequest(
    string Payee,
    string Frequency,
    DateTime NextDueDate,
    decimal? ExpectedAmount,
    string Currency,
    bool Autopay,
    Guid? PaidFromAccountId);

public record UpdateBillRequest(
    string Payee,
    string Frequency,
    DateTime NextDueDate,
    decimal? ExpectedAmount,
    string Currency,
    bool Autopay,
    Guid? PaidFromAccountId,
    string Status);

public record BillResponse(
    Guid BillId,
    Guid UserId,
    string Payee,
    string Frequency,
    DateTime NextDueDate,
    decimal? ExpectedAmount,
    string Currency,
    bool Autopay,
    Guid? PaidFromAccountId,
    Guid? LinkedInvoiceId,
    Guid? LinkedOrderId,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// ── Dashboard BFF DTOs ──────────────────────────────────────────

public record DashboardResponse(
    DashboardMetricsDto Metrics,
    IReadOnlyList<DashboardBillDto> UpcomingBills,
    IReadOnlyList<DashboardOrderDto> RecentOrders,
    DashboardOverviewDto Overview);

public record DashboardMetricsDto(
    decimal AvailableToSpend,
    string AvailableToSpendLabel,
    string AvailableToSpendSubtitle,
    double SpendableProgress,
    string SpendableProgressLabel,
    decimal NetWorth,
    string NetWorthLabel,
    decimal NetWorthChange,
    string NetWorthChangeLabel,
    string NetWorthTrendLabel,
    decimal TotalAssets,
    string AssetsLabel,
    decimal TotalBillsDue,
    string BillsLabel,
    string Currency,
    int UpcomingBillsCount);

public record DashboardBillDto(
    Guid Id,
    string Payee,
    decimal? ExpectedAmount,
    string AmountLabel,
    string Currency,
    DateTime NextDueDate,
    string DueDateLabel);

public record DashboardOrderDto(
    Guid Id,
    string BeneficiaryName,
    string? BeneficiaryPhotoUrl,
    decimal Amount,
    string AmountLabel,
    string OrderType,
    string Status,
    string DateLabel);

public record DashboardOverviewDto(
    string MonthLabel,
    string MonthShortLabel,
    string YearLabel,
    IReadOnlyList<DashboardOverviewSliceDto> Slices);

public record DashboardOverviewSliceDto(
    string Label,
    decimal Amount,
    string AmountLabel,
    string ColorKey);

// ── Safe-to-spend DTOs ──────────────────────────────────────────

public record SafeToSpendBreakdownResponse(
    decimal LiquidAssets,
    string LiquidAssetsLabel,
    decimal ProtectedObligations,
    string ProtectedObligationsLabel,
    decimal AvailableToSpend,
    string AvailableToSpendLabel,
    string Currency,
    DateTime AsOfUtc,
    int LookaheadDays,
    IReadOnlyList<SafeToSpendFactorDto> Factors);

public record SafeToSpendFactorDto(
    string Kind,
    Guid SourceId,
    string Label,
    decimal Amount,
    string AmountLabel,
    string Currency,
    DateTime DueDate,
    int DaysUntilDue);
