using Aonik.Platform.Contracts.Models.Customers;
using Aonik.Platform.Contracts.Services.Customers;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Customers;

public class GetCustomerStatsEndpoint : EndpointWithoutRequest<CustomerStats>
{
    private readonly ICustomerAdminService _customerAdminService;

    public GetCustomerStatsEndpoint(ICustomerAdminService customerAdminService)
    {
        _customerAdminService = customerAdminService;
    }

    public override void Configure()
    {
        Get("/admin/customers/{partyId}/stats");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partyId = Route<Guid>("partyId");
        var result = await _customerAdminService.GetCustomerStatsAsync(partyId, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
