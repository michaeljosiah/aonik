namespace Aonik.SharedKernel.Events.Integration;

// ── Platform-originated integration events ──────────────────────────────────
// Published by the Platform module. Other modules (Finance, AI, etc.)
// subscribe to these to react to platform-level changes.
// Handlers are NOT wired up yet — event types are defined here for future use.

/// <summary>
/// Raised after a tenant has been fully provisioned (identity, roles, settings, seed data).
/// Finance module will subscribe to create default ledgers, chart of accounts, fee policies.
/// </summary>
public record TenantProvisionedEvent(
    Guid TenantId,
    string TenantName,
    string BaseCurrency) : ITenantScopedEvent;

/// <summary>
/// Raised when a new Party (Person or Business) is created.
/// Finance module will subscribe to maintain its PartyReadModel projection.
/// </summary>
public record PartyCreatedEvent(
    Guid TenantId,
    Guid PartyId,
    string PartyType,
    string DisplayName,
    string? Email) : ITenantScopedEvent;

/// <summary>
/// Raised when key Party details are updated (name, status, KYC level, etc.).
/// Finance module will subscribe to keep its PartyReadModel in sync.
/// </summary>
public record PartyUpdatedEvent(
    Guid TenantId,
    Guid PartyId,
    string DisplayName,
    string? Email,
    string? KycStatus) : ITenantScopedEvent;

/// <summary>
/// Raised when a user's role or permission set changes.
/// Modules with cached permission state can invalidate their caches.
/// </summary>
public record UserPermissionsChangedEvent(
    Guid TenantId,
    Guid UserId) : ITenantScopedEvent;

/// <summary>
/// Raised after a host admin changes a tenant's module enablement (Spec 097 §9, §14). Published on
/// every write so a Platform handler in each host removes that tenant's cached module set; it is
/// also the hook for anything that must react to a module coming on or going off.
/// </summary>
/// <param name="TenantId">The tenant whose module state changed.</param>
/// <param name="Enabled">Catalogue ids switched on by this write.</param>
/// <param name="Disabled">Catalogue ids switched off by this write.</param>
/// <param name="ChangedBy">The acting user, when known.</param>
public record TenantModulesChangedEvent(
    Guid TenantId,
    IReadOnlyList<string> Enabled,
    IReadOnlyList<string> Disabled,
    Guid? ChangedBy) : ITenantScopedEvent;
