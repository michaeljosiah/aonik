namespace Aonik.Infrastructure.VectorStore.Qdrant;

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Aonik.Infrastructure.VectorStore.Contracts;
using Microsoft.Extensions.Options;

/// <summary>
/// HTTP client for Qdrant vector store REST API.
/// </summary>
internal class QdrantHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly QdrantConfiguration _config;

    public QdrantHttpClient(HttpClient httpClient, IOptions<QdrantConfiguration> options)
    {
        _httpClient = httpClient;
        _config = options.Value;
    }

    /// <summary>
    /// Create or update a collection in Qdrant.
    /// </summary>
    public async Task CreateCollectionAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            vectors = new
            {
                size = _config.VectorDimensions,
                distance = "Cosine"
            }
        };

        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"/collections/{collectionName}",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            // Collection might already exist - that's OK
            if (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                return;
            }
            throw;
        }
    }

    /// <summary>
    /// Check if collection exists.
    /// </summary>
    public async Task<bool> CollectionExistsAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/collections/{collectionName}",
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>
    /// Upsert a point (vector with payload) into a collection.
    /// </summary>
    public async Task UpsertPointAsync(
        string collectionName,
        string pointId,
        float[] vector,
        Dictionary<string, object>? payload,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            points = new[]
            {
                new
                {
                    id = pointId,
                    vector = vector,
                    payload = payload ?? new Dictionary<string, object>()
                }
            }
        };

        var response = await _httpClient.PutAsJsonAsync(
            $"/collections/{collectionName}/points",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Search for similar vectors in a collection.
    /// </summary>
    public async Task<IEnumerable<QdrantSearchHit>> SearchAsync(
        string collectionName,
        float[] vector,
        int limit,
        float scoreThreshold,
        Dictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            vector = vector,
            limit = limit,
            score_threshold = scoreThreshold,
            with_payload = true,
            filter = filter
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{collectionName}/points/search",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<QdrantSearchResponse>(cancellationToken);
        return result?.Result ?? Enumerable.Empty<QdrantSearchHit>();
    }

    /// <summary>
    /// Delete points from a collection.
    /// </summary>
    public async Task DeletePointsAsync(
        string collectionName,
        IEnumerable<string> pointIds,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            points_selector = new
            {
                points = pointIds.ToList()
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{collectionName}/points/delete",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Retrieve points by payload filter (no vector similarity — exact match scroll).
    /// POST /collections/{name}/points/scroll
    /// </summary>
    public async Task<QdrantScrollResult> ScrollAsync(
        string collectionName,
        Dictionary<string, object> filter,
        int limit = 100,
        string? offset = null,
        bool withPayload = true,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            filter = filter,
            limit = limit,
            // null on the first page; Qdrant resumes (inclusively) at this point id otherwise.
            offset = offset,
            with_payload = withPayload
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{collectionName}/points/scroll",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<QdrantScrollResponse>(cancellationToken);
        return result?.Result ?? new QdrantScrollResult(new List<QdrantScrollPoint>());
    }

    /// <summary>
    /// Update payload fields on existing points without re-uploading vectors.
    /// POST /collections/{name}/points/payload
    /// </summary>
    public async Task SetPayloadAsync(
        string collectionName,
        IEnumerable<string> pointIds,
        Dictionary<string, object> payload,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            payload = payload,
            points = pointIds.ToList()
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{collectionName}/points/payload",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Create payload field indexes for efficient filtering.
    /// PUT /collections/{name}/index
    /// </summary>
    public async Task CreatePayloadIndexAsync(
        string collectionName,
        string fieldName,
        string fieldSchema = "keyword",
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            field_name = fieldName,
            field_schema = fieldSchema
        };

        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"/collections/{collectionName}/index",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // Index might already exist — that's OK
        }
    }

    /// <summary>
    /// Check Qdrant health.
    /// </summary>
    public async Task<bool> HealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Qdrant exposes /livez and /readyz for health probing. Using /readyz
            // aligns the app-side check with ACA readiness probes so collection
            // initialization does not incorrectly treat a healthy instance as down.
            var response = await _httpClient.GetAsync("/readyz", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Polly's AttemptTimeout (or HttpClient.Timeout) elapsed while
            // calling /readyz — treat as "not healthy" instead of letting
            // the cancellation bubble up to the caller as an unhandled
            // exception. The initializer's outer 3-attempt loop will retry
            // and the platform's incident panel won't surface this as an
            // app-level failure (Polly's per-attempt log is enough).
            // We deliberately rethrow when the CALLER cancelled
            // (cancellationToken.IsCancellationRequested == true), e.g.
            // app shutdown, so we don't pretend we observed Qdrant state.
            return false;
        }
    }
}

/// <summary>
/// Qdrant search response model.
/// </summary>
internal record QdrantSearchResponse(List<QdrantSearchHit> Result);

/// <summary>
/// Qdrant search hit (matching point).
/// </summary>
public record QdrantSearchHit(
    string Id,
    float Score,
    [property: JsonPropertyName("payload")]
    Dictionary<string, object>? Payload = null);

/// <summary>
/// Qdrant scroll response model.
/// </summary>
internal record QdrantScrollResponse(QdrantScrollResult? Result);

/// <summary>
/// Qdrant scroll result containing points and optional next page offset.
/// <c>NextPageOffset</c> is the point id to resume from; it is null on the final page.
/// (Modelled as a string because document chunk points use UUID ids; a uint64-id
/// collection would return a numeric offset.)
/// </summary>
internal record QdrantScrollResult(
    List<QdrantScrollPoint>? Points,
    [property: JsonPropertyName("next_page_offset")]
    string? NextPageOffset = null);

/// <summary>
/// A point returned by scroll (no score — not a similarity search).
/// </summary>
public record QdrantScrollPoint(
    string Id,
    [property: JsonPropertyName("payload")]
    Dictionary<string, object>? Payload = null);
