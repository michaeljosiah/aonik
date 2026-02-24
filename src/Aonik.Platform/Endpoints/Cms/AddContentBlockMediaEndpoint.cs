using Aonik.Platform.Contracts.Models.Cms;
using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Cms;

internal class AddContentBlockMediaEndpoint : Endpoint<AddContentBlockMediaRequest, ContentBlockMediaResponse>
{
    private readonly IContentBlockService _contentBlockService;

    public AddContentBlockMediaEndpoint(IContentBlockService contentBlockService)
    {
        _contentBlockService = contentBlockService;
    }

    public override void Configure()
    {
        Post("/cms/content-blocks/{id}/media");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(AddContentBlockMediaRequest req, CancellationToken ct)
    {
        var contentBlockId = Route<Guid>("id");
        var result = await _contentBlockService.AddMediaAsync(contentBlockId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
