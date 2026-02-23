using Aonik.Platform.Contracts.Api.ReferenceData;
using Aonik.Platform.Contracts.Services.ReferenceData;
using Aonik.Platform.Contracts.Models.ReferenceData;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.ReferenceData;

public class UpsertReferenceDataItemEndpoint : Endpoint<ReferenceDataItemUpsertRequest, ReferenceDataItemAdminResponse>
{
    private readonly IReferenceDataService _referenceDataService;

    public UpsertReferenceDataItemEndpoint(IReferenceDataService referenceDataService)
    {
        _referenceDataService = referenceDataService;
    }

    public override void Configure()
    {
        Put("/admin/reference-data/{type}/{code}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(ReferenceDataItemUpsertRequest req, CancellationToken ct)
    {
        var type = Route<string>("type") ?? string.Empty;
        var code = Route<string>("code") ?? string.Empty;
        type = type.Trim();
        code = code.Trim();

        var result = await _referenceDataService.UpsertAsync(
            new ReferenceDataItemUpsert(
                type,
                code,
                req.DisplayName,
                req.SortOrder,
                req.IsActive),
            cancellationToken: ct);

        var response = new ReferenceDataItemAdminResponse(
            result.Type,
            result.Code,
            result.DisplayName,
            result.SortOrder,
            result.IsActive);

        await Send.OkAsync(response, ct);
    }
}
