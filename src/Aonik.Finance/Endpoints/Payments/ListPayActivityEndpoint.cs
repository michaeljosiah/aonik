using Aonik.Finance.Contracts.Api.PayActivity;
using Aonik.Finance.Contracts.Services.PayActivity;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "List recent pay activity";
            s.Description = "Returns the recent payment activity summary for the current authenticated user.";
            s.Response(200, "Activity retrieved successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _payActivityService.GetRecentActivityAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
