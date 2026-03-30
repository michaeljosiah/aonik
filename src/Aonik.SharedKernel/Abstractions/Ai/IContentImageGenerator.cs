namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Generates images from text prompts using an AI image model.
/// </summary>
public interface IContentImageGenerator
{
    /// <summary>
    /// Generate an image from a text prompt and return the raw PNG bytes.
    /// </summary>
    /// <param name="prompt">The text prompt describing the image to generate.</param>
    /// <param name="width">Desired image width in pixels (null = provider default).</param>
    /// <param name="height">Desired image height in pixels (null = provider default).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<byte[]> GenerateImageAsync(string prompt, int? width = null, int? height = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether image generation is available (i.e. an API key and model are configured).
    /// </summary>
    bool IsAvailable { get; }
}
