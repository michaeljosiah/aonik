using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.VectorStore;

/// <summary>
/// Singleton holder for the resolved <c>Ai.UserMemory.Backend</c> setting
/// value. Hydrated once at startup via
/// <see cref="UserMemoryBackendSelectionExtensions.InitializeUserMemoryBackendAsync"/>
/// so the per-request <c>IUserMemoryService</c> factory can pick the right
/// concrete implementation without blocking a thread-pool thread on a
/// sync-over-async DB lookup.
/// </summary>
/// <remarks>
/// Trade-off: changing the backend at runtime now requires a process
/// restart. That matches operational reality — switching the user-memory
/// backend swaps the entire storage layer, which already requires a
/// rolling restart of the API to take effect.
/// </remarks>
public sealed class UserMemoryBackendSelection
{
    /// <summary>The default backend when no setting / configuration is supplied.</summary>
    public const string DefaultBackend = "SqlServer";

    /// <summary>The Qdrant backend identifier (case-insensitive in checks).</summary>
    public const string QdrantBackend = "Qdrant";

    private string _backendName = DefaultBackend;
    private bool _isHydrated;

    /// <summary>The resolved backend name (e.g. "SqlServer" or "Qdrant").</summary>
    public string BackendName => _backendName;

    /// <summary><c>true</c> when the resolved backend is Qdrant.</summary>
    public bool IsQdrant => _backendName.Equals(QdrantBackend, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>true</c> once <see cref="UserMemoryBackendSelectionExtensions.InitializeUserMemoryBackendAsync"/>
    /// has populated the value at startup. Used in diagnostics; the factory
    /// does not gate on this because the default is a safe fallback.
    /// </summary>
    public bool IsHydrated => _isHydrated;

    /// <summary>
    /// Sets the resolved backend name. Called once during startup
    /// hydration; subsequent calls overwrite, which is fine because the
    /// initialiser only runs once before the app accepts traffic.
    /// </summary>
    internal void Set(string backendName)
    {
        if (string.IsNullOrWhiteSpace(backendName))
        {
            backendName = DefaultBackend;
        }

        _backendName = backendName;
        _isHydrated = true;
    }
}

/// <summary>
/// Startup extension that resolves the <c>Ai.UserMemory.Backend</c>
/// setting once and pins the choice into the singleton
/// <see cref="UserMemoryBackendSelection"/>.
/// </summary>
public static class UserMemoryBackendSelectionExtensions
{
    /// <summary>
    /// Resolves the backend setting once via the async <see cref="ISettingProvider"/>
    /// and caches it on the singleton. Call after <c>builder.Build()</c>
    /// but before <c>app.Run()</c>.
    /// </summary>
    public static async Task InitializeUserMemoryBackendAsync(this IHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<UserMemoryBackendSelection>>();

        var selection = services.GetRequiredService<UserMemoryBackendSelection>();

        try
        {
            var settingProvider = services.GetRequiredService<ISettingProvider>();
            var backend = await settingProvider.GetAsync(AiSettingNames.UserMemoryBackend)
                ?? UserMemoryBackendSelection.DefaultBackend;

            selection.Set(backend);

            logger.LogInformation(
                "User-memory backend resolved: {Backend} (settingKey={SettingKey})",
                backend,
                AiSettingNames.UserMemoryBackend);
        }
        catch (Exception ex)
        {
            // Don't block startup on a settings lookup failure (e.g. SQL
            // outage during a cold start). The singleton's default value
            // (SqlServer) keeps the API serving requests with the safe
            // backend; a subsequent restart will retry resolution.
            logger.LogWarning(
                ex,
                "Failed to resolve user-memory backend setting; falling back to {Default}",
                UserMemoryBackendSelection.DefaultBackend);
            selection.Set(UserMemoryBackendSelection.DefaultBackend);
        }
    }
}
