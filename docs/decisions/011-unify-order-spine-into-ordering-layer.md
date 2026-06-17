# ADR-011: Unify the Order Spine into a Shared Ordering Layer (Order as a Core Concept)

**Status**: Accepted — Phase 1 landed (generalise in place); Phases 2–4 pending
**Date**: 2026-06-17
**Decision Makers**: Development Team
**Related**: [ADR-005](005-adopt-module-first-modular-monolith.md), [ADR-006](006-extract-personal-finance-module.md), [ADR-002](002-anemic-domain-model.md), [Spec 041](../specifications/041.unified-order-spine-ordering-layer.html), [Spec 005](../specifications/005.orders-intent.md)

## Context

AONIK is about to grow a new product surface: a B2C commerce capability (selling physical wellness-food products) that wants to reuse the platform's agentic, multi-tenant, audited foundations. The natural shape for that capability is a **`Aonik.Commerce`** module — but it immediately collides with where the `Order` concept lives today.

`Order` currently lives **inside `Aonik.Finance`** (`src/Aonik.Finance/Entities/Orders/`). Its doctrine (CLAUDE.md, [Spec 005](../specifications/005.orders-intent.md)) frames it narrowly as *"the canonical record of a customer's intent to use an AONIK-powered financial service"* — bill payment, transfer, remittance. Yet the **entity itself is already generic**:

- `Order.OrderType` is an open `string` (the `OrderType` enum is only a known-values helper), so new order kinds are additive, not a schema change.
- `OrderItem.DetailsJson` is an explicit type-discriminated container — bill-payment lines store meter numbers, remittance lines store recipient details.
- `OrderFulfilmentRef` already fans out to multiple fulfilment kinds (`PayoutId` / `PaymentIntentId` / `PartnerBillPaymentId`) under a "exactly one set" CHECK.
- Orders already link to `Invoice` (`Invoice.OrderId`) and to `PaymentIntent` (`OrderFundingRef`) — they are *already* "invoiced and paid for".

A product purchase is, structurally, just another `OrderType`: a customer's intent to transact, with line items, parties, funding, fulfilment, and a lifecycle. Notably the doctrine's own **"Not Orders"** exclusions are *imported bank transactions, manual categorisations, and ledger corrections* — a product purchase is not on that list.

The blocker is **ownership and layering**, not the entity shape. The modular-monolith rule ([ADR-005](005-adopt-module-first-modular-monolith.md)) forbids sibling domain modules from referencing each other, so a new `Aonik.Commerce` cannot take a `ProjectReference` on `Aonik.Finance` to reuse `Order`. If `Order` is genuinely shared by financial services *and* commerce, it must not be trapped inside one consuming domain.

## Decision

Treat `Order` as a **core, cross-cutting concept** and unify it into a shared ordering layer that sits *below* the consuming domains:

1. **The Order contract is core.** `IOrderService`, the order DTOs, the `OrderType` / `OrderStatuses` / `OrderPartyRoles` constants, and the order integration events move into **`Aonik.SharedKernel.Abstractions.Ordering`** — mirroring the read-contract pattern established by [ADR-006](006-extract-personal-finance-module.md). Any module that only needs to read or create orders depends on the contract alone.

2. **The Order entities are core.** The anemic `Order`, `OrderItem`, `OrderPartyRole`, `OrderFundingRef`, `OrderFulfilmentRef`, `OrderHistoryEvent`, `OrderNote` entities **and their EF configurations** move into **`Aonik.SharedKernel`**, extending the existing precedent that SharedKernel already hosts persisted cross-cutting entities with their configs (`OutboxMessage` / `InboxMessage` in `SharedKernel/Events/Outbox/` + `SharedKernel/Persistence/`). SharedKernel already references EF Core, so this adds no new dependency.

3. **The generic order machinery is a new middle-layer module, `Aonik.Ordering`.** The generic `OrderService` implementation, the order lifecycle endpoints, the order agent tools, and the module-scoped `OrderingDbContext` live here. This is what SharedKernel cannot host — it has no concrete DbContext, carries no FastEndpoints surface, and is not where domain orchestration belongs.

4. **Domains compose on top.** `Aonik.Finance` keeps only its *order-type-specific orchestration* (FX quoting, compliance screening for remittance / bill-pay), and `Aonik.Commerce` adds its own (product, inventory, cart for product purchases). Both build on the shared core. Finance and Commerce remain siblings that never reference each other.

### Why a neutral `Ordering` layer rather than `Commerce`

The shared layer is named after **what is common to everything that depends on it** — *ordering* — not after one consumer. Naming it `Commerce` would force `Aonik.Finance` to depend on a module that also owns products / inventory / cart, leaking retail concepts into a remittance path, and would read wrong to future engineers (bill payment and remittance are not "commerce" in any normal sense). `Aonik.Commerce` remains a real module — the **retail domain on top** of the ordering core, not the owner of the order spine.

### Layering

```
Aonik.SharedKernel                         Order entities + EF configs
  └─ Abstractions.Ordering                 IOrderService contract + DTOs + events  (core)
        ▲
Aonik.Ordering                             generic OrderService impl + lifecycle endpoints + order tools
        ▲                ▲                  (middle layer; module-scoped OrderingDbContext, no migrations)
        │                │
Aonik.Finance      Aonik.Commerce          Finance: bill-pay / remittance orchestration
(existing)         (new, later)            Commerce: products, inventory, cart, retail orchestration
                                           both reuse Invoice, PaymentIntent, Party, the agent/approval rails
```

This is not a cross-module-reference violation. The [ADR-005](005-adopt-module-first-modular-monolith.md) rule prohibits **sibling-to-sibling** coupling; depending *downward* on a shared lower layer is the sanctioned way to share — exactly what SharedKernel is for. `Aonik.Ordering` is a domain-flavoured extension of that shared layer, blessed here the way ADR-005 blessed the first.

### Generalising the entity

- **`OrderType`** gains `ProductPurchase`. The column is already an open `string`; this is additive.
- **`OrderItem`** gains four **nullable** retail columns — `Quantity`, `UnitPrice`, `ProductId`, `Sku` — populated only for product lines. `ProductId` is a **soft reference (no FK constraint)**, exactly as `Order.PayerPartyId` already soft-references a Party in another module. The existing `AmountIn` is reused as the line total (`Quantity × UnitPrice`); no redundant `LineTotal` column is added.
- **`OrderFulfilmentRef`** gains a future nullable `ShipmentId` under its existing "exactly one set" CHECK when shipping lands.
- The financial-only header fields (`AmountOut` / `CurrencyOut` / `FxQuoteId` / `OriginCountry` / `DestinationCountry` / `PurposeCode`) were always nullable and simply go unused for product orders.

### Doctrine change

CLAUDE.md and [Spec 005](../specifications/005.orders-intent.md) are generalised: an **Order is the canonical record of a customer's intent to transact — to use an AONIK financial service *or* to purchase goods — where `OrderType` determines the nature of the order and what its line items capture.** The "Not Orders" exclusions (imported bank transactions, manual categorisations, ledger corrections) are unchanged. The non-negotiable rule that Order, Payment, and Ledger are never collapsed is unchanged.

### Architectural Guarantees

1. **No physical database change for the relocation.** Tables keep their `Ank` prefix (`AnkOrders`, `AnkOrderItems`, …); the entity move is project-structure-only, with C# namespaces deliberately preserved (per the [ADR-006](006-extract-personal-finance-module.md) Phase 2 rationale) so the `Designer.cs` snapshot FQN strings stay valid and **no migration is required for the move**.
2. **Single migration stream stays in `AonikDbContext`.** `OrderingDbContext` is runtime-only DI scoping with no migrations. The only schema delta — the new nullable `OrderItem` columns + the `ProductPurchase` value — is generated by the EF CLI against `AonikDbContext`. The permanent reference cost is one `Aonik.Infrastructure → Aonik.Ordering` ProjectReference (Infrastructure already references every module for the canonical migration stream).
3. **No HTTP contract change for existing callers.** `/orders/*` routes are unchanged.
4. **No `ProjectReference` between `Aonik.Finance` and `Aonik.Commerce`.** Both consume Order through `SharedKernel.Abstractions.Ordering` and (where they need the generic impl) `Aonik.Ordering`.

### Phased Rollout

Staged so the system stays shippable between phases.

| Phase | Description |
|-------|-------------|
| 1 ✅ | **Generalise in place (landed).** Added the nullable retail columns (`Quantity`, `UnitPrice`, `ProductId`, `Sku`) to `OrderItem`, added `ProductPurchase` to `OrderType`, updated the doctrine. One tool-generated migration (`OrderItemRetailColumns`) against `AonikDbContext`. No relocation yet — Finance's `Order` is now retail-capable. Lowest-risk, fully reversible, proves the "just another order type" thesis against the real schema. |
| 2 | **Promote the contract.** Introduce `SharedKernel.Abstractions.Ordering` (`IOrderService`, DTOs, events, constants). Finance's existing order callers route through the contract. No entity move yet. |
| 3 | **Relocate the spine.** Move the Order entities + EF configs into `SharedKernel` (namespace-preserving, no migration). Create `Aonik.Ordering` with `OrderingDbContext`, the generic `OrderService` impl, lifecycle endpoints, and order agent tools + approval manifest. Finance retains only its type-specific orchestration. |
| 4 | **Build `Aonik.Commerce`.** Product / Inventory / Cart entities + services, a `commerce-agent` with approval-gated tools ([Spec 032](../specifications/032.tiered-ai-mutation-approval.html)), product-purchase orders funded via the existing public `PaymentIntent` and billed via the existing `Invoice`. |

## Consequences

### Positive

- `Order` becomes a genuine platform primitive: one spine, one lifecycle, one audit trail, reused across financial services and commerce without sibling coupling.
- A new `Aonik.Commerce` module inherits the agentic, multi-tenant, audited, payment-capable substrate for free — the differentiated value the commerce capability was after.
- The ordering layer establishes a reusable pattern for future shared-domain concepts that outgrow a single module.
- Phase 1 delivers retail-capable orders immediately, before committing to the larger relocation.

### Trade-offs

- **A new shared layer.** `Aonik.Ordering` is a second shared layer beneath the domains, which the codebase did not previously have. This ADR formally blesses it; reviewers should understand depending on it is sanctioned, not a cross-module violation.
- **A rich aggregate in SharedKernel.** `Order` is heavier than the infra `Outbox`/`Inbox` entities already in SharedKernel. The kernel must stay disciplined — only the anemic entities + configs + contract belong there; all orchestration stays in `Aonik.Ordering`.
- **One permanent reverse reference:** `Aonik.Infrastructure → Aonik.Ordering`. Acceptable; Infrastructure already references every module to host the migration stream.
- **C# namespaces temporarily misaligned with project location** during the Phase 3 move (the same deliberate trade-off as ADR-006). A namespace rename can land later as an isolated change.
- **A wider `Order` entity** that carries both financial (`AmountOut`, `FxQuoteId`, …) and retail (`Quantity`, `Sku`, …) nullable columns — the classic single-table-with-type-specific-nullables trade-off, accepted in exchange for one unified spine.

## Alternatives Considered

- **Keep `Order` in Finance; `Commerce` reuses it via a SharedKernel write contract.** One Order entity without relocation — lower blast radius — but `Order` stays semantically owned by Finance, which is the wrong home for a concept now shared by a peer domain. Rejected in favour of making the ownership honest.
- **A separate `CommerceOrder` entity.** Clean retail shape, lowest coupling, but creates two "order" concepts and forfeits the unified spine the team explicitly wants. Rejected.
- **Put the entity *and* the service in `SharedKernel`.** Rejected — the generic `OrderService` needs a concrete DbContext and the kernel must not host domain orchestration or an endpoint surface.
- **Name the shared layer `Aonik.Commerce`.** Rejected — leaks retail concepts into Finance and mis-names a layer that owns remittance and bill payment.

## See Also

- [Spec 041](../specifications/041.unified-order-spine-ordering-layer.html) — full specification (current-state inventory, domain model, contracts, phased tasks, risk register).
- [ADR-005](005-adopt-module-first-modular-monolith.md) — module-first modular monolith.
- [ADR-006](006-extract-personal-finance-module.md) / [Spec 027](../specifications/027.extract-personal-finance-module.html) — module-extraction & namespace-preservation precedent.
- [ADR-002](002-anemic-domain-model.md) — anemic domain entities.
- [Spec 005](../specifications/005.orders-intent.md) — the original Orders-as-intent design (doctrine being generalised).
- [Spec 030](../specifications/030.proposal-execution-dispatcher.html) & [Spec 032](../specifications/032.tiered-ai-mutation-approval.html) — agent mutation governance for commerce tools.
