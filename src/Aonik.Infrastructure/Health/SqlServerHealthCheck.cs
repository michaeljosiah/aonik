using System.Diagnostics;

using Aonik.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.Health;

/// <summary>
/// Readiness probe for the canonical SQL Server connection. A successful
/// <see cref="DatabaseFacade.CanConnectAsync"/> against
/// <see cref="AonikDbContext"/> proves both that the network path is open
/// and that EF Core can hand out a connection from the pool — i.e. the
/// app is ready to serve requests that touch the database.
/// </summary>
/// <remarks>
/// Tagged "ready" (and "db") so the probe contributes to <c>/health</c>
/// (readiness) but not <c>/alive</c> (liveness). A SQL outage is a
/// reason to take the pod out of the load balancer; it is NOT a reason
/// for the orchestrator to recycle it — the database is external state.
/// Latency is included in the result data so dashboards can graph
/// per-probe round-trip time.
/// </remarks>
internal sealed class SqlServerHealthCheck : IHealthCheck
{
    private readonly AonikDbContext _dbContext;
    private readonly ILogger<SqlServerHealthCheck> _logger;

    public SqlServerHealthCheck(
        AonikDbContext dbContext,
        ILogger<SqlServerHealthCheck> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            stopwatch.Stop();

            var data = new Dictionary<string, object>
            {
                ["round_trip_ms"] = stopwatch.ElapsedMilliseconds,
                ["context"] = nameof(AonikDbContext),
            };

            if (!canConnect)
            {
                _logger.LogWarning(
                    "SQL health check returned CanConnect=false in {ElapsedMs}ms",
                    stopwatch.ElapsedMilliseconds);

                return HealthCheckResult.Unhealthy(
                    description: "SQL Server reported it could not accept a connection.",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                description: $"SQL Server reachable in {stopwatch.ElapsedMilliseconds}ms.",
                data: data);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Per-probe timeout fired (the orchestrator's HTTP probe budget,
            // typically 10 s). Surface as Unhealthy so /health flips to 503;
            // a real shutdown cancellation is rethrown above.
            stopwatch.Stop();
            _logger.LogWarning(
                "SQL health check timed out after {ElapsedMs}ms",
                stopwatch.ElapsedMilliseconds);

            return HealthCheckResult.Unhealthy(
                description: $"SQL Server did not respond within {stopwatch.ElapsedMilliseconds}ms.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "SQL health check threw after {ElapsedMs}ms: {Message}",
                stopwatch.ElapsedMilliseconds, ex.Message);

            return HealthCheckResult.Unhealthy(
                description: ex.Message,
                exception: ex);
        }
    }
}
