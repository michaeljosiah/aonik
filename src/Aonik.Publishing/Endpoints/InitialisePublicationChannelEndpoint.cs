using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Endpoints;

internal sealed class InitialisePublicationChannelEndpoint(IPublicationService publications) : PublishingEndpoint<InitialisePublicationChannelRequest,PublicationOperationResponse>
{
    public override void Configure()
    {
        Post("/publishing/management/channels/{ChannelKey}/initialise"); Policies("PublishingPublishPolicy"); Tags("Publishing delivery");
        Options(b=>b.WithMetadata(new RequestSizeLimitAttribute(16*1024)));
    }
    public override Task HandleAsync(InitialisePublicationChannelRequest req,CancellationToken ct)=>GuardedAsync(async()=>
    {
        HttpContext.Response.Headers.CacheControl="private, no-store";
        await Send.OkAsync(await publications.InitialiseAsync(Route<string>("ChannelKey") ?? "",req,ct),ct);
    });
}
