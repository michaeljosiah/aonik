using Aonik.Application.Services.Cms;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Cms;

public class ListContentBlocksEndpoint : EndpointWithoutRequest<List<Contracts.Cms.ContentBlockResponse>>
{
    private readonly IContentBlockService _contentBlockService;

    public ListContentBlocksEndpoint(IContentBlockService contentBlockService)
    {
        _contentBlockService = contentBlockService;
    }

    public override void Configure()
    {
        Get("/cms/content-blocks");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var area = Query<string>("area", false);
        var contentKey = Query<string>("contentKey", false);
        var locale = Query<string>("locale", false) ?? "en";
        var isEnabled = Query<bool?>("isEnabled", false);

        var request = new Application.Models.Cms.ContentBlockListRequest(area, contentKey, locale, isEnabled);
        var results = await _contentBlockService.ListContentBlocksAsync(request, ct);

        await Send.OkAsync(results.Select(MapToContract).ToList(), ct);
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
