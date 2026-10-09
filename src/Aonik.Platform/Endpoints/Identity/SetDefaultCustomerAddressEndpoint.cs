using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;

using FastEndpoints;

namespace Aonik.Platform.Endpoints.Identity;

public sealed class SetDefaultCustomerAddressEndpoint(ICustomerAddressService addresses) : Endpoint<CustomerAddressVersionRequest, CustomerAddressBookDto>
{
    public override void Configure()
    {
        Put("/profiles/customers/me/addresses/{addressId:guid}/default");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CustomerAddressVersionRequest req, CancellationToken ct)
        => await Send.OkAsync(await addresses.SetDefaultAsync(Route<Guid>("addressId"), req.ExpectedVersion, ct), ct);
}
