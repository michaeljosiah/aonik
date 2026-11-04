using FastEndpoints;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Endpoints;

public sealed class ListCreatorDecisionsRequest
{
    public Guid Id { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

internal sealed class ListCreatorDecisionsEndpoint(ISubmissionRevisionService revisions) : PublishingEndpoint<ListCreatorDecisionsRequest, CreatorDecisionPage>
{
    public override void Configure() { Get("/publishing/submissions/{Id:guid}/decisions"); Policies("UserPolicy"); Tags("Publishing"); }
    public override Task HandleAsync(ListCreatorDecisionsRequest req, CancellationToken ct) => GuardedAsync(async () =>
    {
        HttpContext.Response.Headers.CacheControl = "private, no-store";
        await Send.OkAsync(await revisions.DecisionsAsync(req.Id, req.Page, req.PageSize, ct), ct);
    });
}
