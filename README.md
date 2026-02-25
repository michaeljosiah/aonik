<p align="center">
  <img src="docs/images/hero-banner.png" alt="AONIK" width="100%">
</p>

<h1 align="center">AONIK</h1>

<p align="center">
  <strong>AI-native financial infrastructure.</strong><br>
  Ledger. Payments. Agents. One platform.
</p>

<p align="center">
  <code>.NET 10</code> &middot; <code>SQL Server</code> &middot; <code>FastEndpoints</code> &middot; <code>EF Core 10</code> &middot; <code>Microsoft Agent Framework</code>
</p>

<p align="center">
  <em>Early development &mdash; APIs and data models are evolving. Breaking changes expected.</em>
</p>

---

## What is AONIK?

AONIK is an open-source financial operating system designed to power payments, remittances, billing, and personal finance — with AI embedded at the infrastructure level, not bolted on.

It provides three things traditional fintech platforms don't:

1. **A double-entry ledger as the source of truth.** Every financial state change is an immutable, auditable journal entry. Not an afterthought — the foundation.

2. **AI agents that operate on financial primitives directly.** Agents read ledger data, classify transactions, detect anomalies, and generate insights — but they never mutate financial state on their own.

3. **A proposal-based control model.** Agents propose. Domain services apply. Humans approve what matters. Every AI action is recorded, policy-governed, and auditable.

AONIK is the platform layer. Products are built on top:

| Product | Domain |
|---|---|
| **Payabo** | B2C personal finance — budgets, bills, subscriptions, goals |
| **MyBillAfrica** | B2B billing and collections |
| **RemitExchange** | Cross-border remittance |

---

## Architecture

AONIK is a **module-first modular monolith** — not microservices, not a monolith pretending to be modular. Each domain module owns its entities, services, endpoints, and persistence configuration as vertical slices.

```
src/
  Aonik.Platform/       Identity, tenancy, party/profile, compliance, notifications
  Aonik.Finance/        Ledger, payments, orders, billing, pricing, partners
  Aonik.Ai/             Model routing, prompts, AI execution records
  Aonik.Agents/         Domain agents, orchestration, proposal workflows
  Aonik.SharedKernel/   Cross-cutting primitives, interfaces, events
  Aonik.Infrastructure/  External adapters, EF migrations, composition support
  Aonik.Api/            HTTP API (FastEndpoints)
  Aonik.Worker/         Background jobs (Quartz)
  Aonik.Migrator/       Database migration host
  Aonik.AppHost/        .NET Aspire orchestration
  Aonik.AdminUi/        Admin interface (React 19, Vite, Tailwind)
  Aonik.Finance.Mcp/    Finance MCP server
  Aonik.Platform.Mcp/   Platform MCP server

tests/
  Aonik.SharedKernel.Tests/
  Aonik.Application.Tests/
  Aonik.Infrastructure.Tests/
  Aonik.Api.Tests/
```

Each module has its own scoped DbContext (`PlatformDbContext`, `FinanceDbContext`, `AiDbContext`, `AgentsDbContext`) over a shared physical SQL Server database. Modules communicate through in-process integration events and shared contracts in SharedKernel.

---

## Core Design Principles

These are non-negotiable. Code that violates them gets rejected.

- **Ledger is the source of financial truth.** Double-entry, immutable.
- **Orders represent business intent.** They are not payments. They capture *why* money moves, link parties, and reference both funding and fulfilment.
- **Payments execute intent; ledger proves it.**
- **Agents propose; systems execute.** All material actions follow: Propose → Approve → Apply.
- **Every AI action is auditable and policy-governed.** Every AI execution is recorded as an `AiRun`. Financially material outputs reference an `AiRunId`.
- **Risk tier determines AI autonomy.** Human approval is explicit for high-risk actions.

---

## AI and Agent Architecture

AONIK's AI layer is not a chatbot wrapper. It is structured infrastructure:

**AI Platform (`Aonik.Ai`)**
- Multiple LLM providers and models — no hard-coded model usage
- All calls resolve through `AiRoutePolicy` for routing and governance
- Prompts and tools are versioned; prompts are immutable once published
- Every execution is recorded as an `AiRun`

**Agent Framework (`Aonik.Agents`)**
- Built on Microsoft Agent Framework (MAF)
- Agents are constrained, domain-specific actors — they reason, plan, and use tools
- Agents never directly mutate financial state
- MCP servers per domain module expose tools safely to agents

**The Proposal Pattern** (mandatory for all material actions):
```
Agent creates Proposal → Human or policy approves → Domain service applies
```
This flow is never bypassed.

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

## Project Status

AONIK is in active early development. The platform core is functional:

- Double-entry ledger with journal entries and account management
- Payment intents, payment processing, and order orchestration
- Multi-tenant billing and invoicing with line items
- Party/profile management with KYC/KYB scaffolding
- AI model routing, prompt versioning, and execution recording
- Agent proposal framework with policy-governed approval
- Admin UI with module extension system
- Azure IaC with ACA and App Service deployment profiles
- Containerized deployment (API, Worker, Admin UI)

What is not built yet is clearly not claimed. The roadmap lives in the code, not in promises.

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
