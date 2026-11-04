using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Endpoints;

internal sealed class ListPublicationChannelsEndpoint(IPublicationService publications) : PublishingEndpointWithoutRequest<IReadOnlyList<PublicationChannelResponse>>
{
    public override void Configure()
    {
        Get("/publishing/management/channels"); Policies("PublishingManagementPolicy"); Tags("Publishing delivery");

    }
    public override Task HandleAsync(CancellationToken ct)=>GuardedAsync(async()=>
    {
        HttpContext.Response.Headers.CacheControl="private, no-store";
        await Send.OkAsync(await publications.ChannelsAsync(ct),ct);
    });
}
