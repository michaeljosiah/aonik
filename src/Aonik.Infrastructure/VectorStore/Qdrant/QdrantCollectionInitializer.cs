namespace Aonik.Infrastructure.VectorStore.Qdrant;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Initializes Qdrant collections on application startup.
/// Creates collections lazily as they are accessed (actual creation in QdrantVectorStore).
/// This service ensures the Qdrant endpoint is healthy before the app proceeds.
/// </summary>
internal class QdrantCollectionInitializer : IHostedService
{
    private readonly QdrantHttpClient _httpClient;
    private readonly QdrantConfiguration _config;
    private readonly ILogger<QdrantCollectionInitializer> _logger;

    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public QdrantCollectionInitializer(
        QdrantHttpClient httpClient,
        IOptions<QdrantConfiguration> options,
        ILogger<QdrantCollectionInitializer> logger)
    {
        _httpClient = httpClient;
        _config = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Initializing Qdrant vector store at {Endpoint}",
                _config.Endpoint);

            // Retry health check to handle container startup race
            var healthy = false;
            for (var attempt = 1; attempt <= MaxRetries; attempt++)
            {
                healthy = await _httpClient.HealthAsync(cancellationToken);
                if (healthy) break;

                _logger.LogWarning(
                    "Qdrant health check attempt {Attempt}/{MaxRetries} failed, retrying in {Delay}s",
                    attempt, MaxRetries, RetryDelay.TotalSeconds);

                await Task.Delay(RetryDelay, cancellationToken);
            }

            if (!healthy)
            {
                _logger.LogError(
                    "Qdrant at {Endpoint} is not healthy after {MaxRetries} attempts. " +
                    "Vector store operations will fail until Qdrant becomes available.",
                    _config.Endpoint, MaxRetries);
                return;
            }

            // Pre-create the user-memory collection with payload indexes for efficient filtering
            await EnsureUserMemoryCollectionAsync(cancellationToken);

            _logger.LogInformation(
                "Qdrant vector store initialized successfully. " +
                "Collections will be created on-demand with prefix '{Prefix}'",
                _config.CollectionPrefix);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Qdrant initialization cancelled during shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to initialize Qdrant vector store at {Endpoint}",
                _config.Endpoint);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Qdrant collection initializer");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Pre-create the user-memory collection with payload field indexes
    /// for efficient tenant+user+key+superseded_by filtering.
    /// </summary>
    private async Task EnsureUserMemoryCollectionAsync(CancellationToken cancellationToken)
    {
        var collectionName = _config.GetCollectionName("user-memory");

        try
        {
            var exists = await _httpClient.CollectionExistsAsync(collectionName, cancellationToken);
            if (!exists)
            {
                _logger.LogInformation("Creating user-memory collection {Collection}", collectionName);
                await _httpClient.CreateCollectionAsync(collectionName, cancellationToken);
            }

            // Create payload indexes for efficient filtered scroll/search
            await _httpClient.CreatePayloadIndexAsync(collectionName, "tenant_id", "keyword", cancellationToken);
            await _httpClient.CreatePayloadIndexAsync(collectionName, "user_id", "keyword", cancellationToken);
            await _httpClient.CreatePayloadIndexAsync(collectionName, "key", "keyword", cancellationToken);
            await _httpClient.CreatePayloadIndexAsync(collectionName, "superseded_by", "keyword", cancellationToken);
            await _httpClient.CreatePayloadIndexAsync(collectionName, "entry_type", "keyword", cancellationToken);

            _logger.LogInformation("User-memory collection {Collection} initialized with indexes", collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to initialize user-memory collection {Collection}. " +
                "It will be created on-demand when first accessed.",
                collectionName);
        }
    }
}
