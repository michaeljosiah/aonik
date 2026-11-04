using FastEndpoints;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Endpoints;

internal sealed class GetEditorialReviewEndpoint(IEditorialReviewService reviews) : PublishingEndpoint<SubmissionPath, EditorialReviewDetail>
{
    public override void Configure() { Get("/publishing/review/submissions/{Id:guid}"); Policies("PublishingReviewPolicy"); Tags("Publishing review"); }
    public override Task HandleAsync(SubmissionPath req, CancellationToken ct) => GuardedAsync(async () =>
    {
        HttpContext.Response.Headers.CacheControl = "private, no-store";
        await Send.OkAsync(await reviews.GetAsync(req.Id, ct), ct);
    });
}
