# AONIK Architecture

> **This document is a legacy reference.** For current architecture documentation, see the files below.

AONIK is an **AI-native intelligence platform** built as a **module-first modular monolith**. AONIK Core provides shared orchestration, memory, governance, agents, and AI routing; Finance and PersonalFinance are the first shipped domain modules. Each implementation module lives in a self-contained project that owns its entities, services, endpoints, and persistence.

## Current Architecture Documentation

- **[Architecture Overview](architecture/overview.md)** — high-level module map and project structure
- **[Module Organization](architecture/module-organization.md)** — module anatomy, boundary rules, inter-module communication
- **[Data Flow](architecture/data-flow.md)** — request/response patterns
- **[Technology Stack](architecture/technology-stack.md)** — frameworks and tools
- **[ADR-005: Modular Monolith](decisions/005-adopt-module-first-modular-monolith.md)** — architectural decision record

## Core Principles

1. **Ledger is the source of financial truth** (double-entry, immutable)
2. **Orders represent business intent**, not payments
3. **Payments execute intent; ledger proves it**
4. **Agents propose; systems execute** (proposal/approval/apply pattern)
5. **Every AI action is auditable and policy-governed**
6. **Risk tier determines AI autonomy**
7. **Human approval is explicit for high-risk actions**

## Project Structure

```
src/
├── Aonik.SharedKernel/          # Cross-cutting primitives
├── Aonik.Platform/              # Platform module (identity, tenancy, party, compliance)
├── Aonik.Finance/               # Finance module (ledger, payments, billing, orders, pricing)
├── Aonik.Ai/                    # AI module (providers, models, routing, prompts, runs)
├── Aonik.Agents/                # Agents module (orchestration, proposals, workflows)
├── Aonik.Application/           # Thin shim (background jobs, migration compatibility)
├── Aonik.Infrastructure/        # External adapters, auth, EF migrations
├── Aonik.Api/                   # Composition root
├── Aonik.Worker/                # Background job host (Quartz)
├── Aonik.Migrator/              # EF Core migration runner
├── Aonik.AppHost/               # .NET Aspire orchestration
├── Aonik.ServiceDefaults/       # Aspire service defaults
├── Aonik.Finance.Mcp/           # Finance MCP server
├── Aonik.Platform.Mcp/          # Platform MCP server
└── Aonik.AdminUi/               # React admin UI

tests/
├── Aonik.SharedKernel.Tests/
├── Aonik.Infrastructure.Tests/
├── Aonik.Application.Tests/
└── Aonik.Api.Tests/
```

See [AGENTS.md](../AGENTS.md) for build commands, coding standards, and the pre-commit checklist.
