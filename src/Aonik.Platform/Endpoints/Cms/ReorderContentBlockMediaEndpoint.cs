using Aonik.Platform.Contracts.Models.Cms;
using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Cms;

internal class ReorderContentBlockMediaEndpoint : Endpoint<ReorderContentBlockMediaRequest>
{
    private readonly IContentBlockService _contentBlockService;

    public ReorderContentBlockMediaEndpoint(IContentBlockService contentBlockService)
    {
        _contentBlockService = contentBlockService;
    }

    public override void Configure()
    {
        Put("/cms/content-blocks/{id}/media/reorder");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Reorder content block media";
            s.Description = "Updates the display order of media items attached to a content block.";
            s.Response(204, "Media reordered");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Content Management"));
    }

    public override async Task HandleAsync(ReorderContentBlockMediaRequest req, CancellationToken ct)
    {
        var contentBlockId = Route<Guid>("id");
        await _contentBlockService.ReorderMediaAsync(contentBlockId, req.MediaIds, ct);
        await Send.NoContentAsync(ct);
    }
}
