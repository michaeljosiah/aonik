using Aonik.Api.Contracts.Settings;
using Aonik.Application.Abstractions.Settings;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Cache;

public class GetCacheOverviewEndpoint : EndpointWithoutRequest<CacheOverviewResponse>
{
    private readonly ICacheManagementService _cacheManagementService;

    public GetCacheOverviewEndpoint(ICacheManagementService cacheManagementService)
    {
        _cacheManagementService = cacheManagementService;
    }

    public override void Configure()
    {
        Get("/admin/cache");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _cacheManagementService.GetOverviewAsync(ct);
        var response = new CacheOverviewResponse(
            result.CacheSets.Select(cacheSet => new CacheSetSummaryResponse(cacheSet.Name, cacheSet.EntryCount)).ToArray(),
            result.TotalCacheSets,
            result.TotalEntries);

        await Send.OkAsync(response, ct);
    }
}
