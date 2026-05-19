---
title: Core Concepts
description: Conceptual reference for the Aonik platform model — modules, tenancy, the money triad, the AI runtime.
sidebar_label: Overview
sidebar_position: 1
---

# Core Concepts

:::warning Coming soon
This section is being written in **Phase 3** of the docs rewrite. Until then, use the pages below for the same ground.
:::

## What this section will cover

Short, focused concept pages — one idea per page — that explain how Aonik is put together:

- The platform model (modules + contracts + integration events)
- Multi-tenancy (`TenantId`, isolation, routing)
- Modules and boundaries (vertical slices, no cross-module references)
- Orders, Payments, Ledger (the canonical money-movement triad)
- Agents propose, systems apply (the human-in-the-loop contract)
- AiRuns, AiPolicies, AiRoutes (every AI call is recorded)
- The single migration stream rule (why only `AonikDbContext`)
- The identity model (Tenant → User → Role → Permission)

Each concept page is short and conceptual. For the lifecycle and config of a specific capability, see [Platform Capabilities](../platform-capabilities/index.md).

## In the meantime

- [Architecture at a glance](../getting-started/architecture-at-a-glance.md) — a one-page summary that covers most of this material at a high level
- [Glossary](../getting-started/glossary.md) — canonical platform vocabulary
- [Legacy Architecture.md](../legacy/Architecture.md) — the 1,000-line deep dive that the new pages will rewrite
- [Legacy ADRs](../legacy/decisions/README.md) — the design decisions that shaped the platform

## What's next

- [Architecture at a glance](../getting-started/architecture-at-a-glance.md)
- [Glossary](../getting-started/glossary.md)
