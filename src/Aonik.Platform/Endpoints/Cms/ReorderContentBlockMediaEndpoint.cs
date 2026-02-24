using Aonik.Platform.Contracts.Models.Cms;
using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(ReorderContentBlockMediaRequest req, CancellationToken ct)
    {
        var contentBlockId = Route<Guid>("id");
        await _contentBlockService.ReorderMediaAsync(contentBlockId, req.MediaIds, ct);
        await Send.NoContentAsync(ct);
    }
}
