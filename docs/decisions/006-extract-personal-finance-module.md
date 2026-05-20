# ADR-006: Extract PersonalFinance into Its Own Sibling Module

**Status**: In Progress (Phases 0–3 partial landed; Phases 3-remainder through 8 in flight)
**Date**: 2026-05-19
**Decision Makers**: Development Team
**Related**: [ADR-005](005-adopt-module-first-modular-monolith.md), [Spec 027](../specifications/027.extract-personal-finance-module.html)

## Context

`Aonik.Finance` accreted seven distinct subdomains — Ledger, Payments, Orders, Billing, Pricing, Partners, Catalog, and **PersonalFinance**. `FinanceModule.cs` reached ~280 lines of DI registrations, and PersonalFinance accounted for roughly half the surface area. PersonalFinance is also the entire substrate of one product (Payabo, B2C personal finance), whose release cadence is decoupled from the slower-changing Ledger / Orders / Billing core that powers MyBillAfrica and RemitExchange.

ADR-005 anticipated this: *"module boundaries make selective service extraction feasible if scale requires it."* PersonalFinance is the highest-payoff, lowest-coupling-cost extraction:

- It owns its own bounded vocabulary (Household, PersonalAccount, PersonalTransaction, Bill, Subscription, DebtRepayment, Budget, Goal, FinancialContext, FinancialLifeGraph, CustomerInsightSnapshot, StatementImport, FinancialConnection).
- It owns its own AI surface — five agent descriptors plus the CodeAct sandbox plumbing.
- Its cross-module integration already passes through six `SharedKernel.Abstractions.*` adapters.

## Decision

Promote PersonalFinance from a folder hierarchy inside `Aonik.Finance` into a sibling module `Aonik.PersonalFinance`. The two modules are siblings whose only shared surface is `Aonik.SharedKernel`.

### Architectural Guarantees

1. **No database schema change.** Tables keep their `Ank` prefix; no data migration.
2. **Single migration stream stays in `AonikDbContext`.** Module DbContexts are runtime-only DI scoping. The cost is one permanent ProjectReference: `Aonik.Infrastructure → Aonik.PersonalFinance`.
3. **No `/personal-finance/*` URL contract change.** Payabo mobile / web are unaffected.
4. **No `ProjectReference` from `Aonik.PersonalFinance` to `Aonik.Finance`.** PF reads Order / Invoice / Payment data exclusively through SharedKernel read contracts.

### SharedKernel Boundary

Six thin read contracts in `SharedKernel.Abstractions/` decouple PersonalFinance from direct queries against entities in other modules:

**Finance reads** (`SharedKernel.Abstractions.Finance/`):
- `ICustomerOrderHistoryReader` → `OrderHistoryItem`
- `ICustomerInvoiceHistoryReader` → `InvoiceHistoryItem`
- `ICustomerPaymentHistoryReader` → `PaymentHistoryItem`
- `IFxQuoteReader` → `FxQuoteHistoryItem`

**Platform reads** (`SharedKernel.Abstractions.Platform/`):
- `IPartyReader` → `PartyHistoryItem`, `PartyRelationshipHistoryItem`
- `IUserDirectoryReader` → `UserDirectoryItem`

Implementations live in their owning module (`Aonik.Finance/Services/Finance/Readers/` and `Aonik.Platform/Services/{Party,Identity}/`) and are registered as `Scoped`. DTOs carry only the fields PersonalFinance actually consumes — they are not full entity projections. Tenant scoping is enforced inside each reader (verified by 17 unit tests in `tests/Aonik.Application.Tests/Finance/Readers/`).

### Phased Rollout

The extraction is staged so the system stays shippable between phases:

| Phase | Status | Description |
|-------|--------|-------------|
| 0 | ✅ Landed | SharedKernel read contracts + EF-backed reader implementations + unit tests. |
| 1 | ✅ Landed | `Aonik.PersonalFinance.csproj` skeleton + empty `PersonalFinanceModule` + solution entry. |
| 2 | ✅ Landed | 62 files moved (26 entities, 24 EF configs, 10 model contracts, 2 seed helpers) with C# namespaces deliberately preserved to keep Designer.cs FQN strings intact. `PersonalFinanceDbContext` created with the `CategorisationRule.Scope == "System"` global carve-out. Transitional refs added (Finance ↔ PersonalFinance) plus the permanent `Infrastructure → PersonalFinance` per R8. |
| 3 | ✅ Landed | 24 service contracts moved. 39 services moved (22 stateless / pure-PF, 17 with `FinanceDbContext` → `PersonalFinanceDbContext` swap). DI registrations relocated to `PersonalFinanceModule`. `FinancialLifeGraphSnapshot` refactored to use SharedKernel DTOs (`OrderHistoryItem`, `InvoiceHistoryItem`, `PaymentHistoryItem`) instead of entity types — first concrete payoff of the Phase 0 reader contracts. |
| 4 | ✅ Landed | All 63 user + admin endpoints + `PersonalFinanceValidators.cs` moved. The Phase 4 reverts were unblocked by the Phase 7 wrap-up: with `FinancialLifeGraphWriteService` / `FinancialLifeGraphValidationService` / `FinancialLifeGraphRetrievalService` / `CustomerInsightSnapshot*` / `HouseholdService` / `DashboardService` / `FinancialContextService` now living in PersonalFinance, their endpoints follow. |
| 5 | ✅ Landed | 3 `StructuredOutputs/*.cs` (Insights/Forecast/Classify) + 8 CodeAct sandbox files (`AcaSessions*`, `Hyperlight*`, `Null*`, `CodeActCallbackNonceService`, `CodeActSandboxContextFactory`) moved with their DI registrations. `Azure.Core`, `Azure.Identity`, and the three `Hyperlight.HyperlightSandbox.*` NuGet refs dropped from `Aonik.Finance.csproj`. |
| 6 | ⏳ Pending | `tests/Aonik.PersonalFinance.Tests/` + move PF tests + seed phases. |
| 7 | ✅ Landed (partial) | **Deferred-refactor wrap-up.** Extended SharedKernel readers with `ExistsAsync` / `HasActiveRelationshipBetweenAsync` / `GetRecentForPayerAsync` + published `OrderStatusCodes` / `OrderPartyRoleCodes` constants. Moved 9 services (FinancialLifeGraph Write / Validation / Retrieval, Household, Dashboard, FinancialContext, CustomerInsightSnapshot Service / Reader / Generator) + the `CustomerInsight/` subfolder + 15 endpoints (6 FLG + 8 Household + 1 CustomerInsight + 1 admin FLG). Refactored `CustomerInsightOrderHistoryBuilder` to consume `OrderHistoryItem` DTOs and `DashboardService` to use the new `GetRecentForPayerAsync` + `IPartyReader`. Two transitional refs still ship — they're now blocked by a smaller, well-scoped remainder (see "Transitional References" below). |
| 8 | ✅ Landed | This ADR + `CLAUDE.md`. `docs/architecture/module-organization.md` update pending. |

### Why Namespaces Were Preserved in Phase 2

The spec's draft showed updated namespaces under `Aonik.PersonalFinance.*`. In practice, ~15 Designer.cs migration snapshot files contain FQN strings that resolve types reflectively at runtime, and CLAUDE.md forbids hand-editing those snapshots. Updating namespaces would have forced a regenerated EF migration (a no-op schema-wise but an additive snapshot revision). Preserving the `Aonik.Finance.*` namespaces while physically relocating the files achieves the spec's primary intent — separate compilation and release cadence — without touching the migration stream. Namespace renaming, if desired, can land later as an isolated change.

### Transitional References (still in place after Phase 7 wrap-up)

Two transitional references still ship after Phase 7. The Phase 7 audit narrowed their blocker set significantly — they're now held in place by a small, well-defined remainder:

- `Aonik.Finance → Aonik.PersonalFinance` ProjectReference
- `Aonik.PersonalFinance` exposes `InternalsVisibleTo("Aonik.Finance")`

**Remaining blockers (out of scope for this wrap-up):**

- **`src/Aonik.Finance/Agents/Tools/*`** — `PersonalFinanceTools`, `FinancialLifeGraphTools`, `FinancialLifeGraphRetrievalTools`, `FinancialLifeGraphSchemaTools`, `FinancialLifeGraphTraversalTools`, `AccountLinkingTools` — agent-tool catalogues that consume PersonalFinance contracts and StructuredOutputs.
- **`src/Aonik.Finance/Agents/Pf*AgentRegistration.cs` + `PersonalFinanceAgentRegistration.cs`** — sub-agent descriptors referencing the moved CodeAct sandbox types.
- **`src/Aonik.Finance/Endpoints/Agents/CodeAct*.cs`** — endpoint shims around the moved CodeAct services.
- **`src/Aonik.Finance/Contracts/PersonalFinance/CommitmentEnums.cs`** — last PF-namespaced contract still in Finance.
- **`src/Aonik.Finance/Endpoints/PersonalFinanceEndpointsValidators.cs`** — validator file for PF endpoints.
- **`src/Aonik.Finance/Services/Seeding/PersonalFinanceSeedContributor.cs` + `Services/Seeding/Phases/PersonalFinanceActivitySeedPhase.cs`** — seed phases targeting PF entities.
- **`src/Aonik.Finance/Services/Accounts/Linking/*`** + `AccountTransactionCategorizer.cs` + `AccountLinkService.cs` — the legacy Account-linking subtree consumes `Aonik.Finance.Contracts.Services.PersonalFinance` interfaces.
- **`src/Aonik.Finance/Persistence/FinanceDbContext.cs`** — still exposes PF DbSets (for legacy queries above). Removing these alongside the AccountLinking subtree.

Each is annotated in the `csproj` with the removal trigger. Drop both transitional refs once the above relocate to `Aonik.PersonalFinance` (or invert their dependencies via SharedKernel).

## Consequences

### Positive

- Payabo product churn no longer recompiles Ledger / Orders / Billing.
- The SharedKernel read contracts establish a reusable extraction pattern for future module splits (e.g. Pricing).
- DI registration is cleanly split — `FinanceModule.cs` shrinks as services migrate.

### Trade-offs

- **One permanent reverse reference**: `Aonik.Infrastructure → Aonik.PersonalFinance`. Acceptable; Infrastructure already references every module to host the canonical migration stream.
- **Two transitional references** while Phase 3 completes. These are blockers for declaring the extraction "done" but harmless for shipping intermediate phases.
- **C# namespaces deliberately misaligned with project location** in moved files. The override is explicit (file declares `namespace Aonik.Finance.Entities.PersonalFinance;` even when located under `src/Aonik.PersonalFinance/`). Reviewers should be aware this is intentional, not an oversight.

## See Also

- [Spec 027](../specifications/027.extract-personal-finance-module.html) — full specification including current-state inventory and risk register.
- [ADR-005](005-adopt-module-first-modular-monolith.md) — module-first modular monolith.
