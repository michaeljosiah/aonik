using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Endpoints;

public sealed record EditorialImagePath(Guid Id,Guid RevisionId,string AssetId);
internal sealed class PreviewEditorialImageEndpoint(IEditorialReviewService reviews):PublishingEndpoint<EditorialImagePath,EmptyResponse>
{
    public override void Configure(){Get("/publishing/review/submissions/{Id:guid}/revisions/{RevisionId:guid}/images/{AssetId}");Policies("PublishingReviewPolicy");Tags("Publishing review");}
    public override Task HandleAsync(EditorialImagePath req,CancellationToken ct)=>GuardedAsync(async()=>
    {
        var result=await reviews.ImageAsync(req.Id,req.RevisionId,req.AssetId,ct);
        HttpContext.Response.Headers.CacheControl="private, no-store";HttpContext.Response.Headers.XContentTypeOptions="nosniff";
        HttpContext.Response.Headers.ContentSecurityPolicy="default-src 'none'; sandbox";
        await Send.ResultAsync(Results.File(result.Content,result.MediaType,enableRangeProcessing:false));
    });
}
