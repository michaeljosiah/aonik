using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Cache;

internal class InvalidateCacheSetEndpoint : Endpoint<InvalidateCacheSetRequest, InvalidateCacheSetResponse>
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
        Summary(s =>
        {
            s.Summary = "Invalidate a cache set";
            s.Description = "Clears all entries from the specified cache set, forcing fresh data to be loaded on next access.";
            s.Response(200, "Cache invalidated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("System Administration"));
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
