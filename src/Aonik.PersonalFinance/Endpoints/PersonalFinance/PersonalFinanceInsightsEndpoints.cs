using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

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
        Summary(s =>
        {
            s.Summary = "Get spending summary";
            s.Description = "Returns an aggregated spending summary for a given period, including totals, averages, and comparisons to the previous period.";
            s.Response(200, "Spending summary returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(SpendingInsightsRequest req, CancellationToken ct)
    {
        try
        {
            var (periodStart, periodEnd) = ResolvePeriod(req.PeriodStart, req.PeriodEnd);
            var response = await _insightsService.GetSpendingSummaryAsync(periodStart, periodEnd, req.PersonalAccountId, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
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
        Summary(s =>
        {
            s.Summary = "Get spending by category";
            s.Description = "Returns a breakdown of spending grouped by transaction category for the specified period and optional account filter.";
            s.Response(200, "Category breakdown returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(SpendingInsightsRequest req, CancellationToken ct)
    {
        try
        {
            var (periodStart, periodEnd) = ResolvePeriod(req.PeriodStart, req.PeriodEnd);
            var response = await _insightsService.GetCategoryBreakdownAsync(periodStart, periodEnd, req.PersonalAccountId, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
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
        Summary(s =>
        {
            s.Summary = "Get spending by merchant";
            s.Description = "Returns a ranked breakdown of spending grouped by merchant for the specified period, limited to the top N merchants.";
            s.Response(200, "Merchant breakdown returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(SpendingInsightsRequest req, CancellationToken ct)
    {
        try
        {
            var (periodStart, periodEnd) = ResolvePeriod(req.PeriodStart, req.PeriodEnd);
            var response = await _insightsService.GetMerchantBreakdownAsync(periodStart, periodEnd, req.PersonalAccountId, req.Top, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
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
        Summary(s =>
        {
            s.Summary = "Get spending by account";
            s.Description = "Returns a breakdown of spending grouped by personal account for the specified period.";
            s.Response(200, "Account breakdown returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(SpendingInsightsRequest req, CancellationToken ct)
    {
        try
        {
            var (periodStart, periodEnd) = ResolvePeriod(req.PeriodStart, req.PeriodEnd);
            var response = await _insightsService.GetAccountBreakdownAsync(periodStart, periodEnd, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }

    private static (DateTime PeriodStart, DateTime PeriodEnd) ResolvePeriod(DateTime? requestedStart, DateTime? requestedEnd)
    {
        var now = DateTime.UtcNow;
        var start = requestedStart ?? new DateTime(now.Year, now.Month, 1);
        var end = requestedEnd ?? start.AddMonths(1).AddTicks(-1);
        return (start, end);
    }
}
