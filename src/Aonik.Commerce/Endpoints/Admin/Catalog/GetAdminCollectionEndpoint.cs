using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>One collection with full membership — draft members and ranks included (Spec 070 §10).</summary>
public class GetAdminCollectionEndpoint : EndpointWithoutRequest<AdminCollectionDto>
{
    private readonly ICollectionService _collections;

    public GetAdminCollectionEndpoint(ICollectionService collections) => _collections = collections;

    public override void Configure()
    {
        Get("/commerce/admin/collections/{collectionId:guid}");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Get one collection with its full membership.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _collections.GetAdminAsync(Route<Guid>("collectionId"), ct), ct);
}
