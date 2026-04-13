using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.Logging;
using OpenAI.Images;

namespace Aonik.Ai.Services;

/// <summary>
/// Generates images using OpenAI's image generation API (DALL-E / gpt-image).
/// Reads API key and model from <see cref="IAiProviderSettings"/> (database-backed,
/// runtime-configurable via the Settings module).
/// </summary>
public sealed class ContentImageGenerator : IContentImageGenerator
{
    private readonly ImageClient? _imageClient;
    private readonly ILogger<ContentImageGenerator> _logger;

    public ContentImageGenerator(IAiProviderSettings aiSettings, ILogger<ContentImageGenerator> logger)
    {
        _logger = logger;

        if (string.IsNullOrWhiteSpace(aiSettings.OpenAiApiKey))
        {
            _logger.LogWarning("OpenAI API key not configured — image generation disabled.");
            return;
        }

        _imageClient = new ImageClient(aiSettings.OpenAiImageModel, aiSettings.OpenAiApiKey);
        _logger.LogInformation("Image generation enabled with model: {Model}", aiSettings.OpenAiImageModel);
    }

    public bool IsAvailable => _imageClient is not null;

    public async Task<byte[]> GenerateImageAsync(string prompt, int? width = null, int? height = null, CancellationToken cancellationToken = default)
    {
        if (_imageClient is null)
            throw new InvalidOperationException("Image generation is not available. Configure AI:OpenAI:ApiKey.");

        _logger.LogInformation("Generating image for prompt: {Prompt} (requested size: {Width}x{Height})",
            prompt[..Math.Min(prompt.Length, 100)], width, height);

        var options = new ImageGenerationOptions
        {
            Size = ResolveSize(width, height),
            ResponseFormat = GeneratedImageFormat.Bytes,
        };

        var result = await _imageClient.GenerateImageAsync(prompt, options, cancellationToken);
        var imageBytes = result.Value.ImageBytes.ToArray();

        _logger.LogInformation("Image generated successfully ({Size} bytes)", imageBytes.Length);
        return imageBytes;
    }

    /// <summary>
    /// Maps requested dimensions to the closest supported OpenAI image size.
    /// Supported: 1024x1024, 1024x1792 (portrait), 1792x1024 (landscape).
    /// </summary>
    private static GeneratedImageSize ResolveSize(int? width, int? height)
    {
        if (width is null || height is null)
            return GeneratedImageSize.W1024xH1024;

        var ratio = (double)width.Value / height.Value;

        // Landscape (wider than 4:3)
        if (ratio > 1.33)
            return GeneratedImageSize.W1792xH1024;

        // Portrait (taller than 3:4)
        if (ratio < 0.75)
            return GeneratedImageSize.W1024xH1792;

        // Square-ish
        return GeneratedImageSize.W1024xH1024;
    }
}

/// <summary>
/// No-op implementation for when AI provider is Stub or image generation is disabled.
/// </summary>
public sealed class StubContentImageGenerator : IContentImageGenerator
{
    public bool IsAvailable => false;

    public Task<byte[]> GenerateImageAsync(string prompt, int? width = null, int? height = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Image generation is not available in stub mode.");
}
