using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class GetDashboardEndpoint : EndpointWithoutRequest<DashboardResponse>
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<GetDashboardEndpoint> _logger;

    public GetDashboardEndpoint(
        IDashboardService dashboardService,
        ILogger<GetDashboardEndpoint> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/personal-finance/dashboard");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get personal finance dashboard";
            s.Description = "Returns an aggregated dashboard view including account balances, recent transactions, spending summaries, and upcoming bills.";
            s.Response(200, "Dashboard data returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var response = await _dashboardService.GetDashboardAsync(ct);
            await Send.OkAsync(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard endpoint failed");
            ThrowError(ex.Message, statusCode: 500);
        }
    }
}
