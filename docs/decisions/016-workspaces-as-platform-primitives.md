# ADR-016: A Workspace Is a Platform Primitive; a World Is a Kind of One

**Status:** Proposed (principle + seam; mechanism in Spec 089)
**Date:** 2026-08-01
**Related:** [ADR-005](005-adopt-module-first-modular-monolith.md) (module-first, no cross-module references) · [ADR-011](011-unify-order-spine-into-ordering-layer.md) (middle-layer module precedent) · [ADR-013](013-product-identity-is-configuration.md) (product identity is configuration) · [ADR-015](015-groups-and-sharing-as-platform-primitives.md) (the precedent extraction) · [Spec 086](../specifications/086.extract-groups-and-sharing-to-platform.html) (groups and sharing) · [Spec 087](../specifications/087.subscriptions-entitlements-and-metered-usage.html) (entitlements and metering)

## Context

Arke Studio is a local-first Windows desktop application. Its own specification is unambiguous about
what it is: *"A free, MIT-licensed, local-first desktop application. Worlds are folders of readable
files on the user's own disk… Nothing leaves the machine except approved dispatches."* Its
architectural decision is *"the filesystem as the only durable truth. No cloud backend on the hot
path, no database of record, no git."* Accounts, subscriptions, billing and any cloud backend are
listed **out of scope for v1**.

A commercial cloud tier is now under consideration, offered alongside — not instead of — the free
local product. That changes one thing and only one thing: a world must be able to exist somewhere
other than one person's disk. Everything else the cloud tier needs, the platform already has.

**What already fits.** Authentication across three operator-choice IdPs; tenancy; parties and the
user↔party bridge; subscriptions with plan versions, mandates, renewal and dunning; counter meters
whose reserve/commit lifecycle is the same shape as a media dispatch; ceiling meters for seats; flag
meters for feature gates; orders, invoices and a double-entry ledger; groups with roles and
invitations; and scoped, revocable, single-use sharing.

**What does not.** There is no versioned tree of files anywhere in the platform. `IFileStore` stores
a blob and returns a SHA-256. `Aonik.Documents` classifies, chunks and indexes documents for
retrieval. Neither is a workspace: neither has a path, a revision, a manifest, or a notion of the
same logical file changing over time.

The naïve answer — leave worlds in Arke, let the platform reference them — fails on the sharing
seam. [Spec 086](../specifications/086.extract-groups-and-sharing-to-platform.html) §6 makes a
share grant name a `ResourceKind` plus ids, and requires the owning module to answer *"does this
owner own these ids?"* through an `IShareResourceResolver`. That question is asked on the hot path
of an authorisation check. If worlds live in the product, the platform must call the product over
HTTP to answer it — inverting the dependency, for a product designed to work offline.

The second naïve answer — a module called `Aonik.Worlds` — fails a different test. "World" is
Arke's word. Payabo will never have one. [ADR-013](013-product-identity-is-configuration.md) says
product identity is configuration, not platform code, and
[Spec 086](../specifications/086.extract-groups-and-sharing-to-platform.html) has just finished
demonstrating the cost of the opposite: `Household` in the platform meant the personal-finance
lifecycle contributor treated an Arke Kids *family* as a household, wrote it into
`PersonalProfile.HouseholdId`, and then refused a second family as *"User already belongs to a
household."* That defect took three review rounds to fully close because the vocabulary invited it.

## Decision

**A versioned tree of files owned by a party is a platform primitive.** It is called a
**`Workspace`**, it lives in a middle-layer `Aonik.Workspaces` module referencing only SharedKernel,
and a **world is a `Workspace` whose `Kind` is `world`** — exactly as a household is a `Group` whose
kind is `household`.

### The model

| Entity | Shape |
| --- | --- |
| `Workspace` | `TenantId`, `Kind` (open string: `world`, …), `Name`, `OwnerPartyId`, `HeadRevision`, byte total |
| `WorkspaceRevision` | Monotonic per workspace, `AuthorPartyId`, message, created — one revision is one manifest |
| `WorkspaceFile` | Path (forward-slash, POSIX-normalised), content hash, size, blob key — a manifest row |

Blobs are stored **by content hash** through the existing `IFileStore`, which already returns a
SHA-256 on upload. A revision is a manifest of `path → hash`. Nothing else is needed to make sync
work, and in particular **git is not**, which the Arke specification explicitly forbids.

### The seam

| Question | Answer |
| --- | --- |
| **What does the platform own?** | The container, the tree, revisions, blobs, the sync protocol, sharing, quota, tenancy, soft-delete, subject-access export. |
| **What does the product own?** | What a sheet is, what canon means, ripple, the accept gate, model sheets, productions — all of it operating on **file contents the platform never opens**. |
| **How is that enforced?** | The same rule as `TermsJson` in [Spec 086](../specifications/086.extract-groups-and-sharing-to-platform.html) §6.1: the platform stores it and never reads it. If `Aonik.Workspaces` ever learns what a canon entry is, the seam has failed and the module has become a second copy of the product. |
| **Why is the folder the wire format?** | Because the MIT local application must not gain a dependency on Aonik. It reads and writes folders; a sync client moves them. The local format stays authoritative offline and the cloud holds a mirror of the same tree. |
| **How does sharing work?** | Register `workspace` as a `ShareResourceKind`. The resolver is then a query against the platform's own table rather than a call back into the product — which is the whole reason workspaces belong here. |
| **How is it metered?** | Workspace count as a **ceiling** meter, stored bytes as a **counter** meter. Both are [Spec 087](../specifications/087.subscriptions-entitlements-and-metered-usage.html) primitives that only work if the platform knows how many workspaces exist and how large they are. |
| **Naming** | Platform code says `Workspace`. "World" is product vocabulary and stays in product UIs, per [ADR-013](013-product-identity-is-configuration.md). |

### Which revision is true

**The local tree is authoritative on the machine that holds it. The cloud head is authoritative
between machines.** A remote revision that does not descend from the local head is *not* merged and
*not* silently overwritten — it arrives as a **proposal**.

That is not a new mechanic invented for sync. It is Arke Studio's core mechanic
(*"nothing enters the authored record without a human accept… proposals are ripple-checked against
canon"*) doing second duty. A conflicting remote revision is ripple-checked and accepted exactly as
a locally-drafted change is, which means sync needs no new conflict vocabulary, no merge algorithm,
and no new user interface language.

### Scope discipline (YAGNI)

**In:** the workspace container, content-addressed blobs, revisions as manifests, a pull/push sync
protocol, `workspace` as a share resource kind, count and byte meters.

**Out:** real-time collaborative editing; operational transform or CRDTs; server-side execution of
Arke's job queue; any platform understanding of file *contents*; branching or merge algorithms;
per-file permissions (a grant names a workspace, not a path).

## Consequences

### Positive

- **A second product needs no second sharing implementation.** Invites, single-use tokens, expiry,
  immediate revocation, ownership validation and the anonymous preview are inherited.
- **Quota becomes commercially meaningful on day one.** Storage limits retrofitted onto users who
  already uploaded 200GB are a support problem, not an engineering one.
- **The free product stays free and offline.** Sync is opt-in; the folder is the interchange format;
  nothing in the local domain layer learns about Aonik.
- **Arke Kids inherits it.** Both Arke products have worlds. That is the same two-consumer test that
  put groups in the platform: one consumer is a feature, two is a capability.

### Negative, and accepted

- **Sync is the highest-risk component in the plan**, and everything else here is plumbing. It is
  mitigated by refusing merge (proposals instead) and by refusing real-time collaboration.
- **Media is large.** A world with video takes is plausibly gigabytes. Content addressing dedupes
  well across takes of one shot, but the database must hold pointers and hashes only, never bytes.
- **Two truths exist by construction.** Local and cloud can diverge. This ADR fixes which wins;
  without that rule written down first, every downstream decision is guesswork.
- **A third axis appears.** Arke's specification warns that conflating the authoring path and the
  media path is the main architectural risk it exists to prevent. Local-versus-remote crosses both,
  giving four combinations where the product currently reasons about two. Spec 089 must say which of
  the four v1 supports.

## Alternatives Considered

- **Leave worlds in Arke; the platform references them by id.** Rejected: the share resolver would
  become an HTTP call from platform to product on an authorisation hot path, inverting the
  dependency for a product designed to work offline.
- **Name the module `Aonik.Worlds`.** Rejected: it puts one product's noun in platform code, which
  is the drift [ADR-013](013-product-identity-is-configuration.md) forbids and which
  [Spec 086](../specifications/086.extract-groups-and-sharing-to-platform.html) has just paid for in
  three review rounds.
- **Reuse `Aonik.Documents`.** Rejected: it is built for classification, chunking and retrieval of
  individual documents. It has no path, no revision and no manifest, and bending it to acquire them
  would give one module two incompatible jobs.
- **Depend on git.** Rejected: Arke's specification forbids it explicitly (§2.4, versioning is
  explicit in the world folder). Content addressing gives the dedupe without the dependency.
- **Make the cloud copy the only copy.** Rejected: it deletes the free local-first product, which is
  the thing the commercial tier is meant to sit *alongside*.

## Follow-ups (explicitly not in this ADR)

| Item | Why deferred |
| --- | --- |
| **Offline-verifiable entitlement** | A paid feature must be checkable with no network, and `IEntitlementReader` is a live server read. Signed, cached, expiring entitlement needs its own spec; done casually it yields either a trivially cracked licence or an app that bricks on a flight. |
| **Ledger authority** | Arke keeps its own append-only spend ledger with provider-reported actuals; AONIK's rule 1 is that the ledger is the source of financial truth. If cloud dispatch spends AONIK credits while local dispatch spends the user's own provider key, the reconciliation rule must be written before either is built. |
| **Provider key custody in the cloud** | Locally, keys sit in an app-owned encrypted file under the OS key store. Holding a user's keys server-side is a custody and liability change, not a storage decision. |
| **Server-side media dispatch** | Arke's job queue is local and calls providers directly. Cloud execution is a new subsystem and is where credits are actually consumed, so it must be transactional with metering. |

## See Also

- [ADR-015 — Groups and Sharing Are Platform Primitives](015-groups-and-sharing-as-platform-primitives.md) — the precedent, including the `Group`/`household` naming lesson
- [ADR-011 — Unify the Order Spine into an Ordering Layer](011-unify-order-spine-into-ordering-layer.md) — the middle-layer module pattern `Aonik.Workspaces` follows
- [Spec 086 §6 — Resource-kind polymorphism](../specifications/086.extract-groups-and-sharing-to-platform.html) — the sharing seam a workspace plugs into
- [Spec 087 — Subscriptions, entitlements and metered usage](../specifications/087.subscriptions-entitlements-and-metered-usage.html) — the meters quota is expressed in
