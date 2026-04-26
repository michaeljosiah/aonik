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

    private readonly Meter _meter;
    public ActivitySource ActivitySource { get; }

    // Histogram metrics (latency tracking)
    private readonly Histogram<long> _upsertDurationMs;
    private readonly Histogram<long> _searchDurationMs;
    private readonly Histogram<long> _embeddingApiDurationMs;

    // Counter metrics
    private readonly Counter<long> _searchResultCount;
    private readonly Counter<long> _embeddingApiErrorCount;

    public QdrantMetrics()
    {
        _meter = new Meter(MeterName, MeterVersion);
        ActivitySource = new ActivitySource(MeterName, MeterVersion);

        // Create histograms for latency tracking (in milliseconds)
        _upsertDurationMs = _meter.CreateHistogram<long>(
            "qdrant.vector.upsert.duration_ms",
            description: "Duration of vector upsert operations in milliseconds");

        _searchDurationMs = _meter.CreateHistogram<long>(
            "qdrant.vector.search.duration_ms",
            description: "Duration of vector search operations in milliseconds");

        _embeddingApiDurationMs = _meter.CreateHistogram<long>(
            "embedding.api.duration_ms",
            description: "Duration of embedding API calls in milliseconds");

        // Create counters
        _searchResultCount = _meter.CreateCounter<long>(
            "qdrant.vector.search.result_count",
            description: "Number of vectors returned from search");

        _embeddingApiErrorCount = _meter.CreateCounter<long>(
            "embedding.api.error_count",
            description: "Count of embedding API errors");
    }

    /// <summary>
    /// Record upsert operation duration.
    /// </summary>
    public void RecordUpsertDuration(long durationMs, int vectorCount = 1)
    {
        _upsertDurationMs.Record(durationMs);
    }

    /// <summary>
    /// Record search operation duration and result count.
    /// </summary>
    public void RecordSearchDuration(long durationMs, int resultCount, string collectionName)
    {
        _searchDurationMs.Record(durationMs);
        _searchResultCount.Add(resultCount);
    }

    /// <summary>
    /// Record embedding API call duration.
    /// </summary>
    public void RecordEmbeddingApiDuration(long durationMs, int textCount = 1)
    {
        _embeddingApiDurationMs.Record(durationMs);
    }

    /// <summary>
    /// Record embedding API error.
    /// </summary>
    public void RecordEmbeddingApiError(string errorType)
    {
        _embeddingApiErrorCount.Add(1);
    }

    public void Dispose()
    {
        ActivitySource?.Dispose();
        _meter?.Dispose();
    }
}
