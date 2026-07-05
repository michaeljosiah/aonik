# Finishing the PersonalFinance extraction — coordinated plan for #118 + #126

**Status:** Proposed · **Rev 2** (corrected against the current tree)
**Relates to:** [ADR-006](006-extract-personal-finance-module.md) (extract PersonalFinance as a Finance sibling), Spec 027
**Closes:** [#118](https://github.com/michaeljosiah/aonik/issues/118) (M5 — split & move `PersonalFinanceTools`), [#126](https://github.com/michaeljosiah/aonik/issues/126) (M15 — finish the module-boundary extraction)

Issues #118 and #126 are one program with a single goal: delete the one transitional project reference still tying Finance to PersonalFinance. There is no circular dependency to untangle — only Finance-side code to relocate. #118 is the largest piece of that move, not a separate task.

---

## 1. What grounding changed (rev 1 → rev 2)

> **No circular dependency.** The first draft of this plan read a `Finance ⇄ PersonalFinance` cycle and made "move shared contracts to SharedKernel" its keystone phase. Verifying that for execution **disproved it.**

- `Aonik.PersonalFinance.csproj` references **only SharedKernel** — no Finance reference. Its own comment calls this "the load-bearing absence (Spec 027 §10)... PersonalFinance reads Orders / Invoices / Payments via the `SharedKernel.Abstractions.Finance` read contracts."
- Those read contracts are **already wired**: `FinancialLifeGraphLoader` / `FinancialLifeGraphValidationService` inject `ICustomerInvoiceHistoryReader`, `ICustomerPaymentHistoryReader`, `IFxQuoteReader`. The whole solution compiles with no PersonalFinance→Finance reference today.
- The apparent cycle was a **namespace illusion**: the `Aonik.Finance.*.PersonalFinance` types (`…Contracts.Services.PersonalFinance`, `…Entities.PersonalFinance`, etc.) are **physically in the PersonalFinance assembly**, just not renamed yet. A grep for `using Aonik.Finance.*` inside PersonalFinance *looks* like a Finance dependency but resolves intra-assembly.

**Consequence:** there is no keystone contract move. `PersonalFinance → Finance` is already severed. The only edge left is one-directional — `Finance → PersonalFinance` (`Aonik.Finance.csproj:47`, flagged transitional) — and the entire job is relocating the Finance-side code that still reaches across it, then deleting the reference.

## 2. The coupling map (corrected)

| Edge | State | Detail |
|---|---|---|
| **PersonalFinance → Finance** | ✅ already severed (0 refs) | No project reference; reads Finance data via the SharedKernel read contracts. Nothing to do. |
| **Finance → PersonalFinance** | ⛔ the one remaining edge | 6 categories: agent tools (#118), sub-agent descriptors, CodeAct shims, seed contributors, the AccountLinking subtree, and `FinanceDbContext`'s 27 PF DbSets/configs. |
| **`IAonikDbContext` facade** | ~110 DbSets, **9 consumers** | All Platform infra (tenant, settings, reference-data, features). No domain module uses it. Independent side-cleanup. |
| **Misnamed namespaces** | **281 of 290** PF files | Still declare `Aonik.Finance.*` — the source of the illusion. Batched rename with zero external importers. |

## 3. The main line — remove the edge

Five sequential steps, each its own reviewed PR. This is the whole of #118 plus the structural half of #126. **No keystone gate — S1 can start immediately.**

### S1 — Split `PersonalFinanceTools` & relocate the agent surface `#118 #126` · risk: med–high
Split the 1,660-line / 22-dependency class into **~8 capability classes** — Accounts, Transactions, Bills, Spending, Dashboard&FX, Goals&Compass, Commitments, Specialists — each taking only the 1–4 services it needs. Move them, `AccountLinkingTools`, and the four sub-agent descriptors into a `PersonalFinance.Agents` area; PersonalFinance gains references to MAF / `Microsoft.Extensions.AI` and `SharedKernel.Abstractions.Agents`. Rewire the `AIFunctionFactory.Create` registration to compose from the split classes, and re-point `CodeActCallbackEndpoint.ResolveSlice` at a DI-registered tool-slice factory interface.

*Highest-risk step (agent wiring across 9+ registration sites). Do it in two commits — **split in place**, then **relocate** — verified by the AG-UI / playground / agent tests.*

### S2 — Move seeding & AccountLinking into PersonalFinance `#126` · risk: medium
Relocate the seed contributors (`PersonalFinanceSeedContributor`, `PersonalFinanceActivitySeedPhase`) and the 7-file AccountLinking subtree onto PersonalFinance and its `PersonalFinanceDbContext`. Self-contained relocations; parallelizable with S3.

### S3 — Finish the DbContext ownership transfer `#126` · risk: medium
`PersonalFinanceDbContext` **already exists** and is in use — this step finishes the transfer: move the 27 PF DbSets + their entity configurations off `FinanceDbContext` (which still declares them and applies the PF configs transitionally) so PersonalFinance owns them outright. Migrations stay in `AonikDbContext` per the single-stream rule; this is DI-scoping, not a schema change.

*Mirror `AonikDbContextBase`'s tenant + soft-delete filters exactly, and add a discriminating cross-tenant test on a PF entity (the shape used for PaymentService in #120).*

### S4 — Migrate the namespaces `#126` · risk: low (large)
Rename the 281 files from `Aonik.Finance.*.PersonalFinance` → `Aonik.PersonalFinance.*` under 7 prefix rules. Batch by prefix; build green after each. Zero external importers, so the blast radius is internal — `TreatWarningsAsErrors` (from #119) and the compiler catch any straggler at once. This is what finally makes the boundary read honestly.

### S5 — Drop the `Finance → PersonalFinance` reference `#118 #126` · risk: low
The finish line. With every Finance-side call site relocated, remove the `ProjectReference` and the paired `InternalsVisibleTo` in both csproj files. A green build is the proof of isolation. This is where #118 and #126 both close, and Finance / PersonalFinance become true ADR-006 siblings.

## 4. Two independent side-cleanups (off the critical path)

- **Sever Platform → Finance** (low risk, 3 files): point `ListCustomerInsightsEndpoint` at the SharedKernel reader contracts, and give the demo-seed cleanup an `IDemoDataCleaner` port Finance implements (or relocate the cleanup into Finance). Drop the `Platform → Finance` reference.
- **Narrow `IAonikDbContext`** (low risk): trim the ~110-DbSet facade toward the four tables its 9 infra consumers actually touch (`Tenants`, `Settings`, `ReferenceDataItems`, `TenantFeatures`), or split into module-scoped surfaces.

## 5. Sequencing, risk & effort

**Critical path:** `S1 → (S2 + S3) → S4 → S5`. The two side-cleanups land whenever.

- **S1 is the one to test hardest** — agent wiring isn't proven by compilation. Split-in-place first (a pure reorganization diff), then move, leaning on the AG-UI / playground / agent-descriptor tests.
- **S3 guards tenant isolation** — the PF DbSets must keep `AonikDbContextBase`'s tenant + soft-delete filters; add a cross-tenant test that discriminates.
- **`TreatWarningsAsErrors` is the tripwire** for S4's rename — a stray namespace or dangling using fails the build immediately.
- **Every step is a reviewed PR** with the adversarial pass used across the M-series; isolate parallel work (S2/S3) in worktrees.

| Step | Serves | PRs | Where the effort is |
|---|---|---:|---|
| S1 Split + relocate tools | #118 · #126 | 2–3 | **The real engineering** — ~8 classes, agent rewiring, CodeAct factory |
| S2 Seeding + AccountLinking | #126 | 1–2 | Self-contained relocations onto `PersonalFinanceDbContext` |
| S3 DbContext ownership | #126 | 1 | Transfer 27 DbSets/configs off `FinanceDbContext` (context already exists) |
| S4 Namespace migration | #126 | 2–3 | Bulk of the diff, least of the risk — batched regex |
| S5 Drop the reference | #118 · #126 | 1 | Trivial once S1–S4 land; the build is the proof |
| Platform + facade cleanups | #126 | 2 | Small, independent, anytime |

**Bottom line:** ~9–12 focused PRs, and no keystone gate — S1 can start immediately. The 281-file rename dominates the diff but carries the least risk; the agent-surface and DbContext work (S1–S3) is where judgment and testing matter.

---

*Corrected against the current tree: `Aonik.PersonalFinance.csproj` references only SharedKernel (no Finance edge); the SharedKernel read contracts are already in use; the one remaining reference is `Finance → PersonalFinance`; the 281 `Aonik.Finance.*.PersonalFinance` namespaces are the illusion behind the earlier "circular" reading.*
