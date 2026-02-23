using Aonik.Api.Contracts.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Cache;

public class InvalidateCacheSetEndpoint : Endpoint<InvalidateCacheSetRequest, InvalidateCacheSetResponse>
{
    private readonly ICacheManagementService _cacheManagementService;

    public InvalidateCacheSetEndpoint(ICacheManagementService cacheManagementService)
    {
        _cacheManagementService = cacheManagementService;
    }

    public override void Configure()
    {
        Post("/admin/cache/invalidate");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(InvalidateCacheSetRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CacheSet))
        {
            AddError(r => r.CacheSet, "Cache set is required.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var result = await _cacheManagementService.InvalidateCacheSetAsync(req.CacheSet.Trim(), ct);
        var response = new InvalidateCacheSetResponse(result.CacheSet, result.Invalidated, result.InvalidatedAtUtc);

        await Send.OkAsync(response, ct);
    }
}
