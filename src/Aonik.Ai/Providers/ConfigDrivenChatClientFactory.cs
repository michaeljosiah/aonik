using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Providers;

/// <summary>
/// Configuration-driven <see cref="IChatClientFactory"/> implementation.
/// Reads the <c>AI:Provider</c> configuration key to determine which
/// <see cref="IChatClient"/> to create.
///
/// Supported providers:
/// <list type="bullet">
///   <item><c>Stub</c> (default) — Returns placeholder responses for development/testing</item>
///   <item><c>OpenAI</c> — OpenAI API (future implementation)</item>
///   <item><c>AzureOpenAI</c> — Azure OpenAI Service (future implementation)</item>
/// </list>
/// </summary>
internal sealed class ConfigDrivenChatClientFactory : IChatClientFactory
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigDrivenChatClientFactory> _logger;

    public ConfigDrivenChatClientFactory(
        IConfiguration configuration,
        ILogger<ConfigDrivenChatClientFactory> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public IChatClient CreateClient()
    {
        var provider = _configuration["AI:Provider"] ?? "Stub";

        _logger.LogInformation("Creating IChatClient for provider: {Provider}", provider);

        return provider.ToLowerInvariant() switch
        {
            "stub" => new StubChatClient(),

            "openai" => throw new NotSupportedException(
                "OpenAI provider is not yet implemented. " +
                "Add the Microsoft.Extensions.AI.OpenAI package and configure AI:OpenAI:ApiKey and AI:OpenAI:Model."),

            "azureopenai" or "azure_openai" or "azure-openai" => throw new NotSupportedException(
                "Azure OpenAI provider is not yet implemented. " +
                "Add the Azure.AI.OpenAI package and configure AI:AzureOpenAI:Endpoint, AI:AzureOpenAI:ApiKey, and AI:AzureOpenAI:DeploymentName."),

            _ => throw new InvalidOperationException(
                $"Unknown AI provider '{provider}'. Supported values: Stub, OpenAI, AzureOpenAI.")
        };
    }
}
