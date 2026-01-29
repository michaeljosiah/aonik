using Aonik.Application.Services.Cms;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Cms;

public class UpdateContentBlockEndpoint : Endpoint<Contracts.Cms.UpdateContentBlockRequest, Contracts.Cms.ContentBlockResponse>
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

    public override async Task HandleAsync(Contracts.Cms.UpdateContentBlockRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        
        var appRequest = new Application.Models.Cms.UpdateContentBlockRequest(
            req.Title,
            req.Slug,
            req.Area,
            req.Format,
            req.Body,
            req.Locale,
            req.IsEnabled,
            req.StartAt,
            req.EndAt,
            req.Priority,
            req.TargetingJson);

        var result = await _contentBlockService.UpdateContentBlockAsync(id, appRequest, ct);
        await Send.OkAsync(MapToContract(result), ct);
    }

    private static Contracts.Cms.ContentBlockResponse MapToContract(Application.Models.Cms.ContentBlockResponse response)
    {
        return new Contracts.Cms.ContentBlockResponse(
            response.Id,
            response.ContentKey,
            response.Title,
            response.Slug,
            response.Area,
            response.Format,
            response.Body,
            response.Locale,
            response.IsEnabled,
            response.StartAt,
            response.EndAt,
            response.Priority,
            response.Media.Select(m => new Contracts.Cms.ContentBlockMediaResponse(
                m.Id,
                m.StorageType,
                m.Url,
                m.Alt,
                m.Caption,
                m.MimeType,
                m.Order,
                m.LinkUrl)).ToList(),
            response.CreatedAt,
            response.UpdatedAt);
    }
}
