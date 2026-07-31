using Aonik.Subscriptions.Contracts.Models;

namespace Aonik.Subscriptions.Contracts.Services;

/// <summary>
/// Authoring what is sold (Spec 087 §6) — meters, plans, plan versions and their entitlements.
/// Module-internal: cross-module consumers read a subscriber's standing through
/// <c>IEntitlementReader</c> and never touch the catalogue directly.
///
/// Two invariants live here rather than in a convention, because both are silent when broken:
/// a published <see cref="PlanVersionResponse"/> can never be edited, and every
/// <c>MeterCode</c> is validated against the tenant's meter table on write.
/// </summary>
public interface ICatalogueService
{
    // ---- Meters ------------------------------------------------------------------------------

    Task<MeterResponse> CreateMeterAsync(CreateMeterRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Display name and unit only. <c>Kind</c> is deliberately not updatable: plans, grants and
    /// usage rows already written against this meter assume its kind, and changing it would
    /// reinterpret history rather than correct it.
    /// </summary>
    Task<MeterResponse> UpdateMeterAsync(Guid meterId, UpdateMeterRequest request, CancellationToken cancellationToken = default);

    Task<MeterResponse?> GetMeterAsync(Guid meterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MeterResponse>> ListMetersAsync(CancellationToken cancellationToken = default);

    // ---- Plans -------------------------------------------------------------------------------

    /// <summary>Creates the plan and its first draft version is added separately.</summary>
    Task<PlanResponse> CreatePlanAsync(CreatePlanRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Presentation only — name, description, ordering. Price and entitlements live on a version,
    /// and <c>Code</c> and <c>BillingInterval</c> are fixed once created because subscriptions
    /// bind to them.
    /// </summary>
    Task<PlanResponse> UpdatePlanAsync(Guid planId, UpdatePlanRequest request, CancellationToken cancellationToken = default);

    Task<PlanResponse?> GetPlanAsync(Guid planId, CancellationToken cancellationToken = default);

    Task<PlanResponse?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanResponse>> ListPlansAsync(bool includeRetired = false, CancellationToken cancellationToken = default);

    /// <summary>Withdraw from sale. Existing subscribers keep the plan and continue to renew on it.</summary>
    Task<PlanResponse> RetirePlanAsync(Guid planId, CancellationToken cancellationToken = default);

    // ---- Versions ----------------------------------------------------------------------------

    /// <summary>
    /// Start a new draft version, numbered one above the plan's current highest. A plan may hold
    /// only one draft at a time — two concurrent drafts would race for the same version number and
    /// leave it ambiguous which becomes current on publication.
    /// </summary>
    Task<PlanVersionResponse> CreateDraftVersionAsync(Guid planId, CreatePlanVersionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replace a draft version's entitlements wholesale. Every <c>MeterCode</c> must resolve in the
    /// tenant's meter table, and each allowance must make sense for that meter's kind.
    /// </summary>
    /// <exception cref="InvalidStateException">The version is not a draft.</exception>
    /// <exception cref="NotFoundException">A meter code does not resolve in this tenant.</exception>
    Task<PlanVersionResponse> SetEntitlementsAsync(Guid planVersionId, SetEntitlementsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a draft, superseding the plan's previous published version. After this the price and
    /// entitlements are frozen for good; a change means a new version.
    /// </summary>
    /// <exception cref="InvalidStateException">The version is not a draft, or has no entitlements.</exception>
    Task<PlanVersionResponse> PublishVersionAsync(Guid planVersionId, CancellationToken cancellationToken = default);

    Task<PlanVersionResponse?> GetVersionAsync(Guid planVersionId, CancellationToken cancellationToken = default);

    /// <summary>The version a new subscription would pin, or null when the plan has never published one.</summary>
    Task<PlanVersionResponse?> GetCurrentVersionAsync(Guid planId, CancellationToken cancellationToken = default);
}
