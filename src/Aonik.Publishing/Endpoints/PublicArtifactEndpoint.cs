using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

using Aonik.Publishing.Services;
using Aonik.SharedKernel.Modules;

namespace Aonik.Publishing.Endpoints;

public sealed record PublicArtifactPath(string ChannelKey,Guid ArtifactId);
internal sealed class PublicArtifactEndpoint(PublicPublicationReader reader) : PublishingEndpoint<PublicArtifactPath,EmptyResponse>
{
    public override void Configure()
    {
        Get("/publishing/channels/{ChannelKey}/assets/{ArtifactId:guid}");AllowAnonymous();Tags("Public Player catalogue");
        Description(b=>b.WithMetadata(new ModuleGateExempt()));
    }
    public override Task HandleAsync(PublicArtifactPath req,CancellationToken ct)=>GuardedAsync(async()=>
    {
        var result=await reader.ArtifactAsync(req.ChannelKey,req.ArtifactId,ct);
        HttpContext.Response.Headers.CacheControl="public, max-age=31536000, immutable";
        HttpContext.Response.Headers.ETag='"'+result.Sha+'"';HttpContext.Response.Headers.XContentTypeOptions="nosniff";
        HttpContext.Response.Headers.ContentSecurityPolicy="default-src 'none'; sandbox";
        await Send.ResultAsync(Results.File(result.Stream,result.Type,result.FileName,enableRangeProcessing:false));
    });
}
