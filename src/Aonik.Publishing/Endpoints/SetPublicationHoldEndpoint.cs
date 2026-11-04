using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Endpoints;

internal sealed class SetPublicationHoldEndpoint(IPublicationService publications) : PublishingEndpoint<SetPublicationHoldRequest,PublicationOperationResponse>
{
    public override void Configure()
    {
        Post("/publishing/submissions/{Id:guid}/publication/hold"); Policies("PublishingWithdrawPolicy"); Tags("Publishing delivery");
        Options(b=>b.WithMetadata(new RequestSizeLimitAttribute(16*1024)));
    }
    public override Task HandleAsync(SetPublicationHoldRequest req,CancellationToken ct)=>GuardedAsync(async()=>
    {
        HttpContext.Response.Headers.CacheControl="private, no-store";
        await Send.OkAsync(await publications.HoldAsync(Route<Guid>("Id"),req,ct),ct);
    });
}
