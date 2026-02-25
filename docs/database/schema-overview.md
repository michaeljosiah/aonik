# Schema Overview

AONIK uses EF Core to map domain entities to a SQL Server schema, organized across four domain modules sharing a single physical database.

## Key points

- **Migrations DbContext**: `src/Aonik.Infrastructure/Persistence/AonikDbContext.cs` (monolithic, aggregates all module configurations)
- **Migrations**: `src/Aonik.Infrastructure/Persistence/Migrations/`
- **Module DbContexts** (runtime, internal to each module):
  - `PlatformDbContext` — `src/Aonik.Platform/Persistence/PlatformDbContext.cs`
  - `FinanceDbContext` — `src/Aonik.Finance/Persistence/FinanceDbContext.cs`
  - `AiDbContext` — `src/Aonik.Ai/Persistence/AiDbContext.cs`
  - `AgentsDbContext` — `src/Aonik.Agents/Persistence/AgentsDbContext.cs`
- **Base class**: All DbContexts inherit from `AonikDbContextBase` in SharedKernel, which provides tenant query filters, audit stamping, and soft-delete handling.

## Modules

The schema is organized by domain modules:

| Module | Subdomains |
|--------|-----------|
| **Platform** | Identity, Party, Compliance, Notifications, Settings, Features, Reference Data, CMS, Autonumbering |
| **Finance** | Billing, Ledger, Payments, Orders, Partners, Pricing, Personal Finance, Catalog |
| **AI** | Providers, Models, Policies, Prompts, Tools, Runs, Evals, Insights, Signals |
| **Agents** | Agents, Agent Runs, Orchestrator Policies, Proposals |

All tables currently use the `dbo` schema for backward compatibility with existing migrations.

For migration commands, see [Database Migrations](../guides/database-migrations.md).
