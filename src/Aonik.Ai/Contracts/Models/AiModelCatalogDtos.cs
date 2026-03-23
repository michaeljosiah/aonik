namespace Aonik.Ai.Contracts.Models;

public sealed record AiCatalogModelProviderResponse
{
    public required string ModelProviderKey { get; init; }
    public required string Name { get; init; }
    public string? DocumentationUrl { get; init; }
    public string? SdkPackage { get; init; }
    public string? ApiBaseUrl { get; init; }
    public required IReadOnlyList<string> EnvironmentVariables { get; init; }
    public required int ModelCount { get; init; }
}

public sealed record AiCatalogModelResponse
{
    public required string ModelProviderKey { get; init; }
    public required string ModelKey { get; init; }
    public required string Name { get; init; }
    public string? Family { get; init; }
    public required int ContextWindow { get; init; }
    public required int OutputTokenLimit { get; init; }
    public required string CostProfileJson { get; init; }
    public required IReadOnlyList<string> InputModalities { get; init; }
    public required IReadOnlyList<string> OutputModalities { get; init; }
    public required bool SupportsReasoning { get; init; }
    public required bool SupportsToolCall { get; init; }
    public required bool SupportsStructuredOutput { get; init; }
    public required bool SupportsAttachments { get; init; }
    public required bool IsOpenWeights { get; init; }
}

public sealed record ImportAiCatalogModelProviderRequest
{
    public bool ImportModelsAsInactive { get; init; } = true;
}

public sealed record ImportAiCatalogModelProviderResponse
{
    public required Guid AiProviderId { get; init; }
    public required string ModelProviderKey { get; init; }
    public required string ProviderName { get; init; }
    public required bool ProviderCreated { get; init; }
    public required int ModelsCreated { get; init; }
    public required int ModelsLinked { get; init; }
    public required int ModelsSkipped { get; init; }
}
