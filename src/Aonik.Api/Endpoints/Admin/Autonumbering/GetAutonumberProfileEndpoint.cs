using Aonik.Application.Abstractions.Autonumbering;
using Aonik.Application.Models.Autonumbering;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Autonumbering;

public class GetAutonumberProfileEndpoint : EndpointWithoutRequest<AutonumberProfileSnapshot>
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
