using Aonik.Platform.Contracts.Models.Autonumbering;
using Aonik.Platform.Contracts.Services.Autonumbering;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Autonumbering;

internal class ListAutonumberProfilesEndpoint : EndpointWithoutRequest<List<AutonumberProfileSnapshot>>
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
        Summary(s =>
        {
            s.Summary = "List autonumber profiles";
            s.Description = "Returns all configured autonumber profiles for supported entity types (Invoice, Order, CreditNote, Payment, Payout).";
            s.Response(200, "Profile list");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("System Administration"));
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
