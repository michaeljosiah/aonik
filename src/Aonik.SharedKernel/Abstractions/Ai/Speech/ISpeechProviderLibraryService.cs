namespace Aonik.SharedKernel.Abstractions.Ai.Speech;

/// <summary>
/// Per-tenant CRUD over the speech provider library. Built-in archetypes (from
/// <see cref="IBuiltInSpeechCatalog"/>) are merged into list responses with
/// <see cref="SpeechProvider.IsBuiltIn"/> = true; tenants clone them to get an editable copy.
///
/// <para>
/// See <c>docs/specifications/024.unified-speech-config-and-composer.md</c> §"Service Surface".
/// </para>
/// </summary>
public interface ISpeechProviderLibraryService
{
    /// <summary>
    /// List every active provider available to the current tenant — built-in archetypes plus
    /// tenant-owned rows. Pass <paramref name="includeDisabled"/> = true to surface disabled rows
    /// (useful for the admin Providers tab; never returned from the runtime resolution path).
    /// </summary>
    Task<IReadOnlyList<SpeechProvider>> ListAsync(
        SpeechProviderType? type = null,
        bool includeDisabled = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve a provider by id. Accepts either a built-in id (<c>built-in:openai-whisper-default</c>)
    /// or a tenant-owned Guid. Returns <c>null</c> if not found or soft-deleted.
    /// </summary>
    Task<SpeechProvider?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new tenant-owned provider. The implementation validates that
    /// <c>request.Config</c> matches <c>(request.Type, request.Vendor)</c> and increments the
    /// tenant's provider counter.
    /// </summary>
    Task<SpeechProvider> CreateAsync(
        CreateSpeechProviderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a tenant-owned provider. Bumps <see cref="SpeechProvider.Version"/>; appends the
    /// previous snapshot to the row's history ring buffer. Built-ins are immutable — passing a
    /// built-in id throws.
    /// </summary>
    Task<SpeechProvider> UpdateAsync(
        Guid id,
        UpdateSpeechProviderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clone a built-in archetype into a tenant-owned editable copy. Pass <paramref name="newDisplayName"/>
    /// = null to default to "<c>{archetype display name} (copy)</c>".
    /// </summary>
    Task<SpeechProvider> CloneBuiltInAsync(
        string builtInId,
        string? newDisplayName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggle status. Setting to <see cref="SpeechProviderStatus.Disabled"/> or
    /// <see cref="SpeechProviderStatus.SoftDeleted"/> is rejected if any active recipe references
    /// the provider; the error response carries
    /// <see cref="SpeechProviderUsage.RecipesUsingThisProvider"/> so the UI can link to the
    /// blockers.
    /// </summary>
    Task<SpeechProvider> SetStatusAsync(
        Guid id,
        SpeechProviderStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only history. Returns the most recent <see cref="SpeechLibraryConstants.HistoryRetentionPerEntity"/>
    /// snapshots in newest-first order.
    /// </summary>
    Task<IReadOnlyList<SpeechProviderHistoryEntry>> GetHistoryAsync(
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recipes (across the tenant's library) that reference this provider. Used by the admin UI
    /// to render the "Used by N recipes" badge and to populate the "blocked by" error response
    /// when a delete is rejected.
    /// </summary>
    Task<SpeechProviderUsage> GetUsageAsync(
        string id,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Built-in archetype catalog. Returns the immutable, in-code list of provider + recipe
/// templates that every tenant can clone. Stable ids prefixed <c>built-in:</c>.
/// </summary>
public interface IBuiltInSpeechCatalog
{
    IReadOnlyList<SpeechProvider> AllProviders { get; }
    SpeechProvider? FindProvider(string builtInId);

    IReadOnlyList<VoiceRecipe> AllRecipes { get; }
    VoiceRecipe? FindRecipe(string builtInId);
}

public sealed record CreateSpeechProviderRequest(
    string DisplayName,
    SpeechProviderType Type,
    string Vendor,
    SpeechProviderConfig Config,
    /// <summary>
    /// Optional plaintext API key. When present it's encrypted at rest and stored on the
    /// provider row, becoming the tenant override in the credential resolver chain. Pass
    /// <c>null</c> to leave the row keyless (admin can fill it in later).
    /// </summary>
    string? ApiKey = null);

public sealed record UpdateSpeechProviderRequest(
    string DisplayName,
    SpeechProviderConfig Config,
    /// <summary>
    /// Tri-state. <c>null</c> = leave the existing credential alone (default for "edit display
    /// name only"). Empty string = clear the stored credential. Non-empty = encrypt + replace.
    /// The wire layer maps a missing JSON property to <c>null</c>.
    /// </summary>
    string? ApiKey = null);

/// <summary>Module-wide tunables.</summary>
public static class SpeechLibraryConstants
{
    /// <summary>Per-row history ring buffer size. Older snapshots roll off.</summary>
    public const int HistoryRetentionPerEntity = 25;

    /// <summary>Reserved id prefix for built-in archetypes. Tenant-owned ids cannot start with this.</summary>
    public const string BuiltInIdPrefix = "built-in:";
}
