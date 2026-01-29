using Aonik.Application.Services.Cms;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Cms;

public class AddContentBlockMediaEndpoint : Endpoint<Contracts.Cms.AddContentBlockMediaRequest, Contracts.Cms.ContentBlockMediaResponse>
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

    public override async Task HandleAsync(Contracts.Cms.AddContentBlockMediaRequest req, CancellationToken ct)
    {
        var contentBlockId = Route<Guid>("id");
        
        var appRequest = new Application.Models.Cms.AddContentBlockMediaRequest(
            req.Url,
            req.Alt,
            req.Caption,
            req.MimeType,
            req.LinkUrl);

        var result = await _contentBlockService.AddMediaAsync(contentBlockId, appRequest, ct);
        
        await Send.OkAsync(new Contracts.Cms.ContentBlockMediaResponse(
            result.Id,
            result.StorageType,
            result.Url,
            result.Alt,
            result.Caption,
            result.MimeType,
            result.Order,
            result.LinkUrl), ct);
    }
}
