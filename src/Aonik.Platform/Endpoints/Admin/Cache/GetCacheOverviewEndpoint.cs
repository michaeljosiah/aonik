using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Cache;

internal class GetCacheOverviewEndpoint : EndpointWithoutRequest<CacheOverviewResponse>
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
        Summary(s =>
        {
            s.Summary = "Get cache overview";
            s.Description = "Returns a summary of all cache sets and their entry counts across the platform.";
            s.Response(200, "Cache overview");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("System Administration"));
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
