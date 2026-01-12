using Aonik.Api.Contracts.ReferenceData;
using Aonik.Application.Abstractions.ReferenceData;
using FastEndpoints;

namespace Aonik.Api.Endpoints.ReferenceData;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var type = Route<string>("type");
        var items = await _referenceDataService.GetAsync(type, cancellationToken: ct);

        var response = items
            .Select(item => new ReferenceDataItemResponse(item.Code, item.DisplayName, item.SortOrder))
            .ToList();

        await Send.OkAsync(response, ct);
    }
}
