using Aonik.Application.Abstractions.Autonumbering;
using Aonik.Application.Models.Autonumbering;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Autonumbering;

public class PreviewAutonumberEndpoint : Endpoint<AutonumberGenerateRequest, AutonumberGenerateResult>
{
    private readonly IAutonumberingService _autonumberingService;

    public PreviewAutonumberEndpoint(IAutonumberingService autonumberingService)
    {
        _autonumberingService = autonumberingService;
    }

    public override void Configure()
    {
        Post("/admin/autonumbering/preview");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(AutonumberGenerateRequest req, CancellationToken ct)
    {
        var result = await _autonumberingService.PreviewAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
