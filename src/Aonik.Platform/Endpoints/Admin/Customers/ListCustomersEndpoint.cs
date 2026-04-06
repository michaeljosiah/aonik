using Aonik.Platform.Contracts.Models.Customers;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.SharedKernel.Abstractions;
using Aonik.Platform.Contracts.Services.Customers;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Customers;

internal class ListCustomersEndpoint : Endpoint<ListCustomersRequest, PagedResult<CustomerListItem>>
{
    private readonly ICustomerAdminService _customerAdminService;

    public ListCustomersEndpoint(ICustomerAdminService customerAdminService)
    {
        _customerAdminService = customerAdminService;
    }

    public override void Configure()
    {
        Get("/admin/customers");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List all customers";
            s.Description = "Returns a paginated list of customers for the current tenant with optional filtering.";
            s.Response(200, "Paged customer list");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Customer Administration"));
    }

    public override async Task HandleAsync(ListCustomersRequest req, CancellationToken ct)
    {
        var result = await _customerAdminService.ListCustomersAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
