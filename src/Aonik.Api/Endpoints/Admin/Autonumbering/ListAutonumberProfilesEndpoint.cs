using Aonik.Application.Abstractions.Autonumbering;
using Aonik.Application.Models.Autonumbering;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Autonumbering;

public class ListAutonumberProfilesEndpoint : EndpointWithoutRequest<List<AutonumberProfileSnapshot>>
{
    private readonly IAutonumberingService _autonumberingService;

    public ListAutonumberProfilesEndpoint(IAutonumberingService autonumberingService)
    {
        _autonumberingService = autonumberingService;
    }

    public override void Configure()
    {
        Get("/admin/autonumbering/profiles");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var entityTypes = new[] { "Invoice", "Order", "CreditNote", "Payment", "Payout" };
        var profiles = new List<AutonumberProfileSnapshot>();

        foreach (var entityType in entityTypes)
        {
            var profile = await _autonumberingService.GetProfileAsync(entityType, cancellationToken: ct);
            if (profile != null)
            {
                profiles.Add(profile);
            }
        }

        await Send.OkAsync(profiles, ct);
    }
}
