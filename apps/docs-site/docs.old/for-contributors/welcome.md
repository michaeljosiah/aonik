---
title: Welcome (contributors)
description: Audience, scope, and ground rules for code contributors to Aonik.
sidebar_label: Welcome
sidebar_position: 2
---

# Welcome to the contributor section

:::info
You're writing code in the Aonik repository. This section explains the architectural rules you must follow, where the decisions are documented, and how to get changes merged.
:::

## Who this is for

If you are:

- Adding a feature to a module
- Generating an EF Core migration
- Wiring a new tool to an agent
- Touching the Admin UI or one of the workspace apps
- Reviewing or merging a pull request

...this section is for you. If you are running Aonik for a tenant, go back to the [main docs](../index.md) — none of the pages in this section are written with operators in mind.

## Non-negotiable rules

These rules predate the docs rewrite and apply to every code change. They are documented in detail in `AGENTS.md` at the repo root and in the ADRs.

1. **Ledger is the source of financial truth.** Double-entry, immutable. Never delete or update a journal entry.
2. **Orders are the canonical record of a requested financial service.** Don't collapse orders into payments or ledger entries.
3. **Agents propose; systems apply.** Every mutating tool is wrapped with `ApprovalRequiredAIFunction`. No exceptions.
4. **Every AI action is auditable** through an `AiRun` record.
5. **Risk tier determines AI autonomy.** Higher-risk operations require higher-approval roles.
6. **Domain entities are anemic.** All business logic lives in services, not entities.
7. **Single migration stream.** All EF Core migrations go through `AonikDbContext`. Module-scoped contexts (`PlatformDbContext`, `FinanceDbContext`, `AiDbContext`, `AgentsDbContext`) have no migration history of their own. The `PlatformDbContext` migration folder is frozen legacy — do not add to it.
8. **Migrations must be tool-generated.** Agents (Claude, Copilot, anyone) never hand-write migration files. Use `dotnet ef migrations add` exclusively. Hand-written migrations cause snapshot drift and cascading breakage.

## Where the depth lives

The current detailed contributor material — module breakdowns, patterns, testing strategy, ADR rationale — sits under [Legacy docs](../legacy/old-home.md). It is being rewritten into this section over Phase 6 of the docs rewrite. Until then, treat the legacy material as authoritative.

A short index of the highest-signal legacy reads:

- **[Architecture.md](../legacy/Architecture.md)** — the whole platform shape in one document
- **[ADR-001 Custom AI vs MAF](../legacy/decisions/001-custom-ai-implementation-vs-maf.md)** — why the AI runtime is shaped the way it is
- **[ADR-002 Anemic domain model](../legacy/decisions/002-anemic-domain-model.md)** — why entities are dumb
- **[ADR-003 No generic repository](../legacy/decisions/003-no-generic-repository.md)** — DbContext directly, no `IRepository<T>`
- **[Testing.md](../legacy/Testing.md)** — xUnit + FluentAssertions conventions, AAA structure, InMemory DB strategy
- **[Pricing](../legacy/features/pricing.md)** — the only legacy feature page already written in a tone close to the new style

## How to get a change merged

1. Read `AGENTS.md` at the repo root. It is the canonical playbook.
2. Open an issue (or a draft PR) before substantial work. Architectural changes should land an ADR first.
3. Generate migrations using `dotnet ef migrations add` against `AonikDbContext` only.
4. Run `dotnet test Aonik.sln` and `dotnet build Aonik.sln` before requesting review.
5. Keep PRs focused. Refactors and feature work go in separate PRs.

## What's next

- [Legacy Architecture](../legacy/Architecture.md) — the canonical contributor read until the new architecture pages ship
- [Legacy ADRs](../legacy/decisions/README.md) — accepted decisions
- [Legacy contributing guides](../legacy/contributing/code-style.md) — code style, git workflow, PRs
