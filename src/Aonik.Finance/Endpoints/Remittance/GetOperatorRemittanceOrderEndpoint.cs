using Aonik.Authorization;
using Aonik.Finance.Contracts.Api.Remittance;
using Aonik.Finance.Contracts.Services.Remittance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Remittance;

/// <summary>
/// <c>GET /remittance/{id}</c> — returns a remittance order scoped to the current tenant and the
/// authorised staff audience; an order the tenant does not own reads as 404. Spec 036 §10.3.
/// </summary>
public class GetOperatorRemittanceOrderEndpoint : EndpointWithoutRequest<RemittanceOrderResponse>
{
    private readonly IOperatorRemittanceService _remittanceService;

    public GetOperatorRemittanceOrderEndpoint(IOperatorRemittanceService remittanceService)
    {
        _remittanceService = remittanceService;
    }

    public override void Configure()
    {
        Get("/remittance/{id:guid}");
        Policies("AdminReadPolicy");
        this.RequiresPermission("Payment.Read");
        Summary(s =>
        {
            s.Summary = "Get a remittance order";
            s.Description = "Retrieves a remittance order's current state for the current tenant/staff permission.";
            s.Response(200, "Remittance retrieved");
            s.Response(401, "Not authenticated");
            s.Response(404, "Remittance not found");
        });
        Options(x => x.WithTags("Remittance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var orderId = Route<Guid>("id");
        var result = await _remittanceService.GetForOperatorAsync(orderId, ct);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(RemittanceMapping.ToApi(result), ct);
    }
}
