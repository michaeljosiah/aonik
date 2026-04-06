using Aonik.Finance.Contracts.Api.PayActivity;
using Aonik.Finance.Contracts.Services.PayActivity;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Payments;

/// <summary>
/// GET /payments/activity/{id}
/// Returns the full transaction detail for a single pay activity item.
/// Powers the mobile transaction details screen.
/// </summary>
internal sealed class GetPayActivityDetailEndpoint : EndpointWithoutRequest<PayActivityTransactionDetailResponse>
{
    private readonly IPayActivityService _payActivityService;

    public GetPayActivityDetailEndpoint(IPayActivityService payActivityService)
    {
        _payActivityService = payActivityService;
    }

    public override void Configure()
    {
        Get("/payments/activity/{id}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get pay activity transaction detail";
            s.Description = "Returns the full transaction detail for a single pay activity item.";
            s.Response(200, "Transaction detail retrieved successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Transaction not found");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var response = await _payActivityService.GetTransactionDetailAsync(id, ct);

        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}
