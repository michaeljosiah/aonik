# Architecture Decision Records (ADRs)

This directory contains Architecture Decision Records documenting significant architectural and design decisions made in the AONIK project.

## What is an ADR?

An Architecture Decision Record (ADR) captures an important architectural decision made along with its context and consequences. ADRs help teams:

- Understand why decisions were made
- Onboard new team members quickly
- Revisit and evolve decisions when context changes
- Avoid repeating past discussions

## ADR Format

Each ADR includes:

- **Status**: Proposed, Accepted, Deprecated, Superseded
- **Date**: When the decision was made
- **Context**: The situation and forces at play
- **Decision**: The choice that was made
- **Consequences**: The resulting context after applying the decision

## All ADRs

### Active Decisions

| ADR | Title | Date | Status |
|-----|-------|------|--------|
| [002](002-anemic-domain-model.md) | Adopt Anemic Domain Model | 2026-01-08 | Accepted |
| [003](003-no-generic-repository.md) | No Generic Repository Pattern Over EF Core | 2026-01-08 | Accepted |
| [004](004-adopt-microsoft-agent-framework.md) | Adopt Microsoft Agent Framework (MAF) | 2026-02-22 | Accepted |
| [005](005-adopt-module-first-modular-monolith.md) | Adopt Module-First Modular Monolith | 2026-02-24 | Accepted |
| [006](006-extract-personal-finance-module.md) | Extract PersonalFinance into Its Own Sibling Module | 2026-05-19 | In Progress |
| [007](007-keycloak-as-auth-provider.md) | Keycloak as a First-Class Operator-Choice Auth Provider | 2026-05-21 | Accepted |
| [008](008-task-work-item-scheduling.md) | General-Purpose Task Primitive (WorkItem) in Platform | 2026-06-02 | Proposed |
| [009](009-extract-documents-module.md) | Extract Documents into Its Own Sibling Module | 2026-06-02 | Proposed |
| [010](010-partner-owned-connector-credentials.md) | Partner-Owned Connector Credentials | 2026-06-09 | Proposed |

### Superseded/Deprecated

| ADR | Title | Date | Status |
|-----|-------|------|--------|
| [001](001-custom-ai-implementation-vs-maf.md) | Custom AI Implementation vs MAF | 2024-01-01 | Superseded by [004](004-adopt-microsoft-agent-framework.md) |

## Decision Categories

### Domain & Architecture
- [ADR 002: Anemic Domain Model](002-anemic-domain-model.md) - Domain entity design philosophy
- [ADR 005: Module-First Modular Monolith](005-adopt-module-first-modular-monolith.md) - Canonical module architecture and boundaries

### Data Access & Persistence
- [ADR 003: No Generic Repository](003-no-generic-repository.md) - Direct EF Core usage without repository pattern

### AI & Integration
- [ADR 004: Adopt Microsoft Agent Framework](004-adopt-microsoft-agent-framework.md) - MAF for agents, tools, MCP servers
- [ADR 001: Custom AI Implementation](001-custom-ai-implementation-vs-maf.md) - ~~Original AI framework choice~~ (superseded by 004)

## Creating New ADRs

When making a significant architectural decision:

1. **Create a new ADR file**: `00X-short-title.md`
2. **Use the template** from existing ADRs
3. **Include**:
   - Context: What problem are we solving?
   - Decision: What choice did we make?
   - Rationale: Why did we make this choice?
   - Consequences: What are the trade-offs?
   - Examples: Show code demonstrating the decision
   - References: Links to related docs or external resources
4. **Update this README** with the new ADR in the table above
5. **Commit with descriptive message**: "Add ADR XXX: [Title]"

## Decision Principles

AONIK's architectural decisions are guided by:

1. **Pragmatism over Purity**: Choose what works over theoretical ideals
2. **YAGNI** (You Aren't Gonna Need It): Don't add complexity speculatively
3. **Modern .NET Best Practices**: Follow current Microsoft guidance
4. **Developer Experience**: Optimize for team velocity and maintainability
5. **Measurable Outcomes**: Base decisions on evidence, not assumptions

## Reviewing Decisions

Each ADR includes a "Next Review Date". At that time:

1. Assess if the context has changed
2. Check if the decision still serves us
3. Update the ADR's status if needed (Deprecated, Superseded)
4. Extend the review date if decision remains valid

## Related Documentation

- [../README.md](../README.md) - Main documentation index
- [../guides/application-services.md](../guides/application-services.md) - Service development guidelines
- [../../AGENTS.md](../../AGENTS.md) - Coding guidelines for AI agents and developers

## Questions?

If you have questions about any architectural decision:

1. Check the ADR's "Context" and "Rationale" sections
2. Look at the code examples provided
3. Reach out to the development team
4. Consider proposing a new ADR if context has changed
