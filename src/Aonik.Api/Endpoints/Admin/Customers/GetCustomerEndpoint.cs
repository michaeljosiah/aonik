using Aonik.Application.Models.Customers;
using Aonik.Application.Services.Customers;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Customers;

public class GetCustomerEndpoint : EndpointWithoutRequest<CustomerDetail>
{
    private readonly ICustomerAdminService _customerAdminService;

    public GetCustomerEndpoint(ICustomerAdminService customerAdminService)
    {
        _customerAdminService = customerAdminService;
    }

    public override void Configure()
    {
        Get("/admin/customers/{partyId}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partyId = Route<Guid>("partyId");
        var result = await _customerAdminService.GetCustomerAsync(partyId, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
