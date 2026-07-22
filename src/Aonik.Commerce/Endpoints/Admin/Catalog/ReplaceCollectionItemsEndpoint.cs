using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>Full-replace of a collection's ranked membership (Spec 070 §10) — an idempotent
/// reorder. Ranks must be unique (A12); a draft product may be staged (A9).</summary>
public class ReplaceCollectionItemsEndpoint : Endpoint<ReplaceCollectionItemsRequest, AdminCollectionDto>
{
    private readonly ICollectionService _collections;

    public ReplaceCollectionItemsEndpoint(ICollectionService collections) => _collections = collections;

    public override void Configure()
    {
        Put("/commerce/admin/collections/{collectionId:guid}/items");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Replace a collection's ranked membership.");
    }

    public override async Task HandleAsync(ReplaceCollectionItemsRequest req, CancellationToken ct)
    {
        // A missing or misspelled items property binds to null; coalescing it to empty would make
        // a malformed payload indistinguishable from an intentional clear. The service rejects
        // null — emptying a collection requires an explicit empty array.
        var result = await _collections.ReplaceItemsAsync(
            Route<Guid>("collectionId"),
            new ReplaceCollectionItemsCommand(
                req.Items?.Select(i => new CollectionItemLine(i.ProductId, i.Rank)).ToList()),
            ct);
        await Send.OkAsync(result, ct);
    }
}
