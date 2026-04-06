using Aonik.Platform.Contracts.Models.Cms;
using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Cms;

internal class ListContentBlocksEndpoint : EndpointWithoutRequest<List<ContentBlockResponse>>
{
    private readonly IContentBlockService _contentBlockService;

    public ListContentBlocksEndpoint(IContentBlockService contentBlockService)
    {
        _contentBlockService = contentBlockService;
    }

    public override void Configure()
    {
        Get("/cms/content-blocks");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List content blocks";
            s.Description = "Returns all CMS content blocks, optionally filtered by area, content key, locale, and enabled status.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Content Management"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var area = Query<string>("area", false);
        var contentKey = Query<string>("contentKey", false);
        var locale = Query<string>("locale", false) ?? "en";
        var isEnabled = Query<bool?>("isEnabled", false);

        var request = new ContentBlockListRequest(area, contentKey, locale, isEnabled);
        var results = await _contentBlockService.ListContentBlocksAsync(request, ct);

        await Send.OkAsync(results, ct);
    }
}
