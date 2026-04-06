using Aonik.Platform.Contracts.Models.Customers;
using Aonik.Platform.Contracts.Services.Customers;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Create a new customer";
            s.Description = "Creates a new customer party record with profile details for the current tenant.";
            s.Response(201, "Customer created");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Customer Administration"));
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
