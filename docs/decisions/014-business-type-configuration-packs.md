# ADR-014: Business-Type Configuration Packs

**Status:** Accepted (principle + shape; mechanism in [Spec 065](../specifications/065.business-type-configuration-packs.html), first instance in [Spec 064](../specifications/064.debrand-product-identity-from-platform-code.html))
**Date:** 2026-07-14
**Related:** [ADR-013](013-product-identity-is-configuration.md) (the principle this delivers) · [Spec 065](../specifications/065.business-type-configuration-packs.html) (the mechanism) · [Spec 064](../specifications/064.debrand-product-identity-from-platform-code.html) (the Simi pack, first instance) · [ADR-005](005-adopt-module-first-modular-monolith.md) (module-first) · [Spec 033](../specifications/033.tenant-managed-agent-extensibility.html) (the `Agent` per-tenant override)

## Context

[ADR-013](013-product-identity-is-configuration.md) established that a product's identity is **configuration, not platform code**. But configuration with no *unit of delivery* is just scattered rows — there is no repeatable way to stand up "a Simi tenant" or "a food-commerce tenant". Spec 064 even referred to moving Simi's persona into "the Simi tenant seed", a thing that does not exist.

A survey of the seeding and provisioning machinery found the platform is **close on mechanics, absent on the concept**:

- Three module-contributed seed seams exist, all `IEnumerable<T>` DI (each module advertises its slice): `IGlobalSeedContributor`, `IDemoSeedContributor` (phased via `DemoSeedPhase`, idempotent, **reversible**), and `ITenantProvisioningContributor`.
- `TenantProvisioner` is **one-size-fits-all**: every tenant gets the same five roles, the same chart of accounts, the same policies. `TenantProvisioningContext` (a `record`) carries **no tenant type**.
- `Tenant` and `CreateTenantRequest` have **no type**. The demo seed is a closed enum of two payments-vertical types (`BillCollection`, `CrossBorderPayments`) and **mixes configuration with sample content**.
- **Commerce contributes nothing** to provisioning — a maker-ops tenant provisions bare (generic roles + a ledger, no units, no categories, no catalogue).
- Prior art exists at the wrong level: `PayaboSetupProfile` is exactly this idea, but **user-scoped** — a profile that drives what gets set up.

## Decision

Introduce **business-type configuration packs**. A tenant carries a **business type**, and provisioning applies the pack for that type. A pack has **two separable layers** — and that separation is the thing that does not exist today:

| Layer | Contains | Applied | Reaches |
| --- | --- | --- | --- |
| **Config pack** | enabled modules + feature flags, roles, **agent overrides** (persona, toolset, model), branding settings, reference data (units, categories) | at **provision**, keyed by type | **every** tenant of that type, incl. production |
| **Sample pack** | example entities, demo users, seeded activity | on demand (**demo toggle**) | demo / trial / dev **only** |

Load-bearing sub-decisions:

| Question | Decision |
| --- | --- |
| **Business type** | An **open string** on `Tenant` + `CreateTenantRequest` + `TenantProvisioningContext`, additive like `OrderType`, with a known-values helper. Unset = a generic base tenant. |
| **Delivery** | **Declarative-first.** A pack's generic config (settings/flags, agent overrides, branding, reference data) is a **JSON manifest per type** — *data, not code* — applied by a generic applier through the existing tenant-scoped stores (`Setting`, the `Agent` override, `ReferenceDataItem`). Module resources that cannot be pure data are emitted by the **existing `ITenantProvisioningContributor`s, now type-aware**. Extend the seams; do not fork them. |
| **Platform code stays generic** | The applier and contributors never branch on a specific product (`if (type == "simi")` is banned, per ADR-013). The manifest *names* the product — which is exactly where identity belongs (config), not in code. |
| **The split is mandatory** | Config applies to production tenants; sample content never does. The sample layer generalises the current `DemoSeed` from a 2-value enum to the open business type, reusing the phased / idempotent / reversible pipeline. |
| **Idempotent + versioned** | Packs carry a version; re-applying a newer pack **applies-missing and preserves admin edits** (extend `SettingsSeedService`'s existing "never overwrite admin-edited values" rule to tenant scope). |

### Scope discipline (YAGNI)

v1 = the business type + the config/sample split + a generic manifest applier + the **Simi** and **food-commerce** packs + Commerce's first provisioning contribution. **Not** in v1: operator/partner-authored packs, a pack marketplace, a config DSL, or event-driven provisioning (`TenantProvisionedEvent` stays a *future* async option; v1 applies synchronously through the existing `TenantProvisioner`).

## Consequences

### Positive
- **ADR-013 becomes operational.** "The Simi tenant seed" now has a real home — the Simi config pack (Spec 064's first instance) — so the de-brand has somewhere to land.
- **Commerce gets a provisioning story.** The food-commerce pack replaces "provisions bare"; the pack is the forcing function to add Commerce's first `ITenantProvisioningContributor`.
- **New products onboard by authoring a pack** (mostly data), not by branching platform code — the platform's actual promise.
- **The demo-seed conflation ends.** You can stand up a *real* tenant of a type with no demo rows.

### Trade-offs
- A new packaging layer + the `BusinessType` field (a small migration) + generalising the demo enum. Real but bounded.
- Packs must be maintained as products evolve; an un-versioned or stale pack drifts from what tenants actually run.

## Alternatives Considered
- **Branch inside contributors per product (no pack abstraction).** Rejected for a multi-product platform with a growing set of business types: it scatters identity back into code (the ADR-013 anti-pattern) and gives no config/sample split.
- **Just extend the demo seed to more types.** Rejected: demo mixes config with content and is a closed enum. The fix *is* the split + the open type — i.e. this ADR.
- **Event-driven provisioning now (light up `TenantProvisionedEvent`).** Deferred: provisioning is synchronous and contributor-orchestrated today; rearchitecting to events is orthogonal and speculative for v1.

## See Also
- [ADR-013 — Product identity is configuration, not platform code](013-product-identity-is-configuration.md)
- [Spec 065 — Business-type configuration packs](../specifications/065.business-type-configuration-packs.html) (the mechanism)
- [Spec 064 — Extract product identity ("Simi") from platform code](../specifications/064.debrand-product-identity-from-platform-code.html) (the Simi pack)
- [ADR-005](005-adopt-module-first-modular-monolith.md) · [Spec 033](../specifications/033.tenant-managed-agent-extensibility.html)
