using Aonik.Platform.Contracts.Models.Cms;
using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Cms;

internal class GetContentBlockEndpoint : EndpointWithoutRequest<ContentBlockResponse>
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
        Summary(s =>
        {
            s.Summary = "Get content block by ID";
            s.Description = "Retrieves a single CMS content block by its unique identifier.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
            s.Response(404, "Content block not found");
        });
        Options(x => x.WithTags("Content Management"));
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

        await Send.OkAsync(result, ct);
    }
}
