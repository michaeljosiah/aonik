# ADR-005: Adopt Module-First Modular Monolith as the Canonical Architecture

**Status**: Accepted  
**Date**: 2026-02-24  
**Decision Makers**: Development Team  
**Supersedes**: Module organization guidance in `docs/architecture/module-organization.md`  
**Related**: [Modular Restructuring Plan](../modular-restructuring-plan.md), [ADR-004](004-adopt-microsoft-agent-framework.md)

## Context

AONIK started with a layered Clean Architecture layout and a broad monolithic persistence surface (`IAonikDbContext`/`AonikDbContext`). As Platform, Finance, AI, and Agent capabilities grew, this structure created pressure in four areas:

1. **Boundary erosion**: services/endpoints/configuration were split across layers rather than owned by domains.
2. **Coupled persistence**: a single large context made domain extraction and ownership harder.
3. **AI integration sprawl**: agents and AI workflows needed explicit module boundaries and contracts.
4. **Product expansion**: AONIK must support multiple products (Payabo, MyBillAfrica, RemitExchange) from one governed core.

The restructuring plan (`docs/modular-restructuring-plan.md`) was executed to establish module ownership while keeping the deployment model simple (single deployable monolith).

## Decision

Adopt a **module-first modular monolith** as AONIK's canonical architecture.

### Canonical Modules

- **SharedKernel**: cross-cutting primitives, interfaces, events, tenant abstractions.
- **Aonik.Platform**: identity/access, tenancy, party/profile, settings, reference data, compliance, notifications.
- **Aonik.Finance**: ledger, payments, orders, billing/invoicing, pricing, partners, personal finance.
- **Aonik.Ai**: AI route policy, prompt/model abstractions, AI execution records (`AiRun`).
- **Aonik.Agents**: domain agents, orchestration, proposal/approval/apply scaffolding.
- **Infrastructure + composition roots** (`Api`, `Worker`, `Migrator`): adapters and runtime wiring.

### Module Boundary Rules

1. Entities remain **anemic**; business logic lives in services.
2. Implementations are `internal` by default; public surface is exposed via `Contracts/`.
3. Module-scoped DbContexts are primary (`PlatformDbContext`, `FinanceDbContext`, `AiDbContext`, `AgentsDbContext`) over a shared physical SQL database.
4. Inter-module interaction uses contracts/events/read models, not direct deep references.
5. AI actions remain auditable, policy-governed, and aligned with proposal flow for material actions.

## Rationale

This architecture gives AONIK:

- **Domain ownership clarity**: each module owns its entities, services, endpoints, and persistence configurations.
- **Safer evolution**: module boundaries reduce blast radius for changes.
- **AI-native alignment**: dedicated AI/Agents modules map directly to platform principles (auditable runs, governed autonomy).
- **Operational simplicity**: monolith deployment keeps local/dev/test workflows straightforward while preserving modularity.
- **Future extraction path**: module boundaries make selective service extraction feasible if scale requires it.

## Consequences

### Positive

- Improved maintainability and onboarding with explicit module ownership.
- Better testability through module-focused slices and contracts.
- Cleaner composition roots with explicit module registration.

### Trade-offs

- Additional cross-module contract/read-model maintenance.
- Temporary duplication during migrations (for compatibility windows) may occur.
- Internal/public boundary management (`InternalsVisibleTo`) requires discipline.

## Implementation Notes

- Modular restructuring Phases 0-6 are complete per the progress checklist.
- Legacy `Domain`/`Domain.Tests` project stubs were removed.
- Legacy API endpoint/contract placement in `Aonik.Api` was migrated to module-owned endpoints/contracts.
- Legacy Infrastructure seed service implementations were migrated to Platform module ownership.

## References

- [Modular Restructuring Plan](../modular-restructuring-plan.md)
- [ADR-004: Adopt Microsoft Agent Framework](004-adopt-microsoft-agent-framework.md)
- [AGENTS.md](../../AGENTS.md)
