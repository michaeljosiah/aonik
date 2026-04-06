using Aonik.Platform.Contracts.Models.Cms;
using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Cms;

internal class GetActiveContentBlocksEndpoint : EndpointWithoutRequest<List<ContentBlockResponse>>
{
    private readonly IContentBlockService _contentBlockService;

    public GetActiveContentBlocksEndpoint(IContentBlockService contentBlockService)
    {
        _contentBlockService = contentBlockService;
    }

    public override void Configure()
    {
        Get("/cms/content/active");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get active content blocks";
            s.Description = "Returns all enabled content blocks for a given area and locale. Accessible without authentication.";
            s.Response(200, "Success");
        });
        Options(x => x.WithTags("Content Management"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var area = Query<string>("area", true) ?? "General";
        var locale = Query<string>("locale", false) ?? "en";

        var results = await _contentBlockService.GetActiveContentBlocksAsync(area, locale, ct);
        await Send.OkAsync(results, ct);
    }
}
