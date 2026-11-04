using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Endpoints;

internal sealed class RetryPublicationEndpoint(IPublicationService publications) : PublishingEndpoint<PublicationActionRequest,PublicationOperationResponse>
{
    public override void Configure()
    {
        Post("/publishing/publications/{Id:guid}/retry"); Policies("PublishingManagementPolicy"); Tags("Publishing delivery");
        Options(b=>b.WithMetadata(new RequestSizeLimitAttribute(16*1024)));
    }
    public override Task HandleAsync(PublicationActionRequest req,CancellationToken ct)=>GuardedAsync(async()=>
    {
        HttpContext.Response.Headers.CacheControl="private, no-store";
        await Send.OkAsync(await publications.RetryAsync(Route<Guid>("Id"),req,ct),ct);
    });
}
