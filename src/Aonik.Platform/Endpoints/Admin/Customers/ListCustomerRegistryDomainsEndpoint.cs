using Aonik.Platform.Contracts.Models.Customers;
using Aonik.Platform.Contracts.Services.Customers;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Customers;

/// <summary>
/// GET /admin/customers/domains — Spec 080: which product lines actually have customers in this
/// tenant. The registry's domain filter tabs are built from this rather than from the loaded page,
/// because a page-local derivation would offer tabs that filter and paginate wrongly.
/// </summary>
internal class ListCustomerRegistryDomainsEndpoint : EndpointWithoutRequest<CustomerRegistryDomainsResponse>
{
    private readonly ICustomerAdminService _customerAdminService;

    public ListCustomerRegistryDomainsEndpoint(ICustomerAdminService customerAdminService)
    {
        _customerAdminService = customerAdminService;
    }

    public override void Configure()
    {
        Get("/admin/customers/domains");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List the customer domains in use";
            s.Description = "Returns the product-line keys that have at least one customer in the current tenant.";
            s.Response(200, "Active domain keys");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Customer Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _customerAdminService.GetRegistryDomainsAsync(ct), ct);
}
