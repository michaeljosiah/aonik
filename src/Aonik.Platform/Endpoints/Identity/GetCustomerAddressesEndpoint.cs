using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;

using FastEndpoints;

namespace Aonik.Platform.Endpoints.Identity;

public sealed class GetCustomerAddressesEndpoint(ICustomerAddressService addresses) : EndpointWithoutRequest<CustomerAddressBookDto>
{
    public override void Configure()
    {
        Get("/profiles/customers/me/addresses");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await addresses.GetAsync(ct);
        if (result is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(result, ct);
    }
}
