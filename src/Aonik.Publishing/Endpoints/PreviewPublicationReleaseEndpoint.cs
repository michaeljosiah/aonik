using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Endpoints;

internal sealed class PreviewPublicationReleaseEndpoint(IPublicationService publications) : PublishingEndpoint<SubmissionPath,PublicationReleasePreview>
{
    public override void Configure()
    {
        Get("/publishing/submissions/{Id:guid}/publication"); Policies("PublishingManagementPolicy"); Tags("Publishing delivery");

    }
    public override Task HandleAsync(SubmissionPath req,CancellationToken ct)=>GuardedAsync(async()=>
    {
        HttpContext.Response.Headers.CacheControl="private, no-store";
        await Send.OkAsync(await publications.PreviewAsync(req.Id,ct),ct);
    });
}
