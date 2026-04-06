namespace Aonik.Infrastructure.VectorStore;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// OpenTelemetry metrics for Qdrant vector store operations.
/// Tracks upsert/search performance and embedding service metrics.
/// </summary>
internal sealed class QdrantMetrics : IDisposable
{
    public const string MeterName = "Aonik.VectorStore";
    public const string MeterVersion = "1.0.0";

    private readonly Meter meter;
    public ActivitySource ActivitySource { get; }

    // Histogram metrics (latency tracking)
    private readonly Histogram<long> upsertDurationMs;
    private readonly Histogram<long> searchDurationMs;
    private readonly Histogram<long> embeddingApiDurationMs;

    // Counter metrics
    private readonly Counter<long> searchResultCount;
    private readonly Counter<long> embeddingApiErrorCount;

    public QdrantMetrics()
    {
        meter = new Meter(MeterName, MeterVersion);
        ActivitySource = new ActivitySource(MeterName, MeterVersion);

        // Create histograms for latency tracking (in milliseconds)
        upsertDurationMs = meter.CreateHistogram<long>(
            "qdrant.vector.upsert.duration_ms",
            description: "Duration of vector upsert operations in milliseconds");

        searchDurationMs = meter.CreateHistogram<long>(
            "qdrant.vector.search.duration_ms",
            description: "Duration of vector search operations in milliseconds");

        embeddingApiDurationMs = meter.CreateHistogram<long>(
            "embedding.api.duration_ms",
            description: "Duration of embedding API calls in milliseconds");

        // Create counters
        searchResultCount = meter.CreateCounter<long>(
            "qdrant.vector.search.result_count",
            description: "Number of vectors returned from search");

        embeddingApiErrorCount = meter.CreateCounter<long>(
            "embedding.api.error_count",
            description: "Count of embedding API errors");
    }

    /// <summary>
    /// Record upsert operation duration.
    /// </summary>
    public void RecordUpsertDuration(long durationMs, int vectorCount = 1)
    {
        using var activity = ActivitySource.StartActivity("qdrant.upsert");
        activity?.SetTag("vectors_count", vectorCount);
        activity?.SetTag("duration_ms", durationMs);
        upsertDurationMs.Record(durationMs);
    }

    /// <summary>
    /// Record search operation duration and result count.
    /// </summary>
    public void RecordSearchDuration(long durationMs, int resultCount, string collectionName)
    {
        using var activity = ActivitySource.StartActivity("qdrant.search");
        activity?.SetTag("collection", collectionName);
        activity?.SetTag("result_count", resultCount);
        activity?.SetTag("duration_ms", durationMs);
        
        searchDurationMs.Record(durationMs);
        searchResultCount.Add(resultCount);
    }

    /// <summary>
    /// Record embedding API call duration.
    /// </summary>
    public void RecordEmbeddingApiDuration(long durationMs, int textCount = 1)
    {
        using var activity = ActivitySource.StartActivity("embedding.api");
        activity?.SetTag("text_count", textCount);
        activity?.SetTag("duration_ms", durationMs);
        embeddingApiDurationMs.Record(durationMs);
    }

    /// <summary>
    /// Record embedding API error.
    /// </summary>
    public void RecordEmbeddingApiError(string errorType)
    {
        using var activity = ActivitySource.StartActivity("embedding.api.error");
        activity?.SetTag("error_type", errorType);
        embeddingApiErrorCount.Add(1);
    }

    public void Dispose()
    {
        ActivitySource?.Dispose();
        meter?.Dispose();
    }
}
