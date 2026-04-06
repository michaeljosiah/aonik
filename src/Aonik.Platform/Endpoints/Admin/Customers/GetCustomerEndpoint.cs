using Aonik.Platform.Contracts.Models.Customers;
using Aonik.Platform.Contracts.Services.Customers;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Get customer by party ID";
            s.Description = "Retrieves the full detail of a customer including profile and contact information.";
            s.Response(200, "Customer details");
            s.Response(401, "Not authenticated");
            s.Response(404, "Customer not found");
        });
        Options(x => x.WithTags("Customer Administration"));
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
