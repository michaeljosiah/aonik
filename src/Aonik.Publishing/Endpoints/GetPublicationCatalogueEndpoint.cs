using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Endpoints;

internal sealed class GetPublicationCatalogueEndpoint(IPublicationService publications) : PublishingEndpoint<PublicationCatalogueQuery,PublicationCatalogueResponse>
{
    public override void Configure()
    {
        Get("/publishing/management/channels/{ChannelKey}/catalogue"); Policies("PublishingManagementPolicy"); Tags("Publishing delivery");

    }
    public override Task HandleAsync(PublicationCatalogueQuery req,CancellationToken ct)=>GuardedAsync(async()=>
    {
        HttpContext.Response.Headers.CacheControl="private, no-store";
        await Send.OkAsync(await publications.CatalogueAsync(req.ChannelKey,req.Page,req.PageSize,ct),ct);
    });
}
