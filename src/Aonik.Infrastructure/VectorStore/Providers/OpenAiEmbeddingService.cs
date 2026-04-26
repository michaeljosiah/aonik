namespace Aonik.Infrastructure.VectorStore.Providers;

using System.Diagnostics;
using Aonik.Infrastructure.VectorStore.Contracts;
using Aonik.Infrastructure.VectorStore.Qdrant;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Embedding service that delegates to the <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>
/// registered in DI. When <c>AI:Provider</c> is "OpenAI", the generator calls the real
/// OpenAI embeddings API. When "Stub", it returns deterministic mock vectors.
/// </summary>
internal class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly QdrantConfiguration _qdrantConfig;
    private readonly QdrantMetrics _metrics;
    private readonly ILogger<OpenAiEmbeddingService> _logger;

    public string ModelName => _qdrantConfig.EmbeddingModel;

    public int Dimensions => _qdrantConfig.VectorDimensions;

    public OpenAiEmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        IOptions<QdrantConfiguration> qdrantOptions,
        QdrantMetrics metrics,
        ILogger<OpenAiEmbeddingService> logger)
    {
        _generator = generator;
        _qdrantConfig = qdrantOptions.Value;
        _metrics = metrics;
        _logger = logger;

        _logger.LogInformation(
            "Initialized embedding service with model {Model}, {Dimensions} dimensions, generator {Generator}",
            ModelName, Dimensions, generator.GetType().Name);
    }

    public async Task<float[]> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text required", nameof(text));

        using var activity = _metrics.ActivitySource.StartActivity("embedding.api", ActivityKind.Client);
        activity?.SetTag("gen_ai.operation.name", "embeddings");
        activity?.SetTag("gen_ai.request.model", ModelName);
        activity?.SetTag("aonik.embedding.text_count", 1);
        activity?.SetTag("aonik.embedding.dimension_count", Dimensions);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var embedding = await _generator.GenerateAsync(
                text, cancellationToken: cancellationToken);

            sw.Stop();
            _metrics.RecordEmbeddingApiDuration(sw.ElapsedMilliseconds, textCount: 1);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);

            return embedding.Vector.ToArray();
        }
        catch (Exception ex)
        {
            sw.Stop();
            _metrics.RecordEmbeddingApiError(ex.GetType().Name);
            AiTelemetry.MarkError(activity, ex);
            _logger.LogError(ex, "Failed to generate embedding for text ({Length} chars)", text.Length);
            throw;
        }
    }

    public async Task<IEnumerable<float[]>> GetEmbeddingsBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        if (textList.Count == 0)
            throw new ArgumentException("Texts required", nameof(texts));

        using var activity = _metrics.ActivitySource.StartActivity("embedding.api.batch", ActivityKind.Client);
        activity?.SetTag("gen_ai.operation.name", "embeddings");
        activity?.SetTag("gen_ai.request.model", ModelName);
        activity?.SetTag("aonik.embedding.text_count", textList.Count);
        activity?.SetTag("aonik.embedding.dimension_count", Dimensions);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var embeddings = await _generator.GenerateAsync(
                textList, cancellationToken: cancellationToken);

            sw.Stop();
            _metrics.RecordEmbeddingApiDuration(sw.ElapsedMilliseconds, textCount: textList.Count);
            activity?.SetTag("duration_ms", sw.ElapsedMilliseconds);

            _logger.LogDebug("Generated {Count} embeddings in batch ({Elapsed}ms)",
                textList.Count, sw.ElapsedMilliseconds);

            return embeddings.Select(e => e.Vector.ToArray()).ToList();
        }
        catch (Exception ex)
        {
            sw.Stop();
            _metrics.RecordEmbeddingApiError(ex.GetType().Name);
            AiTelemetry.MarkError(activity, ex);
            _logger.LogError(ex, "Failed to generate batch embeddings for {Count} texts", textList.Count);
            throw;
        }
    }
}
