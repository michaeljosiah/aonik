using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Cms;

internal class DeleteContentBlockEndpoint : EndpointWithoutRequest
{
    private readonly IContentBlockService _contentBlockService;

    public DeleteContentBlockEndpoint(IContentBlockService contentBlockService)
    {
        _contentBlockService = contentBlockService;
    }

    public override void Configure()
    {
        Delete("/cms/content-blocks/{id}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        await _contentBlockService.DeleteContentBlockAsync(id, ct);
        await Send.NoContentAsync(ct);
    }
}
