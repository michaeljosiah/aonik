using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Cms;

internal class RemoveContentBlockMediaEndpoint : EndpointWithoutRequest
{
    private readonly IContentBlockService _contentBlockService;

    public RemoveContentBlockMediaEndpoint(IContentBlockService contentBlockService)
    {
        _contentBlockService = contentBlockService;
    }

    public override void Configure()
    {
        Delete("/cms/content-blocks/{id}/media/{mediaId}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Remove media from a content block";
            s.Description = "Detaches and removes a specific media item from a content block.";
            s.Response(204, "Media removed");
            s.Response(401, "Not authenticated");
            s.Response(404, "Content block or media not found");
        });
        Options(x => x.WithTags("Content Management"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var contentBlockId = Route<Guid>("id");
        var mediaId = Route<Guid>("mediaId");
        
        await _contentBlockService.RemoveMediaAsync(contentBlockId, mediaId, ct);
        await Send.NoContentAsync(ct);
    }
}
