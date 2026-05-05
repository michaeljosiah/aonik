using System.Diagnostics;

using Aonik.Infrastructure.VectorStore.Qdrant;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.Health;

/// <summary>
/// Readiness probe for the Qdrant vector store. Wraps
/// <see cref="QdrantHttpClient.HealthAsync"/> (which hits Qdrant's
/// <c>/readyz</c> endpoint) and surfaces the result as a standard
/// ASP.NET Core health entry.
/// </summary>
/// <remarks>
/// Tagged "ready" so the probe contributes to <c>/health</c>. NOT tagged
/// "live" — a transient Qdrant outage shouldn't make the orchestrator
/// recycle the pod; that's what Qdrant's own liveness probe is for. A
/// long-failing Qdrant instead pulls the pod out of the load balancer
/// via the readiness gate, while leaving the running process alone so
/// recovery is fast.
/// <para>
/// Note: <see cref="QdrantHttpClient"/>'s standard resilience handler
/// already retries the call with a tight per-attempt budget; this probe
/// inherits that behaviour without amplifying it.
/// </para>
/// </remarks>
internal sealed class QdrantHealthCheck : IHealthCheck
{
    private readonly QdrantHttpClient _qdrantClient;
    private readonly ILogger<QdrantHealthCheck> _logger;

    public QdrantHealthCheck(
        QdrantHttpClient qdrantClient,
        ILogger<QdrantHealthCheck> logger)
    {
        _qdrantClient = qdrantClient;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var healthy = await _qdrantClient.HealthAsync(cancellationToken);
            stopwatch.Stop();

            var data = new Dictionary<string, object>
            {
                ["round_trip_ms"] = stopwatch.ElapsedMilliseconds,
                ["endpoint"] = "/readyz",
            };

            if (!healthy)
            {
                _logger.LogWarning(
                    "Qdrant health check returned not-ready in {ElapsedMs}ms",
                    stopwatch.ElapsedMilliseconds);

                // "Degraded" rather than "Unhealthy" because the app remains
                // partially functional without Qdrant — non-RAG paths keep
                // working. The orchestrator's readiness gate decides whether
                // to keep the pod in rotation; ours is to report state.
                return HealthCheckResult.Degraded(
                    description: "Qdrant /readyz reported not-ready.",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                description: $"Qdrant reachable in {stopwatch.ElapsedMilliseconds}ms.",
                data: data);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "Qdrant health check timed out after {ElapsedMs}ms",
                stopwatch.ElapsedMilliseconds);

            return HealthCheckResult.Degraded(
                description: $"Qdrant did not respond within {stopwatch.ElapsedMilliseconds}ms.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "Qdrant health check threw after {ElapsedMs}ms: {Message}",
                stopwatch.ElapsedMilliseconds, ex.Message);

            return HealthCheckResult.Degraded(
                description: ex.Message,
                exception: ex);
        }
    }
}
