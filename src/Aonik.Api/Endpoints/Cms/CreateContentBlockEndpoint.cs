using Aonik.Application.Services.Cms;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Cms;

public class CreateContentBlockEndpoint : Endpoint<Contracts.Cms.CreateContentBlockRequest, Contracts.Cms.ContentBlockResponse>
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

    public override async Task HandleAsync(Contracts.Cms.CreateContentBlockRequest req, CancellationToken ct)
    {
        var appRequest = new Application.Models.Cms.CreateContentBlockRequest(
            req.ContentKey,
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

        var result = await _contentBlockService.CreateContentBlockAsync(appRequest, ct);
        
        await Send.CreatedAtAsync<GetContentBlockEndpoint>(
            routeValues: new { id = result.Id },
            responseBody: MapToContract(result),
            cancellation: ct);
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
