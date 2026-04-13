namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Resolved AI provider settings for the current scope.
/// Read from the Settings module (database-backed, encrypted, scoped),
/// with fallback to IConfiguration for backward compatibility.
/// <para>
/// Register as scoped — settings are resolved once per request scope.
/// </para>
/// </summary>
public interface IAiProviderSettings
{
    /// <summary>AI provider: "Stub", "OpenAI", or "AzureOpenAI".</summary>
    string Provider { get; }

    /// <summary>OpenAI API key (decrypted). Null if not configured.</summary>
    string? OpenAiApiKey { get; }

    /// <summary>OpenAI chat model (e.g. "gpt-5-mini").</summary>
    string OpenAiModel { get; }

    /// <summary>OpenAI image generation model (e.g. "dall-e-3").</summary>
    string OpenAiImageModel { get; }
}
