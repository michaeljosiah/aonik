<p align="center">
  <img src="docs/images/hero-banner.png" alt="AONIK" width="100%">
</p>

<h1 align="center">AONIK</h1>

<p align="center">
  <strong>A modular platform for building AI-powered domain systems.</strong><br>
  Identity. Compliance. Agents. Ledger. Payments. One foundation.
</p>

<p align="center">
  <code>.NET 10</code> &middot; <code>SQL Server</code> &middot; <code>FastEndpoints</code> &middot; <code>EF Core 10</code> &middot; <code>Microsoft Agent Framework</code>
</p>

<p align="center">
  <em>Early development &mdash; APIs and data models are evolving. Breaking changes expected.</em>
</p>

---

## What is AONIK?

AONIK is an open-source, AI-native platform for building intelligent domain systems. It provides the foundational infrastructure that every serious vertical application needs — identity, tenancy, compliance, agents, workflows — so that domain modules can focus on their actual problem space.

The platform is **modular by design**. Each capability is a self-contained module with its own entities, services, endpoints, and persistence. Modules communicate through contracts and integration events, not direct coupling. New verticals plug in without touching the core.

**Finance is the first vertical module.** It implements double-entry ledger, payments, billing, orders, and pricing as a fully-featured domain module built on the platform. But AONIK is not a fintech platform — it is the infrastructure layer that a fintech product (or any other intelligent domain system) is built on.

---

## Platform Core

The platform provides horizontal capabilities that any domain module can consume:

**Identity and Access** — Multi-tenant identity with users, roles, permissions, and tenant isolation. Every request is scoped to a tenant. Every entity is tenant-aware.

**Party and Profile** — Unified party model for people and businesses. KYC/KYB scaffolding, address/contact management, relationship tracking, and external account linking.

**Compliance and Risk** — Screening checks, compliance cases, audit logging, document management with verification workflows. Policy-governed and auditable.

**AI Platform** — Multi-provider LLM routing with model selection policies. Prompts and tools are versioned. Every AI execution is recorded as an `AiRun` with cost tracking and feedback loops.

**Agent Framework** — Domain-specific agents built on Microsoft Agent Framework. Agents reason, plan, and use tools — but they never directly mutate state. All material actions follow the proposal pattern:

```
Agent creates Proposal  -->  Human or policy approves  -->  Domain service applies
```

This flow is never bypassed. Agents propose. Systems apply. Humans stay in control.

**Operations** — Background jobs, work items, notifications, webhook subscriptions, and content management. The runtime plumbing that domain modules need but shouldn't have to build.

---

## First Vertical: Finance

The Finance module (`Aonik.Finance`) is the first domain vertical built on the platform. It demonstrates the full module pattern and provides production-grade financial primitives:

- **Ledger** — Double-entry, immutable. The source of financial truth. Journal entries, chart of accounts, balance snapshots.
- **Payments** — Payment intents, payment processing, payouts, refunds, chargebacks. Provider-abstracted.
- **Orders** — Business intent hub. Orders capture *why* money moves, link parties, reference funding and fulfilment. They are not payments.
- **Billing** — Invoices, line items, allocations, customer accounts, dunning plans.
- **Pricing** — Fee policies, FX rate sources, spread policies, limits, pricing quotes.
- **Partners** — Correspondent network with connectors, routing rules, payout schemas, transmissions.
- **Personal Finance** — Budgets, goals, bills, subscriptions, categorisation, household management.

The Finance module has its own domain agent (`FinanceDomainAgent`) and MCP server (`Aonik.Finance.Mcp`) for tool interoperability with the agent framework.

Products built on AONIK Finance:

| Product | Domain |
|---|---|
| **Payabo** | B2C personal finance — budgets, bills, subscriptions, goals |
| **MyBillAfrica** | B2B billing and collections |
| **RemitExchange** | Cross-border remittance |

---

## Architecture

AONIK is a **module-first modular monolith**. Each domain module owns its vertical slice — entities, services, endpoints, persistence configuration — with a module-scoped DbContext over a shared physical database.

```
src/
  Aonik.SharedKernel/       Cross-cutting primitives, interfaces, integration events
  Aonik.Platform/           Identity, tenancy, party/profile, compliance, notifications
  Aonik.Finance/            Ledger, payments, orders, billing, pricing, partners
  Aonik.Ai/                 Model routing, prompts, AI execution records
  Aonik.Agents/             Domain agents, orchestration, proposal workflows
  Aonik.Application/        Shared application abstractions
  Aonik.Infrastructure/     EF migrations, external adapters, composition support
  Aonik.Api/                HTTP API composition root (FastEndpoints)
  Aonik.Worker/             Background jobs (Quartz)
  Aonik.Migrator/           Database migration host
  Aonik.AppHost/            .NET Aspire orchestration
  Aonik.AdminUi/            Admin interface (React 19, Vite, Tailwind, Dockview)
  Aonik.Finance.Mcp/        Finance MCP server
  Aonik.Platform.Mcp/       Platform MCP server

tests/
  Aonik.SharedKernel.Tests/
  Aonik.Application.Tests/
  Aonik.Infrastructure.Tests/
  Aonik.Api.Tests/
```

### Module Boundaries

Modules depend on `SharedKernel` for primitives, contracts, and integration events. They do not reference each other directly. Cross-module communication uses:

- **SharedKernel contracts** — Interfaces like `IPartyService`, `IComplianceService` that one module implements and another consumes via DI.
- **Integration events** — `TenantProvisionedEvent`, `OrderCreatedEvent`, `PaymentCompletedEvent`, etc. Published by one module, subscribed to by others.
- **Read models** — Lightweight projections for cross-module queries where eventual consistency is acceptable.

Each module registers itself in the API composition root:

```csharp
services.AddPlatformModule(configuration);
services.AddFinanceModule(configuration);
services.AddAiModule(configuration);
services.AddAgentsModule(configuration);
```

### Design Principles

- **Ledger is the source of financial truth.** Double-entry, immutable.
- **Orders represent business intent.** They are not payments.
- **Agents propose; systems execute.** Every material action follows Propose, Approve, Apply.
- **Every AI action is auditable.** Every execution is an `AiRun`. Financially material outputs reference an `AiRunId`.
- **Risk tier determines AI autonomy.** Human approval is explicit for high-risk actions.
- **Modules own their boundaries.** No cross-module project references. Contracts and events only.

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server (LocalDB, Express, or full instance)
- Git

### Build and Run

```bash
git clone https://github.com/anomalyco/aonik.git
cd aonik

# Build
dotnet build Aonik.sln

# Apply database migrations
dotnet ef database update --project src/Aonik.Infrastructure --startup-project src/Aonik.Api

# Run the API
dotnet run --project src/Aonik.Api
```

API starts on `https://localhost:5001` with Swagger at `/swagger`.

### Run Tests

```bash
# All tests
dotnet test Aonik.sln

# Specific project
dotnet test tests/Aonik.Application.Tests

# Specific test
dotnet test --filter "DisplayName~CreateInvoice"
```

106 tests across 4 test projects. All passing.

---

## Technology

| Layer | Stack |
|---|---|
| Runtime | .NET 10 |
| API | FastEndpoints |
| ORM | Entity Framework Core 10 |
| Database | SQL Server |
| AI/Agents | Microsoft Agent Framework, MCP |
| Background Jobs | Quartz.NET |
| Orchestration | .NET Aspire |
| Caching | FusionCache |
| Admin UI | React 19, Vite, Tailwind CSS, Dockview |
| Testing | xUnit, FluentAssertions |
| IaC | Bicep (Azure Container Apps + App Service) |
| CI/CD | GitHub Actions |

---

## Documentation

| Document | Description |
|---|---|
| [AGENTS.md](AGENTS.md) | Coding standards, architecture rules, and build commands |
| [Architecture Overview](docs/architecture/overview.md) | System design and module boundaries |
| [Module Organization](docs/architecture/module-organization.md) | How code is structured within modules |
| [Technology Stack](docs/architecture/technology-stack.md) | Detailed technology choices and rationale |
| [Testing Guide](docs/Testing.md) | Testing patterns, conventions, and examples |
| [Troubleshooting](docs/Troubleshooting.md) | Common issues and solutions |
| [API Authentication](docs/features/authentication-authorization.md) | Auth setup, local usage, and endpoint security |
| [CHANGELOG](CHANGELOG.md) | Version history |

---

## Contributing

AONIK is open to contributions. The project is evolving — expect refactors and breaking changes.

Before submitting code:
1. `dotnet build Aonik.sln` must succeed with 0 errors
2. `dotnet test Aonik.sln` must pass
3. Follow the standards in [AGENTS.md](AGENTS.md)
4. Update [CHANGELOG.md](CHANGELOG.md) with your changes

See [Contributing Guide](docs/contributing/) for more detail.

---

## License

Apache License, Version 2.0. See [LICENSE](LICENSE).

---

<p align="center">
  <strong>Agents propose. Systems apply. Humans stay in control.</strong>
</p>
