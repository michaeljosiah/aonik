using Aonik.Platform.Contracts.Models.Autonumbering;
using Aonik.Platform.Contracts.Services.Autonumbering;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Autonumbering;

internal class GenerateAutonumberEndpoint : Endpoint<AutonumberGenerateRequest, AutonumberGenerateResult>
{
    private readonly IAutonumberingService _autonumberingService;

    public GenerateAutonumberEndpoint(IAutonumberingService autonumberingService)
    {
        _autonumberingService = autonumberingService;
    }

    public override void Configure()
    {
        Post("/admin/autonumbering/generate");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(AutonumberGenerateRequest req, CancellationToken ct)
    {
        var result = await _autonumberingService.GenerateAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
