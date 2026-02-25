# Architecture Overview

AONIK is a **module-first modular monolith** — an AI-native financial platform where each business domain lives in a self-contained module project.

See [ADR-005](../decisions/005-adopt-module-first-modular-monolith.md) for the full decision record.

## Key Characteristics

- **Module-first**: Each domain owns its entities, services, endpoints, and persistence in a single project
- **Anemic domain model**: Entities are data containers; all business logic lives in services
- **Module-scoped DbContexts**: Each module owns a DbContext over a shared physical database
- **AI-native**: Microsoft Agent Framework (MAF) for agents, tools, and MCP servers
- **Auditable**: All AI operations are traceable; proposal pattern for high-risk actions

## Module Map

| Module | Project | DbContext | Purpose |
|--------|---------|-----------|---------|
| Platform | `Aonik.Platform` | `PlatformDbContext` | Identity, tenancy, party/profile, compliance, notifications, operations |
| Finance | `Aonik.Finance` | `FinanceDbContext` | Ledger, payments, billing, orders, pricing, partner network |
| AI | `Aonik.Ai` | `AiDbContext` | AI routing, prompts, model management, execution records |
| Agents | `Aonik.Agents` | `AgentsDbContext` | Agent definitions, orchestration, proposals, workflows |

## Host / Composition Projects

| Project | Purpose |
|---------|---------|
| `Aonik.SharedKernel` | Cross-cutting primitives: `Entity`, `AuditableEntity`, `ITenantScoped`, `Money`, `Result<T>` |
| `Aonik.Application` | Thin remaining shim (background jobs, `IAonikDbContext` for migrations) |
| `Aonik.Infrastructure` | External adapters, auth, persistence (`AonikDbContext` for EF migrations), DI composition |
| `Aonik.Api` | Composition root — references all modules, registers endpoints, middleware |
| `Aonik.Worker` | Background job host (Quartz) |
| `Aonik.Migrator` | EF Core migration runner |
| `Aonik.AppHost` | .NET Aspire orchestration |
| `Aonik.ServiceDefaults` | Aspire service defaults |

## AI / MCP Projects

| Project | Purpose |
|---------|---------|
| `Aonik.Finance.Mcp` | Finance domain MCP server |
| `Aonik.Platform.Mcp` | Platform domain MCP server |

## Admin UI

| Project | Purpose |
|---------|---------|
| `Aonik.AdminUi` | React SPA with module extension system (React 19, Vite, Tailwind, Dockview) |

## Further Reading

- [Module Organization](module-organization.md) — module anatomy and boundary rules
- [Data Flow](data-flow.md) — request/response patterns
- [Technology Stack](technology-stack.md) — frameworks and tools
- [ADR-005: Modular Monolith](../decisions/005-adopt-module-first-modular-monolith.md) — architectural decision record
