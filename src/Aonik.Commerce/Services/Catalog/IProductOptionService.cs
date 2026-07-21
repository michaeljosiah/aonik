using Aonik.Commerce.Contracts.Models.Catalog;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>
/// Authoring and resolution for the tenant option catalogue and its per-product narrowing
/// (Spec 066). Returns DTOs, never entities.
/// </summary>
public interface IProductOptionService
{
    // ─── Authoring ───────────────────────────────────────────────────────────

    Task<OptionGroupDto> CreateGroupAsync(CreateOptionGroupCommand command, CancellationToken cancellationToken = default);

    Task<OptionGroupDto> UpdateGroupAsync(Guid groupId, UpdateOptionGroupCommand command, CancellationToken cancellationToken = default);

    Task<OptionChoiceDto> AddChoiceAsync(Guid groupId, AddOptionChoiceCommand command, CancellationToken cancellationToken = default);

    Task<OptionChoiceDto> UpdateChoiceAsync(Guid choiceId, UpdateOptionChoiceCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically move the group's recommended default to another choice — demote and promote in
    /// one write, serialized by the filtered unique index so two concurrent moves cannot both
    /// commit. Validates every affected product narrowing first (rule V11): a move that would leave
    /// any product's effective default unresolvable is rejected naming those products, rather than
    /// silently dropping the group from their storefront.
    /// </summary>
    Task<OptionGroupDto> SetRecommendedDefaultAsync(Guid groupId, string choiceKey, CancellationToken cancellationToken = default);

    /// <summary>Full-replace a product's narrowing. Idempotent.</summary>
    Task SetProductOptionGroupsAsync(Guid productId, SetProductOptionGroupsCommand command, CancellationToken cancellationToken = default);

    /// <summary>Set or clear a product's per-unit surcharge and its denomination.</summary>
    Task SetUnitSurchargeAsync(Guid productId, SetUnitSurchargeCommand command, CancellationToken cancellationToken = default);

    // ─── Reads ───────────────────────────────────────────────────────────────

    /// <summary>The tenant option catalogue, ordered. Public reads pass
    /// <paramref name="includeInactive"/> = false.</summary>
    Task<IReadOnlyList<OptionGroupDto>> GetCatalogueAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// What one product actually offers: catalogue ∩ narrowing, with each group's effective default
    /// and selection mode resolved (Spec 066 §6). Empty list = not personalisable.
    /// </summary>
    Task<IReadOnlyList<EffectiveOptionGroupDto>> GetEffectiveOptionsAsync(Guid productId, CancellationToken cancellationToken = default);
}
