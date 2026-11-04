using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Endpoints;

internal sealed class WithdrawPublishedEditionEndpoint(IPublicationService publications) : PublishingEndpoint<PublicationActionRequest,PublicationOperationResponse>
{
    public override void Configure()
    {
        Post("/publishing/editions/{Id:guid}/withdraw"); Policies("PublishingWithdrawPolicy"); Tags("Publishing delivery");
        Options(b=>b.WithMetadata(new RequestSizeLimitAttribute(16*1024)));
    }
    public override Task HandleAsync(PublicationActionRequest req,CancellationToken ct)=>GuardedAsync(async()=>
    {
        HttpContext.Response.Headers.CacheControl="private, no-store";
        await Send.OkAsync(await publications.WithdrawAsync(Route<Guid>("Id"),req,ct),ct);
    });
}
