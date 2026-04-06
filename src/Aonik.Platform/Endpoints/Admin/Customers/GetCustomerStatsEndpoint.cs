using Aonik.Platform.Contracts.Models.Customers;
using Aonik.Platform.Contracts.Services.Customers;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Customers;

internal class GetCustomerStatsEndpoint : EndpointWithoutRequest<CustomerStats>
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
        Summary(s =>
        {
            s.Summary = "Get customer statistics";
            s.Description = "Retrieves aggregated statistics for a customer such as transaction counts and activity metrics.";
            s.Response(200, "Customer statistics");
            s.Response(401, "Not authenticated");
            s.Response(404, "Customer not found");
        });
        Options(x => x.WithTags("Customer Administration"));
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
