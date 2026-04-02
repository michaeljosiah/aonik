using Aonik.Finance.Contracts.Api.PayActivity;
using Aonik.Finance.Contracts.Services.PayActivity;

using FastEndpoints;

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
