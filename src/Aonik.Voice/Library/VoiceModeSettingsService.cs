using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Voice.Entities;
using Aonik.Voice.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Voice.Library;

/// <summary>
/// Reads/writes <see cref="VoiceModeSettings"/> for the current tenant. The row is a singleton
/// per tenant (PK = tenant id); first write inserts, subsequent writes update. <see cref="GetAsync"/>
/// returns sensible defaults when no row exists yet so the admin UI never has to handle a 404 on
/// first load.
///
/// <para>
/// Validates that <see cref="UpdateVoiceModeSettingsRequest.ActiveRecipeId"/>, when non-null,
/// resolves to an active recipe in the tenant's library. Built-in id resolution still works
/// (the catalog returns null today but the resolver tolerates that).
/// </para>
/// </summary>
internal sealed class VoiceModeSettingsService : IVoiceModeSettingsService
{
    private readonly VoiceDbContext _db;
    private readonly IVoiceRecipeLibraryService _recipes;
    private readonly ITenantProvider _tenant;
    private readonly ICurrentUserProvider _user;
    private readonly IClock _clock;

    public VoiceModeSettingsService(
        VoiceDbContext db,
        IVoiceRecipeLibraryService recipes,
        ITenantProvider tenant,
        ICurrentUserProvider user,
        IClock clock)
    {
        _db = db;
        _recipes = recipes;
        _tenant = tenant;
        _user = user;
        _clock = clock;
    }

    public async Task<VoiceModeSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await LoadAsync(cancellationToken);
        return entity is null ? Defaults() : ToDto(entity);
    }

    public async Task<VoiceModeSettings> UpdateAsync(
        UpdateVoiceModeSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate the referenced recipe before touching the row, so a bad payload doesn't bump
        // RowVersion or audit columns.
        await ValidateRecipeReferenceAsync(request.ActiveRecipeId, cancellationToken);

        var existing = await LoadAsync(cancellationToken);
        if (existing is null)
        {
            existing = new VoiceModeSettingsEntity
            {
                TenantId = _tenant.GetCurrentTenantId(),
                ActiveRecipeId = request.ActiveRecipeId,
                Enabled = request.Enabled,
            };
            _db.VoiceModeSettings.Add(existing);
        }
        else
        {
            existing.ActiveRecipeId = request.ActiveRecipeId;
            existing.Enabled = request.Enabled;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(existing);
    }

    private async Task<VoiceModeSettingsEntity?> LoadAsync(CancellationToken ct)
    {
        var tenantId = _tenant.GetCurrentTenantId();
        return await _db.VoiceModeSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
    }

    private async Task ValidateRecipeReferenceAsync(string? recipeId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(recipeId)) return;

        var recipe = await _recipes.GetAsync(recipeId, ct);
        if (recipe is null)
        {
            throw new SpeechLibraryValidationException(
                $"Recipe '{recipeId}' was not found or is not available in this tenant's library.");
        }

        if (recipe.Status != VoiceRecipeStatus.Active)
        {
            throw new SpeechLibraryValidationException(
                $"Recipe '{recipe.DisplayName}' is not active and can't drive Voice Mode.");
        }
    }

    private VoiceModeSettings Defaults() => new(
        ActiveRecipeId: null,
        Enabled: true,
        UpdatedAt: _clock.UtcNow,
        LastUpdatedByUserId: null);

    private static VoiceModeSettings ToDto(VoiceModeSettingsEntity e) => new(
        ActiveRecipeId: e.ActiveRecipeId,
        Enabled: e.Enabled,
        UpdatedAt: e.UpdatedAt ?? e.CreatedAt,
        LastUpdatedByUserId: e.UpdatedBy ?? e.CreatedBy);
}
