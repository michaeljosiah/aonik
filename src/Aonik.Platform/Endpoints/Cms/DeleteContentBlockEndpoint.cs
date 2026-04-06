using Aonik.Platform.Contracts.Services.Cms;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Cms;

internal class DeleteContentBlockEndpoint : EndpointWithoutRequest
{
    private readonly IContentBlockService _contentBlockService;

    public DeleteContentBlockEndpoint(IContentBlockService contentBlockService)
    {
        _contentBlockService = contentBlockService;
    }

    public override void Configure()
    {
        Delete("/cms/content-blocks/{id}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Delete a content block";
            s.Description = "Permanently removes a CMS content block and its associated media by ID.";
            s.Response(204, "Content block deleted");
            s.Response(401, "Not authenticated");
            s.Response(404, "Content block not found");
        });
        Options(x => x.WithTags("Content Management"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        await _contentBlockService.DeleteContentBlockAsync(id, ct);
        await Send.NoContentAsync(ct);
    }
}
