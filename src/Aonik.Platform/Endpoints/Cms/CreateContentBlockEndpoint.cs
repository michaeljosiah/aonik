using Aonik.Platform.Contracts.Models.Cms;
using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Create a content block";
            s.Description = "Creates a new CMS content block with the specified area, key, locale, and body content.";
            s.Response(201, "Content block created");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Content Management"));
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
