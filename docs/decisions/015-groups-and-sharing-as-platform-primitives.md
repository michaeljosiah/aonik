# ADR-015: Groups and Sharing Are Platform Primitives

**Status:** Accepted (principle + shape; mechanism in [Spec 086](../specifications/086.extract-groups-and-sharing-to-platform.html))
**Date:** 2026-07-31
**Related:** [ADR-005](005-adopt-module-first-modular-monolith.md) (module-first, no cross-module references) · [ADR-006](006-extract-personal-finance-module.md) (the precedent extraction) · [ADR-013](013-product-identity-is-configuration.md) (product identity is configuration) · [ADR-014](014-business-type-configuration-packs.md) (packs) · [Spec 020](../specifications/020.payabo-household.md) (Household, as built) · [Spec 048](../specifications/048.circle-entity-scoped-sharing.html) (Circle, as built) · [Spec 086](../specifications/086.extract-groups-and-sharing-to-platform.html) (the extraction)

## Context

AONIK is a multi-product platform. A new product — a family story studio — needs three things on day one:

1. **One tenant, many families.** The product is consumer-scale; a family is a group *inside* a tenant, not a tenant of its own. This is the Simi model (`SIMI_TENANT_ID`), already established.
2. **A member who is not a user.** A child has no login and no account. They are a member of the family and a subject of the product, but they never authenticate.
3. **Scoped sharing outside the group.** A private link to a specific artefact for a grandparent, expiring and single-use, where the sharer never sees the recipient's delivery address.

The platform already has both concepts. Both live in `Aonik.PersonalFinance` and are shaped for finance:

- **`Household` / `HouseholdMember`** ([Spec 020](../specifications/020.payabo-household.md)) — a group of users with roles and an invitation lifecycle.
- **`CircleGrant` / `CircleInvite`** ([Spec 048](../specifications/048.circle-entity-scoped-sharing.html)) — scoped, revocable visibility of one user's records to another, via an opaque single-use invite token.

A second product cannot reach either of them. [ADR-005](005-adopt-module-first-modular-monolith.md) forbids direct cross-module references, and even if it did not, putting `Aonik.PersonalFinance` in the critical path of a children's story product is plainly wrong.

### What the code actually says

The generic/domain split is not where inspection would suggest. Measured:

| | Lines | Domain couplings |
| --- | --- | --- |
| `HouseholdService` | 963 | **Pervasive, at every lifecycle transition.** Writes `PersonalProfile.HouseholdId` on create and invitation-acceptance (92, 103, 202, 296) and clears it on removal (655); unshares owned `PersonalAccount`s and emits `HouseholdAccountUnsharedEvent` (664–665); invalidates the financial life graph (109, 347, 510, 677). |
| `CircleService` | 650 | **Pervasive** — `CareEntity`, `PaymentLog` and `Document` projections throughout. Only the grant/invite lifecycle is generic. |

So neither moves untouched. Household's *lifecycle* is generic — invitation, roles, ownership transfer, race safety — but every transition carries PersonalFinance side effects that must survive the move. Circle is two things in one file.

### Three concrete misfits

Neither entity fits a non-finance product as written:

| Field | Problem |
| --- | --- |
| `HouseholdMember.UserId` (`Guid`, non-nullable) | A child profile has no user. A group member cannot be required to be an authenticated principal. |
| `CircleGrant.EntityIdsJson` | Documented as *"CareEntity ids"*. The resource being shared is hard-typed to one module's entity. |
| `CircleGrant.NoAmounts` | Finance-specific redaction. A platform entity must not carry one domain's redaction flags. |

Also noted: `HouseholdMember.PermissionsJson` is described in `CircleGrant`'s own XML comment as *"legacy … unenforced"*.

### Two facts that make this cheap

**`Party` already models a person without a user.** [`Aonik.Platform/Entities/Party`](../../src/Aonik.Platform/Entities/Party) holds `Party` (open `PartyType`, `DisplayName`, no user coupling), `PartyRelationship`, `PartyConsent`, `PartyAddress` and `PartyContact`. A child is a person party with no login; a guardian edge is a `PartyRelationship`; a delivery address a sharer must not see is a `PartyAddress` on the recipient's party.

**Moving a module costs no data migration** — which is not the same as costing no schema change; see the trade-offs. Every module maps to the same table prefix — [`ModuleTablePrefixes`](../../src/Aonik.SharedKernel/Persistence/ModuleTablePrefixes.cs) sets `Platform = Finance = Ai = Agents = Commerce = "Ank"` — and the runtime schema is `dbo` for all of them. Relocating code between modules changes no table name and touches no row.

## Decision

**Group membership and scoped sharing are platform primitives, not personal-finance features.** They move to a `Groups` vertical slice in `Aonik.Platform`, generalised along exactly three axes and no more.

### The model

| Entity | Shape |
| --- | --- |
| `Group` | `TenantId`, `Kind` (open string: `household`, `family`, …), `Name`, members |
| `GroupMember` | `GroupId`, **`PartyId`**, `Role`, invitation lifecycle fields |
| `ShareGrant` | `OwnerPartyId`, `MemberPartyId?`, `GroupId?`, `Scope`, **`ResourceKind`**, `ResourceIds`, **`TermsJson`**, `Status` |
| `ShareInvite` | opaque 256-bit `Token`, the grant terms, `ExpiresAt`, `Status`, `ConsumedAt`, `GrantId?` |

### Load-bearing sub-decisions

| Question | Decision |
| --- | --- |
| **What is a member?** | A **`Party`**, not a `User`. Users already resolve to parties (`ICurrentPartyResolver`). A child is a party with no user. This is the decision the whole ADR turns on. |
| **What is shared?** | A **`ResourceKind` + ids** pair, not a typed foreign key. `ResourceKind` is an open string (the `OrderType` / `BusinessType` precedent). Each module registers an `IShareResourceResolver` for the kinds it owns; PersonalFinance registers `care-entity`. |
| **Where do domain-specific terms go?** | A **`TermsJson`** blob the *owning module* interprets. The platform stores and returns it and never reads it. `NoAmounts` becomes a PersonalFinance term. |
| **What moves and what stays?** | The **generic lifecycle** moves; **domain side effects and projections stay**, re-attached through the lifecycle contributor. Circle splits: grant/invite mechanics move, the shared-care-entity projection stays in PersonalFinance and consumes the platform grant. |
| **Naming** | Platform code says `Group` / `ShareGrant`. "Circle", "household" and "family" are **product vocabulary** and stay in product UIs, per [ADR-013](013-product-identity-is-configuration.md). |
| **Extension seams** | Two, both following the module-contributed `IEnumerable<T>` DI pattern already used for seeding and provisioning: **`IGroupLifecycleContributor`** — which must both *veto* and *react* (`VetoAsync` + `OnCommittedAsync`, in-transaction), because a refusal-only interface has nowhere to put the profile-link, account-unshare, event and cache-invalidation side effects that removal performs today — and `IShareResourceResolver` (resource-kind resolution). |
| **Wire compatibility** | Existing PersonalFinance routes and DTOs are **unchanged**; their services delegate to the platform. Simi's mobile app and the CLI see nothing. This is what makes the extraction safe to do while Simi is live. |
| **Table names** | **Unchanged in this pass.** Classes are renamed; `ToTable("Households")`, `ToTable("CircleGrants")` and column names are retained by explicit mapping. Renaming is a later, optional migration with no functional content. |
| **A slice, not a module** | Groups is a vertical slice inside `Aonik.Platform`, which already owns the people layer (Identity, Party). Four entities and two services do not warrant their own module. |

### Scope discipline (YAGNI)

**In:** the relocation, the three model changes (party member, resource kind, terms blob), the two seams, contracts in `SharedKernel.Abstractions.Groups`, and wire-compatible delegation from PersonalFinance.

**Out:** nested groups; groups owning resources; per-field ACLs or a permission DSL; cross-tenant sharing; removing `PermissionsJson`; renaming any table or column; and anything Arke Kids specific — the `Guardian` relationship type and verifiable-consent fields on `PartyConsent` are named as follow-ups below, not delivered here.

## Consequences

### Positive

- **A second product can have families without touching PersonalFinance.** That is the whole point, and it is what [ADR-005](005-adopt-module-first-modular-monolith.md) requires.
- **One implementation of invite-token security.** The opaque, single-use, expiring bearer capability in `CircleInvite` is the kind of thing that must exist once. Duplicating it per product is how one copy ends up without expiry.
- **Consent, kinship and addresses come for free.** `PartyConsent`, `PartyRelationship` and `PartyAddress` already exist on the party a member now points at.
- **Household stops being mis-filed.** A 963-line generic membership service and its integration events currently live in a finance module — the `Household*` events are even declared in `SharedKernel/Events/Integration/FinanceEvents.cs`.
- **No data migration.** No table moves and no row moves — every module shares the `Ank` prefix in `dbo`, so relocating code changes no table name. The *schema* delta is larger than an early draft of this ADR claimed: roughly nine operations, including party-id columns added alongside (never replacing) the user-id ones, and — unavoidably — **dropping and replacing the `(TenantId, HouseholdId, UserId)` unique index**, which permits only one NULL per group and would otherwise reject the second member without a login. See [Spec 086 §10.2](../specifications/086.extract-groups-and-sharing-to-platform.html#persistence).

### Trade-offs

- **A refactor across a shipping product.** PersonalFinance is live for Simi. Mitigated by keeping every route and DTO identical and delegating — but the diff is wide (see the inventory in [Spec 086](../specifications/086.extract-groups-and-sharing-to-platform.html)).
- **A two-phase member migration.** `GroupMember.PartyId` lands nullable, is backfilled, and is only made required in a later pass. There is an interval where both `UserId` and `PartyId` exist.
- **Deliberate cruft retained.** `PermissionsJson` stays (unenforced legacy) and column names keep their old spelling behind renamed properties. Both are the low-risk call, and both are debt.
- **`Party` coverage must be verified.** The backfill assumes every existing `HouseholdMember.UserId` resolves to a party. That is a migration precondition, not an assumption.

## Alternatives Considered

- **Leave both in PersonalFinance and let the new module depend on it.** Rejected: violates ADR-005's no-cross-module-reference rule, and makes a finance module a hard dependency of a children's product.
- **Duplicate the concepts in the new module.** Rejected: two implementations of an expiring bearer-token invite is a security liability, and the two would diverge immediately.
- **Put the entities in `SharedKernel`.** Rejected: SharedKernel holds contracts, primitives and integration events — not entities with `DbSet`s and services. Nothing there owns persistence.
- **Create a new `Aonik.Groups` module.** Viable, and the right answer if this grows. Rejected for v1 on size: four entities and two services are a slice. Platform already owns Identity and Party, which is where people-shaped concepts belong.
- **Keep `UserId` and create credential-less "child users".** Rejected: it pollutes the identity store with principals that can never authenticate, invents a login surface for children that the product explicitly does not have, and makes consent and deletion harder to reason about.
- **Model the shared resource as a typed FK per module.** Rejected: it puts every module's entities in the platform's model, which is the coupling this ADR exists to remove.

## Follow-ups (explicitly not in this ADR)

| Item | Why deferred |
| --- | --- |
| A `Guardian` relationship type | `PartyRelationshipTypes` has `Mother`, `Father`, `Child` but no guardian, and `Codes` is a closed validation set. Parental authority is not always parenthood. Belongs with the Kids specs. |
| Verifiable-consent fields on `PartyConsent` | Today it carries `PartyId`, `ConsentType`, `GrantedAt`, `RevokedAt` — no granted-by party, verification method, or terms version. A COPPA-grade consent record needs all three. |
| Making `GroupMember.PartyId` required | Phase two, after the backfill is confirmed in every environment. |
| Renaming tables and columns to match the new class names | Pure churn with no functional content; do it when something else is already touching those tables. |

## See Also

- [ADR-005 — Module-First Modular Monolith](005-adopt-module-first-modular-monolith.md)
- [ADR-006 — Extract PersonalFinance into Its Own Sibling Module](006-extract-personal-finance-module.md)
- [ADR-013 — Product Identity Is Configuration, Not Platform Code](013-product-identity-is-configuration.md)
- [Spec 086 — Extract groups and sharing to Platform](../specifications/086.extract-groups-and-sharing-to-platform.html)
