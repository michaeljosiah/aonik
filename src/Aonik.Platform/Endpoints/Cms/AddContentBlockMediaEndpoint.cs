using Aonik.Platform.Contracts.Models.Cms;
using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Cms;

internal class AddContentBlockMediaEndpoint : Endpoint<AddContentBlockMediaRequest, ContentBlockMediaResponse>
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
        Summary(s =>
        {
            s.Summary = "Add media to a content block";
            s.Description = "Attaches a new media item (image, video, etc.) to an existing content block.";
            s.Response(200, "Media added");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Content Management"));
    }

    public override async Task HandleAsync(AddContentBlockMediaRequest req, CancellationToken ct)
    {
        var contentBlockId = Route<Guid>("id");
        var result = await _contentBlockService.AddMediaAsync(contentBlockId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
