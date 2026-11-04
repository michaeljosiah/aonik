using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Endpoints;

internal sealed class DecideEditorialReviewEndpoint(IEditorialReviewService reviews) : PublishingEndpoint<EditorialDecisionRequest, EditorialDecisionResponse>
{
    public override void Configure()
    {
        Post("/publishing/review/submissions/{Id:guid}/decisions"); Policies("PublishingReviewPolicy"); Tags("Publishing review");
        Options(builder => builder.WithMetadata(new RequestSizeLimitAttribute(16 * 1024)));
    }
    public override Task HandleAsync(EditorialDecisionRequest req, CancellationToken ct) => GuardedAsync(async () =>
    {
        HttpContext.Response.Headers.CacheControl = "private, no-store";
        await Send.OkAsync(await reviews.DecideAsync(Route<Guid>("Id"), req, ct), ct);
    });
}
