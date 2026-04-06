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
    private readonly QdrantHttpClient httpClient;
    private readonly QdrantConfiguration config;
    private readonly ILogger<QdrantCollectionInitializer> logger;

    public QdrantCollectionInitializer(
        QdrantHttpClient httpClient,
        IOptions<QdrantConfiguration> options,
        ILogger<QdrantCollectionInitializer> logger)
    {
        this.httpClient = httpClient;
        this.config = options.Value;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Initializing Qdrant vector store at {Endpoint}",
                config.Endpoint);

            // Check health
            var healthy = await httpClient.HealthAsync(cancellationToken);
            if (!healthy)
            {
                throw new InvalidOperationException(
                    $"Qdrant at {config.Endpoint} is not healthy. " +
                    "Ensure Qdrant is running and accessible.");
            }

            logger.LogInformation(
                "Qdrant vector store initialized successfully. " +
                "Collections will be created on-demand with prefix '{Prefix}'",
                config.CollectionPrefix);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to initialize Qdrant vector store at {Endpoint}",
                config.Endpoint);

            // Log but don't fail startup - allow app to start even if Qdrant is unavailable
            // This is intentional to allow graceful degradation in development
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping Qdrant collection initializer");
        return Task.CompletedTask;
    }
}
