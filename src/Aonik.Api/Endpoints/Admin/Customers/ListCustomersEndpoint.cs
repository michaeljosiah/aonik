using Aonik.Application.Models.Customers;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Customers;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Customers;

public class ListCustomersEndpoint : Endpoint<ListCustomersRequest, PagedResult<CustomerListItem>>
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
    }

    public override async Task HandleAsync(ListCustomersRequest req, CancellationToken ct)
    {
        var result = await _customerAdminService.ListCustomersAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
