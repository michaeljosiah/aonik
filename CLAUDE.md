# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What AONIK Is

AONIK is an AI-native, modular financial platform (.NET 10) that powers multiple products: Payabo (B2C personal finance), MyBillAfrica (B2B billing), and RemitExchange (cross-border remittance). It is the foundational platform layer, not a single application.

## Build, Test & Run Commands

```bash
# Build
dotnet build Aonik.sln
dotnet clean Aonik.sln && dotnet build Aonik.sln

# Test
dotnet test Aonik.sln
dotnet test tests/Aonik.Application.Tests
dotnet test --filter "FullyQualifiedName~BillingServiceTests.CreateInvoiceAsync_ShouldCreateInvoiceWithLineItems"
dotnet test --filter "DisplayName~CreateInvoice"

# Run API (https://localhost:5001, Swagger at /swagger)
dotnet run --project src/Aonik.Api

# Run with Aspire orchestrator (starts API + Admin UI + Payabo + Worker)
dotnet run --project src/Aonik.AppHost

# Database migrations
dotnet ef migrations add <Name> --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
dotnet ef database update --project src/Aonik.Infrastructure --startup-project src/Aonik.Api

# Admin UI
cd src/Aonik.AdminUi && npm install && npm run dev   # port 5173

# Payabo app
cd apps/Payabo && npm install && npm run dev          # port 5174
```

## Architecture

**Module-first modular monolith** with anemic domain entities. Each module owns its vertical slice (entities, services, endpoints, persistence) with a module-scoped DbContext over a shared SQL Server database.

Key modules:
- **SharedKernel** — Cross-cutting primitives, interfaces, integration events
- **Platform** — Identity, tenancy, party/profile, compliance, notifications
- **Finance** — Ledger, payments, orders, billing, pricing, partners (B2B / cross-border money plumbing)
- **PersonalFinance** — Households, personal accounts, transactions, bills, subscriptions, budgets, goals, financial life graph, customer insights (B2C / Payabo substrate). Sibling to Finance per [ADR-006](docs/decisions/006-extract-personal-finance-module.md). Reads Order/Invoice/Payment data exclusively through `SharedKernel.Abstractions.Finance` read contracts; no direct ProjectReference to `Aonik.Finance`.
- **Ai** — LLM routing, prompts, AI execution records
- **Agents** — Domain agents (MAF-based), orchestration, proposals
- **Infrastructure** — EF Core migrations, external adapters
- **Api** — HTTP composition root (FastEndpoints)
- **Worker** — Background jobs (Quartz)
- **AdminUi** — React 19 + Vite + Tailwind + Dockview

Module organization follows vertical slices:
```
src/Aonik.Finance/Entities/Billing/Invoice.cs
src/Aonik.Finance/Services/Billing/BillingService.cs
src/Aonik.Finance/Endpoints/Billing/CreateInvoiceEndpoint.cs
src/Aonik.Finance/Persistence/Configurations/Billing/InvoiceConfiguration.cs
```

**No direct cross-module references.** Modules communicate via SharedKernel contracts (interfaces like `IPartyService`) and integration events (`TenantProvisionedEvent`, `OrderCreatedEvent`).

## Database & Migrations (Critical)

**Single migration stream.** All EF Core migrations go through `AonikDbContext` in `src/Aonik.Infrastructure/Migrations/`. This is the ONLY context whose migrations are applied at startup. Never generate migrations against module-scoped DbContexts (`PlatformDbContext`, `FinanceDbContext`, `AiDbContext`, `AgentsDbContext`).

### DbContext Hierarchy

```
AonikDbContextBase (abstract — tenancy, audit, soft-delete)
├── AonikDbContext      — Canonical. Owns ALL entity DbSets. Only context with migrations.
├── PlatformDbContext    — Module read/write path. No migrations (frozen).
├── FinanceDbContext     — Module read/write path. No migrations.
├── AiDbContext          — Module read/write path. No migrations.
└── AgentsDbContext      — Module read/write path. No migrations.
```

All contexts share the same physical SQL Server database. Module contexts exist for DI scoping and module isolation at runtime, NOT for independent migration streams.

### Migration Rules

1. **Always generate migrations against AonikDbContext:**
   ```bash
   dotnet ef migrations add <Name> --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
   ```
2. **Entity configurations** belong in their module's `Persistence/Configurations/` folder as `IEntityTypeConfiguration<T>` classes — not inlined in `AonikDbContext.cs`.
3. **New DbSets** must be added to `AonikDbContext`. If the entity is accessed by a module context, add it there too.
4. **Global entities** (Worker/system-scoped, no tenant): register in `IsGlobalEntity()` override in `AonikDbContext`.
5. **Never generate migrations against `PlatformDbContext`** or other module contexts. The `PlatformDbContext` migration folder (`src/Aonik.Platform/Persistence/Migrations/`) is frozen legacy — do not add to it.
6. **Table naming**: All tables use `Ank` prefix in `dbo` schema (e.g. `AnkInvoices`, `AnkUsers`).

### CRITICAL: Migrations Must Be Tool-Generated Only

**Agents (Claude, Copilot, or any AI assistant) are NEVER permitted to hand-write or manually author migration files.** All migrations MUST be generated exclusively by the EF Core CLI tooling:

```bash
dotnet ef migrations add <Name> --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
```

**Why this rule exists:** Hand-written migrations cause model snapshot drift. The Designer.cs snapshot diverges from the actual database state, leading to duplicate column errors, missing tables, and cascading failures that require dangerous manual SQL patches to fix. Manual SQL patches then corrupt the migration history further, creating a cycle of breakage.

**The correct workflow for any schema change:**
1. Modify the entity class and/or its `IEntityTypeConfiguration<T>`
2. Add `DbSet<T>` to `AonikDbContext` (and module context if needed)
3. Run `dotnet ef migrations add <Name> --project src/Aonik.Infrastructure --startup-project src/Aonik.Api`
4. **Review the generated migration** — verify it only contains the expected changes
5. If the migration contains unexpected changes, investigate and fix the model first, then `dotnet ef migrations remove` and regenerate
6. Run `dotnet ef database update` locally to verify the migration applies cleanly
7. Commit both the migration `.cs` and `.Designer.cs` files

**Prohibited actions:**
- Writing `migrationBuilder.AddColumn(...)`, `migrationBuilder.CreateTable(...)`, etc. by hand
- Copying and modifying an existing migration file
- Editing a generated migration's `.Designer.cs` snapshot
- Running raw SQL against production/dev databases to "fix" schema drift (except as a last-resort emergency with explicit user approval)
- Using `--no-build` with migration generation if model changes haven't been compiled

**After deployment, always verify:** Run `dotnet ef migrations list --connection <conn>` against the target database to confirm the migration was applied. If auto-migration at startup fails silently, apply it explicitly with `dotnet ef database update --connection <conn>`.

## Non-Negotiable Architectural Rules

1. **Ledger is the source of financial truth** (double-entry, immutable)
2. **Orders are the canonical record of a requested financial service** — not payments, not ledger entries — never collapse them
3. **Agents propose; systems execute** — never bypass human-in-the-loop for mutations
4. **Every AI action is auditable** (recorded as `AiRun`)
5. **Risk tier determines AI autonomy**
6. **Domain entities are anemic** — all business logic lives in services, not entities

## Authentication

AONIK supports **three operator-choice IdP providers**: Auth0, Azure AD (Entra ID), and **Keycloak** (per [ADR-007](docs/decisions/007-keycloak-as-auth-provider.md) / [Spec 029](docs/specifications/029.keycloak-auth-provider.html)). The `Auth.Provider` setting selects one per deployment — **not per-tenant**. Six capability surfaces (JWT validation, IdP management client, user provisioning, password reset, account service, token exchange) each have one interface in `Aonik.Platform.Contracts.Services.Authentication` and one factory in `Aonik.Infrastructure.Authentication` that dispatches by `Auth.Provider` string. Issuer routing in `AonikAuthenticationSetup.SelectScheme` lets tokens from any registered provider validate side-by-side, so a provider switch is non-breaking for in-flight tokens. Keycloak owns federation to upstream IdPs (Okta, AD FS, SAML, social) — Aonik itself talks one OIDC dialect. The Keycloak local-dev profile lives at `infra/keycloak/compose.keycloak.yml`.

## Orders

An Order is the canonical record of a customer's intent to **transact** — to use an AONIK-powered financial service *or* to purchase goods. It captures what was requested, the parties involved, the amounts and currencies, and the lifecycle from funding through fulfilment. The `OrderType` determines the nature of the order and what its line items capture; the base order fields stay the same across types (see [ADR-011](docs/decisions/011-unify-order-spine-into-ordering-layer.md) / [Spec 041](docs/specifications/041.unified-order-spine-ordering-layer.html)).

An Order records: what was requested, who the relevant parties are, what amounts/currencies are involved, how the request is funded, and how it is fulfilled.

**Order vs Payment vs Ledger** (never collapsed, regardless of order type):
- **Order** = the requested transaction (bill payment, money transfer, bill collection, payout, remittance, **product purchase**)
- **Payment** = how the order is funded or executed (a `PaymentIntent`, for every type)
- **Ledger** = the financial truth proving what happened

**Order types & line shape:** `OrderType` is an open string (the enum is a known-values helper, so new types are additive). A `ProductPurchase` line carries the retail shape on `OrderItem` — `Quantity`, `UnitPrice`, `ProductId` (a soft reference; no FK), `Sku` — with the existing `AmountIn` holding the line total (`Quantity × UnitPrice`); the FX/remittance fields (`AmountOut`, `CurrencyOut`, `FxQuoteId`, …) are simply left unused. Billing still flows through `Invoice` (`Invoice.OrderId`) and funding through a `PaymentIntent`, exactly as the financial types do.

> **Ownership (per [ADR-011](docs/decisions/011-unify-order-spine-into-ordering-layer.md), in progress):** Order is a *core* concept, not Finance-private. The contract is being promoted to `SharedKernel.Abstractions.Ordering` and the generic order machinery to a middle-layer `Aonik.Ordering` module that Finance and a future `Aonik.Commerce` both build on. Today the Order entities still physically live under `src/Aonik.Finance/Entities/Orders/` (Phase 1 generalised them in place); the relocation is Phases 2–3.

**Not Orders:** a standalone imported bank transaction, a manual categorisation, or a ledger correction (unless it exists to fulfil a transaction request).

## Code Patterns

**Entities**: Simple data containers with `{ get; set; }` properties. Inherit from `Entity` or `AuditableEntity`. Implement `ITenantScoped`. No constructors, no methods, no business logic.

**Services**: All logic here. Interface + implementation (`IBillingService` / `BillingService`). Constructor-inject DbContext, `ITenantProvider`, etc. Return DTOs, not entities. Always accept `CancellationToken cancellationToken = default`.

**Endpoints** (FastEndpoints): Inherit `Endpoint<TRequest, TResponse>`. Use `Send.OkAsync()`, `Send.CreatedAtAsync<T>()`, `Send.NotFoundAsync()`. Never use `SendAsync()` or `ResponseAsync()` directly.

**DTOs**: Use records with positional parameters: `public record CreateInvoiceRequest(Guid CustomerId, string Currency, ...);`

**Agent tools**: Read tools execute directly. Mutating tools must be approval-gated on the server, tiered by risk per [Spec 032](docs/specifications/032.tiered-ai-mutation-approval.html): **low** (reversible personal-state writes) run in-band with an audit record; **medium** (everyday domain writes like `CreateInvoice`) require an in-session confirmation before running in-band; **high** (money movement, ledger posting, partner calls like `CapturePayment`) never run in-band — they are marshalled into a durable `Proposal` and executed only by the [Spec 030](docs/specifications/030.proposal-execution-dispatcher.html) dispatcher after approval.

> ⚠️ **Current status — server gate landed; frontend/voice presentation still pending.** The Spec 032 server-side boundary now exists and **fails closed**. `IToolApprovalGate` (`ToolApprovalGate`, registered in `AgentsModule`) wraps every classified mutating tool in `ApprovalGatedAIFunction` — our *enforcing* decorator, not the framework's passive `ApprovalRequiredAIFunction` marker — and an unclassified mutating-looking tool throws `ToolNotClassifiedException` at build time. The Finance agent's tools are routed through `gate.GateAll(...)` in `FinanceAgentRegistration` and classified by `FinanceToolApprovalManifest` (the one manifest wired today; other modules plug their own into the same gate): the money tools (`finance_capture_payment`, `finance_cancel_payment`, `finance_create_payment_intent`, `finance_mark_invoice_paid`) are **high** → marshalled into a durable `Proposal`, never run in-band; the everyday invoice/ledger writes are **medium** → a durable, args-hash-bound `ToolApprovalRequest` is persisted and the tool runs inline **once**, only after a human approves via `POST /ai/tool-approvals/{id}/decide` (validated server-side for identity, tenant, expiry, single-use, args-hash) and the agent re-invokes with the same arguments. **Still pending (Spec 032 transport/presentation slice):** the Admin UI does not yet render the medium-approval card, route approve/reject through `DecideAsync`, or resubmit the run — it still uses the existing frontend `confirmAction` convention, which is being demoted to pure presentation. Voice parity is also pending. The server boundary is real regardless of the frontend, so the remaining work is UX, not enforcement.

**Money-action logging** (GitHub Issue #142): every money-touching code path — quote, confirm, capture, transmit, settle, webhook — MUST emit `MoneyActionLog.*` at entry and at outcome, open a `FinanceActivitySource` span, and resolve OrderId into `BeginOrderScope` as early as possible. See [docs/observability/money-action-logging.md](docs/observability/money-action-logging.md) for the convention, the EventId schema, the saved KQL query, and the reviewer checklist. Operators pivot on OrderId via `GET /admin/observability/money-actions/{OrderId}` (target wall-clock under 30s).

## Testing

- **Framework**: xUnit + FluentAssertions (use `.Should()`, never `Assert.*`)
- **Pattern**: AAA with `MethodName_Should_ExpectedBehavior_When_Condition` naming
- **Database**: InMemory with unique name `$"TestDb_{Guid.NewGuid()}"`
- **Required mocks**: `ITenantProvider` (use `TestTenantProvider`) and `ICurrentUserProvider`
- **API tests**: Use `CustomWebApplicationFactory` with `UseInMemoryDatabase=true` in config
- **SQL Server lane**: `tests/Aonik.Database.Tests` runs against SQL Server LocalDB via `SqlLocalDbFixture` (per-class throwaway database built with `EnsureCreated`; skips gracefully where LocalDB is absent). The InMemory provider is non-relational, so changes touching `BeginTransaction` / execution strategies, `RowVersion` optimistic concurrency, or filtered unique indexes MUST add or run coverage in this lane — the InMemory suite is structurally unable to fail on those (that is how the Spec 066 P1 shipped). The Docker-based `tests/Aonik.Finance.IntegrationTests` lane complements it by applying the real migration stream.

## Naming Conventions

- Classes/Interfaces: PascalCase (`Invoice`, `IBillingService`)
- Async methods: PascalCase + `Async` suffix (`CreateInvoiceAsync`)
- Private fields: `_camelCase` (`_dbContext`)
- Parameters/locals: camelCase
- Nullable annotations enabled globally

## Tech Stack Quick Reference

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 (`net10.0`) |
| API | FastEndpoints |
| ORM | EF Core 10 / SQL Server |
| AI/Agents | Microsoft Agent Framework, Semantic Kernel |
| Background jobs | Quartz.NET |
| Admin UI | React 19, Vite, Tailwind, Dockview |
| Caching | FusionCache |
| Orchestration | .NET Aspire |
| Testing | xUnit, FluentAssertions |
