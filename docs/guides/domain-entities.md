# Domain Entities

AONIK uses an **anemic domain model**.

## What this means

- Entities are **data containers** only.
- No business logic methods on entities.
- No constructors enforcing invariants.
- Business logic lives in **application services** within each module.

## Entity conventions

- Public `{ get; set; }` properties
- Collections are `List<T>` with public get/set
- Nullable reference types are respected (`string?` when applicable)
- Inherit from `Entity` (provides `Guid Id`) or `AuditableEntity` (adds `CreatedAt/By`, `UpdatedAt/By`)
- Tenant-scoped entities implement `ITenantScoped`

## Where entities live

Entities are co-located in their owning module project:

- `src/Aonik.Platform/Entities/` — Identity, Party, Compliance, Notifications, etc.
- `src/Aonik.Finance/Entities/` — Billing, Ledger, Payments, Orders, Partners, Pricing, etc.
- `src/Aonik.Ai/Entities/` — AI providers, models, prompts, runs, evals, etc.
- `src/Aonik.Agents/Entities/` — Agents, proposals, orchestrator policies

See `AGENTS.md` for the authoritative rules and examples.
