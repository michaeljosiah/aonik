using Aonik.Platform.Contracts.Models.Autonumbering;
using Aonik.Platform.Contracts.Services.Autonumbering;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Autonumbering;

internal class GetAutonumberProfileEndpoint : EndpointWithoutRequest<AutonumberProfileSnapshot>
{
    private readonly IAutonumberingService _autonumberingService;

    public GetAutonumberProfileEndpoint(IAutonumberingService autonumberingService)
    {
        _autonumberingService = autonumberingService;
    }

    public override void Configure()
    {
        Get("/admin/autonumbering/profiles/{entityType}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get autonumber profile";
            s.Description = "Retrieves the autonumber profile configuration for the specified entity type.";
            s.Response(200, "Profile details");
            s.Response(401, "Not authenticated");
            s.Response(404, "Profile not found");
        });
        Options(x => x.WithTags("System Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var entityType = Route<string>("entityType");

        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new ArgumentException("Entity type is required.", nameof(entityType));
        }

        var result = await _autonumberingService.GetProfileAsync(entityType, cancellationToken: ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
