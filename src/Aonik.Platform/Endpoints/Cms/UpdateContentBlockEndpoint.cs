using Aonik.Platform.Contracts.Models.Cms;
using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Update a content block";
            s.Description = "Updates an existing CMS content block's area, key, locale, body, or enabled status.";
            s.Response(200, "Success");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "Content block not found");
        });
        Options(x => x.WithTags("Content Management"));
    }

    public override async Task HandleAsync(UpdateContentBlockRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _contentBlockService.UpdateContentBlockAsync(id, req, ct);
        await Send.OkAsync(result, ct);
    }
}
