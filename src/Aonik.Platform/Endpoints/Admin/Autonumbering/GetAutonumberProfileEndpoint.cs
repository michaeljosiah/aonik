using Aonik.Platform.Contracts.Models.Autonumbering;
using Aonik.Platform.Contracts.Services.Autonumbering;
using FastEndpoints;

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
