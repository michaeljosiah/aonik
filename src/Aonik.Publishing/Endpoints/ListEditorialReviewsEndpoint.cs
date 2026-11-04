using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Endpoints;

public sealed class ListEditorialReviewsRequest
{
    public string ChannelKey { get; set; } = "";
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Query { get; set; }
}

internal sealed class ListEditorialReviewsEndpoint(IEditorialReviewService reviews) : PublishingEndpoint<ListEditorialReviewsRequest, EditorialReviewPage>
{
    public override void Configure() { Get("/publishing/review/submissions"); Policies("PublishingReviewPolicy"); Tags("Publishing review"); }
    public override Task HandleAsync(ListEditorialReviewsRequest req, CancellationToken ct) => GuardedAsync(async () =>
    {
        HttpContext.Response.Headers.CacheControl = "private, no-store";
        await Send.OkAsync(await reviews.ListAsync(req.ChannelKey, req.Status, req.Page, req.PageSize, ct, req.Query), ct);
    });
}
