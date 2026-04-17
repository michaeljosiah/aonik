using System.Diagnostics;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services;

/// <summary>
/// Hosted service that issues a single tiny chat completion on startup to
/// establish the provider TLS handshake + any provider-side model caches
/// before the first real user request lands. Without this, the first AG-UI
/// turn per worker pays for the HTTPS handshake cost (typically 200–500 ms)
/// on top of inference time.
/// </summary>
/// <remarks>
/// Skipped when the AI provider is the stub, so unit tests and CI do not
/// accrue real-provider cost. All failures are caught and logged — a failed
/// warm-up must never crash the host.
/// </remarks>
internal sealed class ChatClientWarmupService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChatClientWarmupService> _logger;

    public ChatClientWarmupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ChatClientWarmupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Fire-and-forget so startup is not blocked.
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var services = scope.ServiceProvider;

                var aiSettings = services.GetService<IAiProviderSettings>();
                if (aiSettings is null
                    || string.Equals(aiSettings.Provider, "stub", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug(
                        "[ChatClientWarmupService] skipped (provider={Provider})",
                        aiSettings?.Provider ?? "<null>");
                    return;
                }

                // Seed a system tenant so scoped services that throw when
                // there is no tenant context do not fail inside the warm-up.
                var tenantContext = services.GetService<ITenantContext>();
                if (tenantContext is not null)
                {
                    tenantContext.TenantId = Guid.Empty;
                    tenantContext.ResolutionSource = "system";
                }

                var chatClient = services.GetService<IChatClient>();
                if (chatClient is null)
                {
                    _logger.LogDebug("[ChatClientWarmupService] skipped (no IChatClient registered)");
                    return;
                }

                var stopwatch = Stopwatch.StartNew();

                var options = new ChatOptions
                {
                    MaxOutputTokens = 1,
                };

                await chatClient.GetResponseAsync(
                    [new ChatMessage(ChatRole.User, "ok")],
                    options,
                    cancellationToken);

                stopwatch.Stop();

                _logger.LogInformation(
                    "[ChatClientWarmupService] warmup completed in {ElapsedMs} ms",
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ChatClientWarmupService] warmup failed (non-fatal)");
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
