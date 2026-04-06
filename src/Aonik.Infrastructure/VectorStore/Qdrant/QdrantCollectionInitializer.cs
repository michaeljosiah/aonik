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
}
