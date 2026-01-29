using Aonik.Application.Services.Cms;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Cms;

public class GetContentBlockEndpoint : EndpointWithoutRequest<Contracts.Cms.ContentBlockResponse>
{
    private readonly IContentBlockService _contentBlockService;

    public GetContentBlockEndpoint(IContentBlockService contentBlockService)
    {
        _contentBlockService = contentBlockService;
    }

    public override void Configure()
    {
        Get("/cms/content-blocks/{id}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _contentBlockService.GetContentBlockAsync(id, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

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
