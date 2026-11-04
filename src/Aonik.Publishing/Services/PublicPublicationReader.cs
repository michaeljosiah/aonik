using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Publishing.Services;

internal sealed class PublicPublicationReader(IPublishingDataContext db,IPublicationArtifactStore store,
    IOptions<PublishingOptions> options,ITenantProvider tenants)
{
    public async Task<(string Json,string Sha)> CatalogueAsync(string channel,CancellationToken cancellationToken)
    {
        var tenant=Tenant(channel);
        var state=await db.PublicationChannels.AsNoTracking().SingleOrDefaultAsync(s=>s.TenantId==tenant&&s.ChannelKey==channel,cancellationToken)
            ??throw new PublishingException(503,"catalogue-not-ready","This public catalogue has not been qualified and initialised.");
        var snapshot=await db.CatalogueSnapshots.AsNoTracking().SingleAsync(s=>s.TenantId==tenant&&s.ChannelKey==channel&&s.Id==state.HeadSnapshotId,cancellationToken);
        return(snapshot.Json,snapshot.Sha256);
    }
    public async Task<(Stream Stream,string Type,string Sha,string? FileName)> ArtifactAsync(string channel,Guid id,CancellationToken cancellationToken)
    {
        var tenant=Tenant(channel);
        var item=await db.PublicationArtifacts.AsNoTracking().SingleOrDefaultAsync(a=>a.TenantId==tenant&&a.ChannelKey==channel&&a.Id==id&&a.ReadyAt!=null,cancellationToken)
            ??throw PublicationAuthority.Missing();
        if(!await db.PublicationOperations.AnyAsync(o=>o.TenantId==tenant&&o.ChannelKey==channel&&o.Id==item.OperationId&&o.Kind=="publish"&&o.DecisionId!=null,cancellationToken))
            throw PublicationAuthority.Missing();
        return(await store.OpenAsync(item,cancellationToken),item.MediaType,item.Sha256,item.MediaType=="application/zip"?"edition-"+item.Id.ToString("N")+".zip":null);
    }
    private Guid Tenant(string key)
    {
        if(!options.Value.Channels.TryGetValue(key,out var channel)||!channel.PublicCatalogueEnabled||channel.TenantId==Guid.Empty)throw PublicationAuthority.Missing();
        if(!tenants.TryGetCurrentTenantId(out var id)||id!=channel.TenantId)throw new PublishingException(503,"public-scope-unavailable","The server-controlled public channel scope is unavailable.");
        return id;
    }
}
