using Aonik.Platform.Contracts.Models.Customers;
using Aonik.Platform.Contracts.Services.Customers;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Customers;

internal class CreateCustomerEndpoint : Endpoint<CreateCustomerRequest, CreateCustomerResponse>
{
    private readonly ICustomerAdminService _customerAdminService;

    public CreateCustomerEndpoint(ICustomerAdminService customerAdminService)
    {
        _customerAdminService = customerAdminService;
    }

    public override void Configure()
    {
        Post("/admin/customers");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CreateCustomerRequest req, CancellationToken ct)
    {
        var result = await _customerAdminService.CreateCustomerAsync(req, ct);
        await Send.CreatedAtAsync<GetCustomerEndpoint>(
            routeValues: new { partyId = result.PartyId },
            responseBody: result,
            cancellation: ct);
    }
}
