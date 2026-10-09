using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;

using FastEndpoints;

namespace Aonik.Platform.Endpoints.Identity;

public sealed class CreateCustomerAddressEndpoint(ICustomerAddressService addresses) : Endpoint<CustomerAddressWrite, CustomerAddressBookDto>
{
    public override void Configure()
    {
        Post("/profiles/customers/me/addresses");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CustomerAddressWrite req, CancellationToken ct)
        => await Send.CreatedAtAsync<GetCustomerAddressesEndpoint>(routeValues: new { }, responseBody: await addresses.CreateAsync(req, ct), cancellation: ct);
}
