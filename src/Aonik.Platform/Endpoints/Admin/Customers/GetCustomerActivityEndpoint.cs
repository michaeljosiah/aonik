using Aonik.Platform.Contracts.Models.Customers;
using Aonik.Platform.Contracts.Services.Customers;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Customers;

internal sealed class GetCustomerActivityResponse
{
    public IReadOnlyList<CustomerActivityEntryDto> Items { get; init; } = Array.Empty<CustomerActivityEntryDto>();
}

internal class GetCustomerActivityEndpoint : EndpointWithoutRequest<GetCustomerActivityResponse>
{
    private readonly ICustomerAdminService _customerAdminService;

    public GetCustomerActivityEndpoint(ICustomerAdminService customerAdminService)
    {
        _customerAdminService = customerAdminService;
    }

    public override void Configure()
    {
        Get("/admin/customers/{partyId}/activity");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get customer activity feed";
            s.Description =
                "Returns a unified, sorted feed of recent activity for the customer — finance events (orders, payments), audit log entries, and document uploads. Capped at 100 entries.";
            s.Response(200, "Activity feed (most recent first)");
            s.Response(401, "Not authenticated");
            s.Response(404, "Customer not found");
        });
        Options(x => x.WithTags("Customer Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partyId = Route<Guid>("partyId");
        var take = Query<int?>("take", isRequired: false) ?? 20;

        var entries = await _customerAdminService.GetCustomerActivityAsync(partyId, take, ct);

        if (entries == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new GetCustomerActivityResponse { Items = entries }, ct);
    }
}
