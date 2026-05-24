<p align="center">
  <img src="docs/images/hero-banner.png" alt="AONIK" width="100%">
</p>

<h1 align="center">AONIK</h1>

<p align="center">
  <strong>AI-native intelligence platform.</strong><br>
  Orchestration. Memory. Governance. Agents. Domain systems. One foundation.
</p>

<p align="center">
  <code>.NET 10</code> &middot; <code>SQL Server</code> &middot; <code>FastEndpoints</code> &middot; <code>EF Core 10</code> &middot; <code>Microsoft Agent Framework</code> &middot; <code>Qdrant</code>
</p>

<p align="center">
  <em>Active development &mdash; APIs and data models are evolving.</em>
</p>

---

## What is AONIK?

AONIK is a modular AI intelligence platform designed to power intelligent systems, agents, and products across multiple domains of human life.

It is not simply a fintech platform, a remittance application, or a personal finance tool. Those are products and domain experiences built on top of the platform. AONIK itself is the foundational intelligence substrate: the shared architecture, orchestration, memory, workflows, governance, and reasoning layer required to build AI-native systems that understand context, coordinate actions, and help people move forward with clarity and control.

The initial focus is finance because finance is structurally rich, operationally important, and deeply connected to everyday life. The long-term vision extends beyond finance into domains such as health, household coordination, education, wellbeing, productivity, and community systems.

In one sentence: **AONIK enables intelligent systems, agents, and products to reason, coordinate, and assist across multiple domains of life through shared orchestration, memory, governance, and trustworthy automation.**

---

## Platform Model

AONIK is structured into three layers:

| Layer | Role |
|---|---|
| **AONIK Core** | Domain-agnostic intelligence and orchestration substrate: identity, memory, workflows, governance, AI routing, agents, approvals, and shared human primitives. |
| **Domain Modules** | Specialised systems that own domain truth, operations, policies, workflows, and agents. Finance and Personal Finance are the first shipped modules. |
| **Product Experiences** | Curated end-user or operator experiences that package Core and one or more domain modules for a market, brand, or workflow. |

The ambition is not to build isolated applications. It is to create a trusted intelligence substrate capable of coordinating complex areas of life through shared context, adaptive workflows, explainable AI assistance, and explicit human control.

AONIK is **modular by design**. Each capability is a self-contained module with its own entities, services, endpoints, and persistence. Modules communicate through contracts and integration events, not direct coupling. New domains and product experiences plug in without collapsing into the core.

---

## Platform Core

AONIK Core provides horizontal capabilities that every domain module and product experience can consume:

**Identity and Access** &mdash; Multi-tenant identity with users, roles, permissions, and tenant isolation. Auth0 for external authentication, with Azure Entra ID as an alternative provider. Every request is scoped to a tenant. Every entity is tenant-aware.

**Party and Profile** &mdash; Unified party model for people and businesses. KYC/KYB scaffolding, address/contact management, relationship tracking, and external account linking.

**Compliance and Risk** &mdash; Screening checks, compliance cases, audit logging, document management with verification workflows. Policy-governed and auditable.

**AI Platform** &mdash; Multi-provider LLM routing with model selection policies. Runtime-configurable AI provider (OpenAI or Stub for development). Prompts and tools are versioned. Every AI execution is recorded as an `AiRun` with cost tracking and feedback loops.

**Vector Store (Qdrant)** &mdash; Semantic search over document embeddings for retrieval-augmented generation (RAG). Agents retrieve domain context before reasoning. Multi-tenant isolation ensures document privacy across tenants.

**User Memory** &mdash; Per-user memory system that agents use to recall context across conversations. Supports SQL Server (key-value) and Qdrant (semantic vector search) backends, configurable at runtime via platform settings.

**Shared Human Primitives** &mdash; Cross-domain concepts such as households, goals, tasks, reminders, relationships, documents, responsibilities, preferences, and memory/context. These are human concepts, not finance-only concepts, and they enable agents to reason across domains as the platform evolves.

**Agent Framework** &mdash; Five domain-specific agents built on Microsoft Agent Framework. Agents reason, plan, and use tools &mdash; but they never directly mutate state. Mutating tools are wrapped with `ApprovalRequiredAIFunction` for human-in-the-loop approval:

```
Agent calls mutating tool  -->  ApprovalRequiredAIFunction gates  -->  Human or policy approves  -->  Tool executes
```

This flow is never bypassed. Agents propose. Systems apply. Humans stay in control.

**Operations** &mdash; Background jobs (Quartz.NET), notifications, webhook subscriptions, content management, text-to-speech, and autonumbering. The runtime plumbing that domain modules need but shouldn't have to build.

**Workflow and Governance** &mdash; Tasks, schedules, operational workflows, approval flows, policy enforcement, provenance, audit logs, and explainability. AONIK does not pursue blind automation; higher-risk actions require user approval, policy validation, or operational oversight.

---

## Domain Agents

AONIK ships with five domain agents, each backed by MCP tool servers:

| Agent | Domain | Description |
|---|---|---|
| **Finance** | Billing, Ledger, Payments | Creates invoices, issues payments, queries ledger balances. Full billing lifecycle. |
| **Personal Finance** | Budgets, Goals, Accounts | Manages household budgets, savings goals, bill tracking, and spending insights. Requires user brief. |
| **Obligation Planning** | Recurring obligations | Plans and optimises recurring financial obligations with structured output. |
| **Spending Intelligence** | Transaction analysis | Analyses spending patterns, categorisation, and anomaly detection. |
| **Platform** | Admin, Tenancy, Settings | Manages tenants, users, roles, platform configuration, and system operations. |

Agents produce structured outputs and use tools scoped to their domain. The orchestrator (`MasterOrchestratorService`) routes conversations to the appropriate agent based on intent.

---

## Finance Module

AONIK Finance (`Aonik.Finance`) is the foundational financial operating domain. It provides the B2B / cross-border money plumbing &mdash; Ledger, Orders, Payments, Billing, Pricing, Partners, Catalog. The B2C personal-finance substrate now lives in its own sibling module (see below).

- **Ledger** &mdash; Double-entry, immutable. The source of financial truth. Journal entries, chart of accounts, balance snapshots.
- **Payments** &mdash; Payment intents, processing, payouts, refunds, chargebacks. Provider-abstracted.
- **Orders** &mdash; Business intent hub. Orders capture *why* money moves, link parties, reference funding and fulfilment. They are not payments.
- **Billing** &mdash; Invoices, line items, allocations, customer accounts, dunning plans.
- **Pricing** &mdash; Fee policies, FX rate sources, spread policies, limits, pricing quotes.
- **Partners** &mdash; Correspondent network with connectors, routing rules, payout schemas, transmissions.
- **Accounts** &mdash; External account linking, connection syncing, transaction import and reconciliation at the tenant level.
- **Catalog** &mdash; Product and service catalogue for pricing and order creation.

## Personal Finance Module

The PersonalFinance module (`Aonik.PersonalFinance`) is distinct from the Finance infrastructure layer. It focuses on personal financial assistance: budgeting, subscriptions, bills, financial guidance, goals, household coordination, insights, planning, and financial wellbeing.

PersonalFinance is a sibling of Finance and the substrate of the **Payabo** product. Extracted from `Aonik.Finance` per [ADR-006](docs/decisions/006-extract-personal-finance-module.md) so the B2C cadence (households, life-graph, customer insights) evolves independently of the Ledger/Orders/Payments core that powers MyBillAfrica and RemitExchange.

- **Households** &mdash; Multi-member groups, invitations, roles, shared accounts and budgets.
- **Personal Accounts &amp; Transactions** &mdash; Plaid-linked accounts, manual accounts, transaction import, categorisation, attachments.
- **Bills, Subscriptions, Debt Repayments** &mdash; Recurring commitments with verification status, next-due tracking, and AI-detected proposals.
- **Budgets &amp; Goals** &mdash; Per-category budget lines tied to transaction taxonomy; savings goals with funding-account links.
- **Financial Life Graph** &mdash; Node-edge graph stitching accounts, merchants, obligations, parties; agents read from it for context and propose new edges for approval.
- **Customer Insight Snapshots** &mdash; Deterministic snapshots of a user's financial position with AI-generated narrative summaries.
- **Financial Connections** &mdash; Plaid link/sync flow, webhook ingestion, account reconciliation.

PersonalFinance does **not** reference `Aonik.Finance` directly. Cross-module reads (Orders, Invoices, Payment Intents, FxQuotes, Parties, Users) go through `SharedKernel.Abstractions.{Finance,Platform}` reader contracts &mdash; the same pattern used by other inter-module boundaries.

## Future Domain Modules

The long-term platform model allows additional domain modules to compose onto AONIK Core without reimplementing identity, memory, workflows, approvals, agents, or governance.

Potential future modules include:

- **AONIK Health** &mdash; Wellness, nutrition, preventative guidance, health coaching, and healthcare coordination.
- **AONIK Household** &mdash; Shared planning, responsibilities, caregiving workflows, family coordination, and shared goals.
- **AONIK Education** &mdash; Learning plans, educational goals, tutoring workflows, and skill development.
- **AONIK Productivity** &mdash; Task coordination, personal operations, intelligent scheduling, and workflow orchestration.

Each module contributes specialised intelligence while benefiting from shared Core capabilities. Over time, agents can reason across domains through shared identity, memory, workflows, and human primitives.

---

## Products

Products are curated experiences built on top of AONIK Core and one or more domain modules. They package user experience, workflows, branding, customer segmentation, and market-specific capabilities without rebuilding foundational intelligence.

| Product | Domain | Stack |
|---|---|---|
| **Payabo** | B2C personal finance &mdash; budgets, bills, subscriptions, goals | React (web) + Flutter (mobile) |
| **MyBillAfrica** | B2B billing and collections | React (web) |
| **RemitExchange** | Cross-border remittance | React (web) |

---

## Architecture

AONIK is implemented as a **module-first modular monolith**. Each domain module owns its vertical slice &mdash; entities, services, endpoints, persistence configuration &mdash; with a module-scoped DbContext over a shared physical database.

```
src/
  Aonik.SharedKernel/       Cross-cutting primitives, interfaces, integration events, cross-module read contracts
  Aonik.Platform/           Identity, tenancy, party/profile, compliance, notifications
  Aonik.Finance/            Ledger, payments, orders, billing, pricing, partners (B2B / cross-border core)
  Aonik.PersonalFinance/    Households, transactions, bills, budgets, goals, life-graph, customer insights (B2C / Payabo)
  Aonik.Ai/                 Model routing, prompts, user memory, AI execution records
  Aonik.Agents/             Domain agents, orchestration, proposal workflows
  Aonik.Application/        Shared application abstractions
  Aonik.Infrastructure/     EF migrations, external adapters, Qdrant, Quartz
  Aonik.Api/                HTTP API composition root (FastEndpoints)
  Aonik.Api.Contracts/      Shared API contracts
  Aonik.Worker/             Background jobs (Quartz)
  Aonik.Migrator/           Database migration host
  Aonik.AppHost/            .NET Aspire orchestration
  Aonik.ServiceDefaults/    Shared service configuration (OpenTelemetry, health checks)
  Aonik.AdminUi/            Admin interface (React 19, Vite 7, Tailwind CSS 4, Dockview)
  Aonik.AdminDesktop/       Electron desktop wrapper for Admin UI
  Aonik.Cli/                Command-line interface
  Aonik.Finance.Mcp/        Finance MCP server
  Aonik.Platform.Mcp/       Platform MCP server

apps/
  Payabo/                   B2C personal finance web app (React, Vite)
  payabo_mobile/            Payabo mobile app (Flutter, Firebase, Plaid)
  website/                  Marketing website (React, Vite, Framer Motion)
  docs-site/                Documentation site (Fumadocs, OpenAPI)

tests/
  Aonik.SharedKernel.Tests/
  Aonik.Application.Tests/
  Aonik.Infrastructure.Tests/
  Aonik.Api.Tests/
  Aonik.Cli.Tests/
  fixtures/
```

### Module Boundaries

Modules depend on `SharedKernel` for primitives, contracts, and integration events. They do not reference each other directly. Cross-module communication uses:

- **SharedKernel contracts** &mdash; Interfaces like `IPartyService`, `IComplianceService` that one module implements and another consumes via DI.
- **SharedKernel read contracts** &mdash; Thin readers like `ICustomerOrderHistoryReader`, `ICustomerInvoiceHistoryReader`, `ICustomerPaymentHistoryReader`, `IFxQuoteReader` (in `SharedKernel.Abstractions.Finance/`) and `IPartyReader`, `IUserDirectoryReader` (in `SharedKernel.Abstractions.Platform/`). Implementations live in the owning module; consumers depend on the contract, not the entity.
- **Integration events** &mdash; `TenantProvisionedEvent`, `OrderCreatedEvent`, `PaymentCompletedEvent`, etc. Published by one module, subscribed to by others.
- **Read models** &mdash; Lightweight projections for cross-module queries where eventual consistency is acceptable.

Each module registers itself in the API composition root:

```csharp
services.AddPlatformModule(configuration);
services.AddFinanceModule(configuration);
services.AddPersonalFinanceModule(configuration);
services.AddAiModule(configuration);
services.AddAgentsModule(configuration);
```

### Design Principles

- **Shared intelligence over isolated silos.** Modules interoperate through shared context, workflows, and orchestration.
- **Human-centred assistance.** AONIK should help people feel clearer, steadier, more capable, and more in control.
- **Explainability over black-box behaviour.** Recommendations, actions, workflows, and decisions must be explainable.
- **Agents propose; systems execute.** Every material action follows Propose, Approve, Apply.
- **Every AI action is auditable.** Every execution is an `AiRun`. Financially material outputs reference an `AiRunId`.
- **Risk tier determines AI autonomy.** Human approval is explicit for high-risk actions.
- **Ledger is the source of financial truth.** Double-entry, immutable.
- **Orders represent business intent.** They are not payments.
- **Modules own their boundaries.** No cross-module project references. Contracts and events only.
- **Domain entities are anemic.** All business logic lives in services, not entities.

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server (LocalDB is used by default in development)
- [Docker](https://www.docker.com/products/docker-desktop) (for Qdrant vector store)
- [Node.js](https://nodejs.org/) (for Admin UI and consumer apps)
- Git

### Quick Start with Aspire

The fastest way to run the full stack locally:

```bash
git clone https://github.com/michaeljosiah/aonik.git
cd aonik

# Start everything (API + Worker + Qdrant + Admin UI + Payabo)
dotnet run --project src/Aonik.AppHost
```

This starts:

| Service | URL |
|---|---|
| API (HTTPS) | `https://localhost:5001` |
| Admin UI | `http://localhost:5173` |
| Payabo | `http://localhost:5174` |
| Qdrant REST | `http://localhost:6333` |
| Swagger | `https://localhost:5001/swagger` |

Aspire automatically provisions SQL Server (LocalDB), starts Qdrant with Docker, configures connection strings, runs migrations, and seeds base data.

### Build and Run Individually

```bash
# Build
dotnet build Aonik.sln

# Initialize database (runs migrations + seeds global base data)
dotnet run --project src/Aonik.Migrator

# Run the API
dotnet run --project src/Aonik.Api

# Run the Admin UI
cd src/Aonik.AdminUi && npm install && npm run dev

# Run Payabo
cd apps/Payabo && npm install && npm run dev
```

### Run Tests

```bash
# All tests
dotnet test Aonik.sln

# Specific project
dotnet test tests/Aonik.Application.Tests

# Specific test
dotnet test --filter "DisplayName~CreateInvoice"
```

---

## Technology

| Layer | Stack |
|---|---|
| Runtime | .NET 10 |
| API | FastEndpoints |
| ORM | Entity Framework Core 10 |
| Database | SQL Server |
| Vector Store | Qdrant (semantic search, RAG, user memory) |
| AI/Agents | Microsoft Agent Framework, MCP, OpenAI |
| Background Jobs | Quartz.NET |
| Orchestration | .NET Aspire |
| Caching | FusionCache |
| Auth | Auth0, Azure Entra ID (MSAL) |
| Observability | OpenTelemetry (metrics, traces) |
| Admin UI | React 19, Vite 7, Tailwind CSS 4, Dockview, Radix UI |
| Mobile | Flutter, Firebase, Plaid |
| Testing | xUnit, FluentAssertions |
| IaC | Bicep (Azure Container Apps) |
| CI/CD | GitHub Actions (9 workflows) |
| Docs | Fumadocs, MDX, OpenAPI |

---

## Admin UI

The Admin UI (`src/Aonik.AdminUi`) is a workspace-oriented dashboard built with React 19, Vite 7, and Tailwind CSS 4. It uses Dockview for a multi-panel workspace layout and Radix UI for accessible primitives.

Key features:
- **Workspace mode** &mdash; Multi-panel layout with drag-and-drop, split views, and workspace templates
- **Agent Playground** &mdash; Interactive chat with domain agents, tool toggle, model comparison, and scenario management
- **Settings** &mdash; Combined tabbed settings page with cross-tab search, conditional visibility, and inline help
- **Financial Life Graph** &mdash; React Flow-based visualisation of financial relationships and obligations
- **Text-to-Speech** &mdash; AI-powered audio generation for content blocks
- **Auth0 integration** &mdash; Multi-tenant login with organisation selection

---

## CI/CD

GitHub Actions workflows handle the full lifecycle:

| Workflow | Purpose |
|---|---|
| `ci.yml` | Build, test, and validate on push/PR |
| `cd-images.yml` | Build and publish container images |
| `cd-deploy.yml` | Deploy to environments (dev, staging, prod) |
| `cd-infra.yml` | Provision Azure infrastructure via Bicep |
| `cd-migrate.yml` | Run database migrations against target environments |
| `release.yml` | Release automation |
| `docs.yml` | Build and publish documentation |
| `drift-detection.yml` | Detect IaC drift in deployed environments |
| `lint.yml` | Code quality checks |

---

## Documentation

| Document | Description |
|---|---|
| [CLAUDE.md](CLAUDE.md) | Claude Code guidelines, architecture rules, and build commands |
| [Architecture Overview](docs/architecture/overview.md) | System design and module boundaries |
| [Module Organization](docs/architecture/module-organization.md) | How code is structured within modules |
| [Technology Stack](docs/architecture/technology-stack.md) | Detailed technology choices and rationale |
| [ADR-005: Modular Monolith](docs/decisions/005-adopt-module-first-modular-monolith.md) | Why and how AONIK is module-first |
| [ADR-006: Extract PersonalFinance](docs/decisions/006-extract-personal-finance-module.md) | Splitting PersonalFinance out of Finance as a sibling module |
| [Financial Life Graph](docs/features/financial-life-graph.md) | Personal-finance node-edge graph and inferred proposals |
| [Insight Generation Pipeline](docs/features/insight-generation-pipeline.md) | Customer-insight snapshots and AI summaries |
| [Transaction Classification](docs/features/transaction-classification.md) | Categorisation rules and AI-assisted classification |
| [Authentication](docs/features/authentication-authorization.md) | Auth0/Entra ID setup, endpoint security |
| [Ledger](docs/features/ledger.md) / [Billing](docs/features/billing.md) / [Payments](docs/features/payments.md) / [Pricing](docs/features/pricing.md) | Finance domain feature guides |
| [CHANGELOG](CHANGELOG.md) | Version history |

End-user / operator documentation lives in the docs site (`apps/docs-site/`) under `content/docs/{operate,tenant-admin,api}`. Architecture-level documents (ADRs, contributor guides, specifications) live under `docs/` in this repo.

---

## Contributing

AONIK is open to contributions. The project is evolving &mdash; expect refactors and breaking changes.

Before submitting code:
1. `dotnet build Aonik.sln` must succeed with 0 errors
2. `dotnet test Aonik.sln` must pass
3. Follow the standards in [CLAUDE.md](CLAUDE.md)
4. Update [CHANGELOG.md](CHANGELOG.md) with your changes

See [Contributing Guide](docs/contributing/) for more detail.

---

## License

Apache License, Version 2.0. See [LICENSE](LICENSE).

---

<p align="center">
  <strong>Agents propose. Systems apply. Humans stay in control.</strong>
</p>
