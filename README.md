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

**Vector Store (Qdrant)** — Semantic search over document embeddings for retrieval-augmented generation (RAG). Agents retrieve domain context before reasoning, improving decision quality. Multi-tenant isolation ensures document privacy across tenants.

**Agent Framework** — Domain-specific agents built on Microsoft Agent Framework. Agents reason, plan, and use tools — but they never directly mutate state. Mutating tools are wrapped with `ApprovalRequiredAIFunction` for human-in-the-loop approval:

```
Agent calls mutating tool  -->  ApprovalRequiredAIFunction gates  -->  Human or policy approves  -->  Tool executes
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

The Finance module has its own domain agents (`FinanceAgentDescriptor` and `FinancialLifeGraphAgentDescriptor`) and MCP server (`Aonik.Finance.Mcp`) for tool interoperability with the agent framework.

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
- [Docker](https://www.docker.com/products/docker-desktop) (for Qdrant vector store)
- Git

### Build and Run

```bash
git clone https://github.com/anomalyco/aonik.git
cd aonik

# Build
dotnet build Aonik.sln

# Initialize database (runs migrations + seeds global base data)
dotnet run --project src/Aonik.Migrator

# Run the API
dotnet run --project src/Aonik.Api
```

API starts on `https://localhost:5001` with Swagger at `/swagger`.

For local-only development, `src/Aonik.Api/appsettings.Development.json` also enables startup auto-migrate/seed. The migrator remains the deterministic first-install path.

### Run with Qdrant Vector Store (Local Orchestration)

For RAG-enabled agents and document retrieval, run the full stack with Qdrant using .NET Aspire:

```bash
# Start the orchestrated environment (API + Worker + Qdrant + Admin UI)
dotnet run --project src/Aonik.AppHost

# Aspire dashboard available at http://localhost:17070
# API available at https://localhost:5001
# Admin UI available at http://localhost:5173
# Qdrant REST API available at http://localhost:6333
```

Aspire automatically:
- Starts SQL Server and Qdrant containers
- Configures all services with correct connection strings
- Sets up volume mounts for Qdrant persistence
- Monitors service health and logs

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
| Vector Store | Qdrant (semantic search, RAG) |
| AI/Agents | Microsoft Agent Framework, MCP, OpenAI Embeddings |
| Background Jobs | Quartz.NET |
| Orchestration | .NET Aspire |
| Caching | FusionCache |
| Observability | OpenTelemetry (metrics, traces) |
| Admin UI | React 19, Vite, Tailwind CSS, Dockview |
| Testing | xUnit, FluentAssertions |
| IaC | Bicep (Azure Container Apps + App Service) |
| CI/CD | GitHub Actions |

---

## Documentation

| Document | Description |
|---|---|
| [CLAUDE.md](CLAUDE.md) | Claude Code guidelines, architecture rules, and build commands |
| [Architecture Overview](docs/architecture/overview.md) | System design and module boundaries |
| [Module Organization](docs/architecture/module-organization.md) | How code is structured within modules |
| [Technology Stack](docs/architecture/technology-stack.md) | Detailed technology choices and rationale |
| [Vector Store Guide](docs/features/vector-store.md) | Qdrant integration, RAG context retrieval, and document indexing |
| [Agent Framework](docs/features/agents.md) | Domain agents, MCP tools, and proposal workflows |
| [AONIK CLI Guide](docs/guides/aonik-cli.md) | Using the command-line client for AONIK systems |
| [Testing Guide](docs/Testing.md) | Testing patterns, conventions, and examples |
| [Troubleshooting](docs/Troubleshooting.md) | Common issues and solutions |
| [API Authentication](docs/features/authentication-authorization.md) | Auth setup, local usage, and endpoint security |
| [CHANGELOG](CHANGELOG.md) | Version history |

---

## Vector Store and RAG

AONIK includes **Qdrant vector store** integration for retrieval-augmented generation (RAG) capabilities. Agents can retrieve domain context from indexed documents before making decisions, improving answer quality and relevance.

### Architecture

- **Qdrant** — Open-source vector database for semantic search over document embeddings
- **OpenAI Embeddings** — Text-embedding-3-small (1536 dimensions) for document and query embeddings
- **RagContextProvider** — Agent framework integration that embeds queries and retrieves similar documents
- **Multi-tenancy** — Document vectors are tenant-isolated; search results respect tenant boundaries
- **Observability** — OpenTelemetry metrics track search latency, result counts, and embedding API performance

### Document Upload and Indexing

```bash
# Upload a document for agent retrieval
POST /ai/documents/upload
Content-Type: multipart/form-data

Document: <binary file>
SourceName: "customer-agreement-v2"
```

The endpoint:
1. Chunks the document into 512-token segments with 100-token overlap
2. Generates embeddings for each chunk via OpenAI API
3. Stores vectors in Qdrant with tenant and source metadata
4. Returns chunk count and embedding cost

### Agent Usage

```csharp
// In agent code, inject RagContextProvider
var context = await ragContextProvider.GetContextAsync(
    query: "What are the payment terms?",
    collectionType: "documents",
    topK: 5,
    scoreThreshold: 0.6f
);

// context contains the 5 most similar document chunks
// Pass to LLM prompt: "Given this context, answer the question..."
```

### Development and Deployment

- **Local:** Run `dotnet run --project src/Aonik.AppHost` to start Qdrant with Docker Compose
- **Dev Azure:** Deploy to Container Apps with `dotnet run --project src/Aonik.AppHost` or GitHub Actions
- **Deterministic Embeddings:** Development uses mock embeddings (no API key required). Production uses real OpenAI API

See [Vector Store Guide](docs/features/vector-store.md) for detailed configuration and examples.

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
