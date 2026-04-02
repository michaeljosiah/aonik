using Aonik.Finance.Contracts.Api.PayActivity;
using Aonik.Finance.Contracts.Services.PayActivity;

using FastEndpoints;

namespace Aonik.Finance.Endpoints.Payments;

/// <summary>
/// GET /payments/activity
/// Returns the recent pay activity for the current authenticated user.
/// Powers the mobile Pay dashboard "Recent Activity" section.
/// </summary>
internal sealed class ListPayActivityEndpoint : EndpointWithoutRequest<PayActivitySummaryResponse>
{
    private readonly IPayActivityService _payActivityService;

    public ListPayActivityEndpoint(IPayActivityService payActivityService)
    {
        _payActivityService = payActivityService;
    }

    public override void Configure()
    {
        Get("/payments/activity");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _payActivityService.GetRecentActivityAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
