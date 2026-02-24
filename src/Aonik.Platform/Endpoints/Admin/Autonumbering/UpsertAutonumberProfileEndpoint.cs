using Aonik.Platform.Contracts.Models.Autonumbering;
using Aonik.Platform.Contracts.Services.Autonumbering;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(AutonumberProfileUpsert req, CancellationToken ct)
    {
        var result = await _autonumberingService.UpsertProfileAsync(req, cancellationToken: ct);
        await Send.OkAsync(result, ct);
    }
}
