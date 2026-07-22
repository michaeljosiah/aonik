using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class CreateCollectionEndpoint : Endpoint<CreateCollectionRequest, AdminCollectionDto>
{
    private readonly ICollectionService _collections;

    public CreateCollectionEndpoint(ICollectionService collections) => _collections = collections;

    public override void Configure()
    {
        Post("/commerce/admin/collections");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Create a curated collection.");
    }

    public override async Task HandleAsync(CreateCollectionRequest req, CancellationToken ct)
    {
        var result = await _collections.CreateAsync(
            new CreateCollectionCommand(req.Slug, req.Title, req.Subtitle, req.Kind ?? CollectionKinds.Curated, req.SortOrder),
            ct);
        await Send.OkAsync(result, ct);
    }
}
