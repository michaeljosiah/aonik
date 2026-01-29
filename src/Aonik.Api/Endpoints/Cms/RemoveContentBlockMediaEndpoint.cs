using Aonik.Application.Services.Cms;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Cms;

public class RemoveContentBlockMediaEndpoint : EndpointWithoutRequest
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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var contentBlockId = Route<Guid>("id");
        var mediaId = Route<Guid>("mediaId");
        
        await _contentBlockService.RemoveMediaAsync(contentBlockId, mediaId, ct);
        await Send.NoContentAsync(ct);
    }
}
