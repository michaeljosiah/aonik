using Aonik.Platform.Contracts.Models.Customers;
using Aonik.Platform.Contracts.Services.Customers;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Customers;

internal class GetCustomerEndpoint : EndpointWithoutRequest<CustomerDetail>
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
