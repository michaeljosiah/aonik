using Aonik.Platform.Contracts.Models.Cms;
using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Cms;

internal class CreateContentBlockEndpoint : Endpoint<CreateContentBlockRequest, ContentBlockResponse>
{
    private readonly IContentBlockService _contentBlockService;

    public CreateContentBlockEndpoint(IContentBlockService contentBlockService)
    {
        _contentBlockService = contentBlockService;
    }

    public override void Configure()
    {
        Post("/cms/content-blocks");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CreateContentBlockRequest req, CancellationToken ct)
    {
        var result = await _contentBlockService.CreateContentBlockAsync(req, ct);
        
        await Send.CreatedAtAsync<GetContentBlockEndpoint>(
            routeValues: new { id = result.Id },
            responseBody: result,
            cancellation: ct);
    }
}
