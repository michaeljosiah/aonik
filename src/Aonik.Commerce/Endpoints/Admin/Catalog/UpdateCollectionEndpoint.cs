using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class UpdateCollectionEndpoint : Endpoint<UpdateCollectionRequest, AdminCollectionDto>
{
    private readonly ICollectionService _collections;

    public UpdateCollectionEndpoint(ICollectionService collections) => _collections = collections;

    public override void Configure()
    {
        Put("/commerce/admin/collections/{collectionId:guid}");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Update a collection. The slug is immutable; omitted members are unchanged.");
    }

    public override async Task HandleAsync(UpdateCollectionRequest req, CancellationToken ct)
    {
        var result = await _collections.UpdateAsync(
            Route<Guid>("collectionId"),
            new UpdateCollectionCommand(req.Title, req.Subtitle, req.ClearSubtitle, req.Kind, req.SortOrder, req.IsActive),
            ct);
        await Send.OkAsync(result, ct);
    }
}
