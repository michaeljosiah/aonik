using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;

using FastEndpoints;

namespace Aonik.Platform.Endpoints.Identity;

public sealed class UpdateCustomerAddressEndpoint(ICustomerAddressService addresses) : Endpoint<CustomerAddressWrite, CustomerAddressBookDto>
{
    public override void Configure()
    {
        Put("/profiles/customers/me/addresses/{addressId:guid}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CustomerAddressWrite req, CancellationToken ct)
        => await Send.OkAsync(await addresses.UpdateAsync(Route<Guid>("addressId"), req, ct), ct);
}
