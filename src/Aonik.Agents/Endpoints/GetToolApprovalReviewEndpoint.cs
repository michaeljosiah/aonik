using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Endpoints;

internal sealed class GetToolApprovalReviewEndpoint(IToolApprovalService approvals) : EndpointWithoutRequest<PendingToolApprovalReview>
{
    public override void Configure()
    {
        Get("/ai/tool-approvals/{Id:guid}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        PendingToolApprovalReview? review = await approvals.GetPendingReviewAsync(Route<Guid>("Id"), ct);
        if (review is null) await Send.NotFoundAsync(ct);
        else await Send.OkAsync(review, ct);
    }
}
