---
title: AI Platform
description: How AI runs in Aonik — providers, route policies, prompts, tools, AiRuns, user memory, RAG, agents, MCP, voice, approval workflows.
sidebar_label: Overview
sidebar_position: 1
---

# AI Platform

:::warning Coming soon
This section is being written in **Phase 4** of the docs rewrite. Until then, the legacy AI integration page covers the broad shape.
:::

## What this section will cover

The full AI runtime, end-to-end:

- **How AI works in Aonik** — the route → run → trace → approval pipeline
- **Providers & models** — registering and rotating OpenAI, Anthropic, Azure OpenAI
- **Route policies** — picking the right model per request
- **Prompts** — immutable, versioned templates with schema
- **Tools & tool catalog** — read vs mutating, `ApprovalRequiredAIFunction` wiring
- **AiRuns and cost tracking** — per-tenant cost guard job
- **User Memory** — SQL or Qdrant backend, recall heuristics
- **RAG with Qdrant** — collections, tenant prefixes, embedding strategy
- **Domain agents** — Finance, Personal Finance, Obligation Planning, Spending Intelligence, Platform
- **MCP servers** — `Aonik.Finance.Mcp`, `Aonik.Platform.Mcp`
- **Voice agents** — the `Aonik.Voice` pipeline, recipes, processors
- **Approval workflows** — server-side approval, proposal lifecycle
- **Playground & scenarios** — the Admin UI playground for QA

## In the meantime

- [Legacy AI integration feature page](../legacy/features/ai-integration.md)
- [Glossary](../getting-started/glossary.md) — AiRun, Tool, Prompt, Proposal, Approval, Agent, MCP
- [Architecture at a glance](../getting-started/architecture-at-a-glance.md) — short summary of the AI pipeline

## What's next

- [Legacy AI integration page](../legacy/features/ai-integration.md)
- [Glossary](../getting-started/glossary.md)
