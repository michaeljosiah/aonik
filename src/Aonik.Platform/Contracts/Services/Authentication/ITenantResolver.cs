namespace Aonik.Platform.Contracts.Services.Authentication;

public interface ITenantResolver
{
    // Async so subdomain resolution (a DB lookup) is awaited on the request hot path rather
    // than blocked with .GetAwaiter().GetResult() — sync-over-async there risks thread-pool
    // starvation under load, and this runs for every authenticated request (H16).
    Task<Guid?> ResolveTenantIdAsync(CancellationToken cancellationToken = default);
    Task<Guid?> ResolveFromHttpContextAsync(CancellationToken cancellationToken = default);
}
