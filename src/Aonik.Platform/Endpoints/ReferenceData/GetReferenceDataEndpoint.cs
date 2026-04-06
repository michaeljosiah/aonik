using Aonik.Platform.Contracts.Api.ReferenceData;
using Aonik.Platform.Contracts.Services.ReferenceData;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.ReferenceData;

public class GetReferenceDataEndpoint : EndpointWithoutRequest<List<ReferenceDataItemResponse>>
{
    private readonly IReferenceDataService _referenceDataService;

    public GetReferenceDataEndpoint(IReferenceDataService referenceDataService)
    {
        _referenceDataService = referenceDataService;
    }

    public override void Configure()
    {
        Get("/reference-data/{type}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get reference data by type";
            s.Description = "Returns a list of reference data items for the specified type (e.g., countries, currencies). No authentication required.";
            s.Response(200, "Success");
        });
        Options(x => x.WithTags("Reference Data"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var type = Route<string>("type") ?? string.Empty;
        var items = await _referenceDataService.GetAsync(type, cancellationToken: ct);

        var response = items
            .Select(item => new ReferenceDataItemResponse(item.Code, item.DisplayName, item.SortOrder))
            .ToList();

        await Send.OkAsync(response, ct);
    }
}
