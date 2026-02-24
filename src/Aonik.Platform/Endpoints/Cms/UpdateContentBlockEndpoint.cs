using Aonik.Platform.Contracts.Models.Cms;
using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Cms;

internal class UpdateContentBlockEndpoint : Endpoint<UpdateContentBlockRequest, ContentBlockResponse>
{
    private readonly IContentBlockService _contentBlockService;

    public UpdateContentBlockEndpoint(IContentBlockService contentBlockService)
    {
        _contentBlockService = contentBlockService;
    }

    public override void Configure()
    {
        Put("/cms/content-blocks/{id}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(UpdateContentBlockRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _contentBlockService.UpdateContentBlockAsync(id, req, ct);
        await Send.OkAsync(result, ct);
    }
}
