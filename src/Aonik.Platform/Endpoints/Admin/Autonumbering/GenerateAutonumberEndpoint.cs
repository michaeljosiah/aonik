using Aonik.Platform.Contracts.Models.Autonumbering;
using Aonik.Platform.Contracts.Services.Autonumbering;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Generate next autonumber";
            s.Description = "Generates and reserves the next sequential number for the specified entity type using its autonumber profile.";
            s.Response(200, "Generated number");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("System Administration"));
    }

    public override async Task HandleAsync(AutonumberGenerateRequest req, CancellationToken ct)
    {
        var result = await _autonumberingService.GenerateAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
