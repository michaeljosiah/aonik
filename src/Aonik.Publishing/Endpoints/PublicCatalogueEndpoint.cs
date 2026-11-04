using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using Aonik.Publishing.Services;
using Aonik.SharedKernel.Modules;

namespace Aonik.Publishing.Endpoints;

public sealed record PublicCataloguePath(string ChannelKey);
internal sealed class PublicCatalogueEndpoint(PublicPublicationReader reader) : PublishingEndpoint<PublicCataloguePath,EmptyResponse>
{
    public override void Configure()
    {
        Get("/publishing/channels/{ChannelKey}/catalogue.json");AllowAnonymous();Tags("Public Player catalogue");
        // Previously committed public snapshots must survive a pause of the tenant's authoring module.
        Description(b=>b.WithMetadata(new ModuleGateExempt()));
    }
    public override Task HandleAsync(PublicCataloguePath req,CancellationToken ct)=>GuardedAsync(async()=>
    {
        var result=await reader.CatalogueAsync(req.ChannelKey,ct);
        HttpContext.Response.Headers.CacheControl="public, max-age=60";HttpContext.Response.Headers.ETag='"'+result.Sha+'"';
        HttpContext.Response.Headers.XContentTypeOptions="nosniff";
        await Send.ResultAsync(Results.Text(result.Json,"application/json",System.Text.Encoding.UTF8));
    });
}
