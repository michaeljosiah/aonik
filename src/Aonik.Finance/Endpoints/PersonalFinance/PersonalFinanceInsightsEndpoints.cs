using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class SpendingInsightsRequest
{
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public Guid? PersonalAccountId { get; set; }
    public int Top { get; set; } = 10;
}

internal sealed class GetSpendingSummaryEndpoint : Endpoint<SpendingInsightsRequest, SpendingSummaryResponse>
{
    private readonly IPersonalFinanceInsightsService _insightsService;

    public GetSpendingSummaryEndpoint(IPersonalFinanceInsightsService insightsService)
    {
        _insightsService = insightsService;
    }

    public override void Configure()
    {
        Get("/personal-finance/insights/spending-summary");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(SpendingInsightsRequest req, CancellationToken ct)
    {
        var (periodStart, periodEnd) = ResolvePeriod(req.PeriodStart, req.PeriodEnd);
        var response = await _insightsService.GetSpendingSummaryAsync(periodStart, periodEnd, req.PersonalAccountId, ct);
        await Send.OkAsync(response, ct);
    }

    private static (DateTime PeriodStart, DateTime PeriodEnd) ResolvePeriod(DateTime? requestedStart, DateTime? requestedEnd)
    {
        var now = DateTime.UtcNow;
        var start = requestedStart ?? new DateTime(now.Year, now.Month, 1);
        var end = requestedEnd ?? start.AddMonths(1).AddTicks(-1);
        return (start, end);
    }
}

internal sealed class GetCategoryBreakdownEndpoint : Endpoint<SpendingInsightsRequest, IReadOnlyList<CategorySpendingItemResponse>>
{
    private readonly IPersonalFinanceInsightsService _insightsService;

    public GetCategoryBreakdownEndpoint(IPersonalFinanceInsightsService insightsService)
    {
        _insightsService = insightsService;
    }

    public override void Configure()
    {
        Get("/personal-finance/insights/category-breakdown");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(SpendingInsightsRequest req, CancellationToken ct)
    {
        var (periodStart, periodEnd) = ResolvePeriod(req.PeriodStart, req.PeriodEnd);
        var response = await _insightsService.GetCategoryBreakdownAsync(periodStart, periodEnd, req.PersonalAccountId, ct);
        await Send.OkAsync(response, ct);
    }

    private static (DateTime PeriodStart, DateTime PeriodEnd) ResolvePeriod(DateTime? requestedStart, DateTime? requestedEnd)
    {
        var now = DateTime.UtcNow;
        var start = requestedStart ?? new DateTime(now.Year, now.Month, 1);
        var end = requestedEnd ?? start.AddMonths(1).AddTicks(-1);
        return (start, end);
    }
}

internal sealed class GetMerchantBreakdownEndpoint : Endpoint<SpendingInsightsRequest, IReadOnlyList<MerchantSpendingItemResponse>>
{
    private readonly IPersonalFinanceInsightsService _insightsService;

    public GetMerchantBreakdownEndpoint(IPersonalFinanceInsightsService insightsService)
    {
        _insightsService = insightsService;
    }

    public override void Configure()
    {
        Get("/personal-finance/insights/merchant-breakdown");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(SpendingInsightsRequest req, CancellationToken ct)
    {
        var (periodStart, periodEnd) = ResolvePeriod(req.PeriodStart, req.PeriodEnd);
        var response = await _insightsService.GetMerchantBreakdownAsync(periodStart, periodEnd, req.PersonalAccountId, req.Top, ct);
        await Send.OkAsync(response, ct);
    }

    private static (DateTime PeriodStart, DateTime PeriodEnd) ResolvePeriod(DateTime? requestedStart, DateTime? requestedEnd)
    {
        var now = DateTime.UtcNow;
        var start = requestedStart ?? new DateTime(now.Year, now.Month, 1);
        var end = requestedEnd ?? start.AddMonths(1).AddTicks(-1);
        return (start, end);
    }
}

internal sealed class GetAccountBreakdownEndpoint : Endpoint<SpendingInsightsRequest, IReadOnlyList<AccountSpendingItemResponse>>
{
    private readonly IPersonalFinanceInsightsService _insightsService;

    public GetAccountBreakdownEndpoint(IPersonalFinanceInsightsService insightsService)
    {
        _insightsService = insightsService;
    }

    public override void Configure()
    {
        Get("/personal-finance/insights/account-breakdown");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(SpendingInsightsRequest req, CancellationToken ct)
    {
        var (periodStart, periodEnd) = ResolvePeriod(req.PeriodStart, req.PeriodEnd);
        var response = await _insightsService.GetAccountBreakdownAsync(periodStart, periodEnd, ct);
        await Send.OkAsync(response, ct);
    }

    private static (DateTime PeriodStart, DateTime PeriodEnd) ResolvePeriod(DateTime? requestedStart, DateTime? requestedEnd)
    {
        var now = DateTime.UtcNow;
        var start = requestedStart ?? new DateTime(now.Year, now.Month, 1);
        var end = requestedEnd ?? start.AddMonths(1).AddTicks(-1);
        return (start, end);
    }
}
