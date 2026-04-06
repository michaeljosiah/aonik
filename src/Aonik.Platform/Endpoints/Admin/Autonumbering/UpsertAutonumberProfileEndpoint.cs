using Aonik.Platform.Contracts.Models.Autonumbering;
using Aonik.Platform.Contracts.Services.Autonumbering;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Autonumbering;

internal class UpsertAutonumberProfileEndpoint : Endpoint<AutonumberProfileUpsert, AutonumberProfileSnapshot>
{
    private readonly IAutonumberingService _autonumberingService;

    public UpsertAutonumberProfileEndpoint(IAutonumberingService autonumberingService)
    {
        _autonumberingService = autonumberingService;
    }

    public override void Configure()
    {
        Put("/admin/autonumbering/profiles");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Create or update autonumber profile";
            s.Description = "Creates a new or updates an existing autonumber profile for the specified entity type.";
            s.Response(200, "Profile saved");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("System Administration"));
    }

    public override async Task HandleAsync(AutonumberProfileUpsert req, CancellationToken ct)
    {
        var result = await _autonumberingService.UpsertProfileAsync(req, cancellationToken: ct);
        await Send.OkAsync(result, ct);
    }
}
