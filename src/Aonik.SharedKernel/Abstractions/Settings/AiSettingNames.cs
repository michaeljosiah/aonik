namespace Aonik.SharedKernel.Abstractions.Settings;

/// <summary>
/// Well-known setting keys consumed by the Ai module. Hosted on
/// SharedKernel because both Ai (which reads them) and Platform (which
/// registers their schema in <c>SettingDefinitions</c>) need to reference
/// the same constants without a cross-module project dependency.
/// </summary>
public static class AiSettingNames
{
    /// <summary>
    /// User memory backend implementation: "SqlServer" (default) or "Qdrant".
    /// When "Qdrant" is selected, all user memory CRUD, audit chains, and semantic
    /// search are handled entirely by the Qdrant vector store.
    /// </summary>
    public const string UserMemoryBackend = "Ai.UserMemory.Backend";

    /// <summary>
    /// AI provider: "Stub" (default), "OpenAI", or "AzureOpenAI".
    /// Controls which LLM, embedding, and image generation backends are active.
    /// </summary>
    public const string Provider = "Ai.Provider";

    /// <summary>
    /// OpenAI API key. Encrypted at rest.
    /// </summary>
    public const string OpenAiApiKey = "Ai.OpenAI.ApiKey";

    /// <summary>
    /// OpenAI chat model (e.g. "gpt-5-mini", "gpt-5").
    /// </summary>
    public const string OpenAiModel = "Ai.OpenAI.Model";

    /// <summary>
    /// OpenAI image generation model (e.g. "dall-e-3", "gpt-image-1").
    /// </summary>
    public const string OpenAiImageModel = "Ai.OpenAI.ImageModel";

    /// <summary>
    /// Enables sensitive input/output capture in AI OpenTelemetry traces.
    /// </summary>
    public const string OpenTelemetryEnableSensitiveData = "Ai.OpenTelemetry.EnableSensitiveData";
}
