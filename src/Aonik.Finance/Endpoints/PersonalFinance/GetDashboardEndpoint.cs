using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class GetDashboardEndpoint : EndpointWithoutRequest<DashboardResponse>
{
    private readonly IDashboardService _dashboardService;

    public GetDashboardEndpoint(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public override void Configure()
    {
        Get("/personal-finance/dashboard");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _dashboardService.GetDashboardAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
