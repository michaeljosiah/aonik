using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class UpdateFacetGroupEndpoint : Endpoint<UpdateFacetGroupRequest, FacetGroupDto>
{
    private readonly IFacetGroupService _facets;

    public UpdateFacetGroupEndpoint(IFacetGroupService facets) => _facets = facets;

    public override void Configure()
    {
        Put("/commerce/admin/facet-groups/{facetGroupId:guid}");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Update a facet group. Key and match kind are immutable; omitted members are unchanged.");
    }

    public override async Task HandleAsync(UpdateFacetGroupRequest req, CancellationToken ct)
    {
        var result = await _facets.UpdateAsync(
            Route<Guid>("facetGroupId"),
            new UpdateFacetGroupCommand(req.Label, req.OptionsJson, req.SourcePath, req.SortOrder, req.IsActive),
            ct);
        await Send.OkAsync(result, ct);
    }
}
