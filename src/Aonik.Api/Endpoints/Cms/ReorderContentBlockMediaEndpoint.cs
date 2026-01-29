using Aonik.Application.Services.Cms;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Cms;

public class ReorderContentBlockMediaEndpoint : Endpoint<Contracts.Cms.ReorderContentBlockMediaRequest>
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

    public override async Task HandleAsync(Contracts.Cms.ReorderContentBlockMediaRequest req, CancellationToken ct)
    {
        var contentBlockId = Route<Guid>("id");
        await _contentBlockService.ReorderMediaAsync(contentBlockId, req.MediaIds, ct);
        await Send.NoContentAsync(ct);
    }
}
