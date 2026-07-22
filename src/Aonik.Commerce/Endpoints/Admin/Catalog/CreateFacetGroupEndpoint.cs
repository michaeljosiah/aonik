using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class CreateFacetGroupEndpoint : Endpoint<CreateFacetGroupRequest, FacetGroupDto>
{
    private readonly IFacetGroupService _facets;

    public CreateFacetGroupEndpoint(IFacetGroupService facets) => _facets = facets;

    public override void Configure()
    {
        Post("/commerce/admin/facet-groups");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Create a facet group definition.");
    }

    public override async Task HandleAsync(CreateFacetGroupRequest req, CancellationToken ct)
    {
        var result = await _facets.CreateAsync(
            new CreateFacetGroupCommand(req.Key, req.Label, req.MatchKind, req.OptionsJson, req.SourcePath, req.SortOrder),
            ct);
        await Send.OkAsync(result, ct);
    }
}
