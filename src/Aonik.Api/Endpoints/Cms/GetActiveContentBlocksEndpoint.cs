using Aonik.Application.Services.Cms;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Cms;

public class GetActiveContentBlocksEndpoint : EndpointWithoutRequest<List<Contracts.Cms.ContentBlockResponse>>
{
    private readonly IContentBlockService _contentBlockService;

    public GetActiveContentBlocksEndpoint(IContentBlockService contentBlockService)
    {
        _contentBlockService = contentBlockService;
    }

    public override void Configure()
    {
        Get("/cms/content/active");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var area = Query<string>("area", true) ?? "General";
        var locale = Query<string>("locale", false) ?? "en";

        var results = await _contentBlockService.GetActiveContentBlocksAsync(area, locale, ct);
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
