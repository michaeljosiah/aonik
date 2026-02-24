using Aonik.Platform.Contracts.Models.Cms;
using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;

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
