---
title: Welcome to Aonik
description: What Aonik is, who these docs are for, and what's in scope.
sidebar_label: Welcome to Aonik
sidebar_position: 1
---

# Welcome to Aonik

Aonik is an AI-native, open-core financial platform built on .NET 10. You self-host it and use it to power your own branded financial products. Today it underpins three first-party products: [Payabo](../products/payabo/index.md) (B2C personal finance), [MyBillAfrica](../products/mybillafrica/index.md) (B2B billing), and [RemitExchange](../products/remitexchange/index.md) (cross-border remittance). Nothing prevents you from building a fourth.

## Who these docs are for

These docs are written for **platform operators and self-hosters** — engineers who:

- Run an Aonik deployment for one or more tenants
- Configure identity providers, integrations, and feature flags
- Bootstrap tenants, manage users and roles, and review compliance cases
- Monitor scheduled jobs, audit logs, and AI usage
- Cut releases and roll out schema migrations

If you are an **API integrator** building against a hosted Aonik tenant, the [API Reference](/api/aonik-api) is the primary surface you need. If you want to **contribute code** to Aonik itself, head to [For Contributors](../for-contributors/index.md).

These docs are **not** end-user help for Payabo, MyBillAfrica, or RemitExchange customers. End-user help lives on each product's own help site.

## What you can do with Aonik

A short list of what an operator gets the day they finish the [Quickstart](quickstart.md):

- Multi-tenant identity backed by Auth0 or Microsoft Entra ID
- Double-entry ledger with immutable journal entries
- Orders, payment intents, billing and invoicing
- Personal finance: Plaid-linked accounts, transactions, household sharing, financial life graph
- Domain AI agents (chat + voice) with mandatory human-in-the-loop approval on mutating actions
- A workspace-mode Admin UI for ops, compliance, and configuration
- A .NET CLI for terminal-driven agent and approval workflows
- A Flutter mobile shell wired for Firebase, Plaid, and the voice pipeline

The [Capability matrix](what-you-get.md) shows which products use which modules.

## What's intentionally not in scope here

- **End-user product docs** — covered by each product's own help centre
- **Marketing or pricing content** — see the Aonik website
- **Roadmap or aspirational features** — a feature is documented when it ships, not when it's planned
- **Design specs** — design specifications live under `docs/specifications/` in the repo as engineering artefacts, not user-facing docs

## How these docs are organised

The sidebar follows the lifecycle of a fresh operator:

1. **Getting Started** — orient, then run the [Quickstart](quickstart.md)
2. **Core Concepts** — short conceptual reference for the platform model
3. **Install & Configure** — every supported way to stand Aonik up
4. **Identity & Access** — pick an IdP, wire users and roles
5. **Integrations** — Plaid, Stripe, Twilio, Firebase, ElevenLabs, model providers, Qdrant, webhooks
6. **Products** — per-product configuration (Payabo, MyBillAfrica, RemitExchange)
7. **Platform Capabilities** — reference for every domain capability
8. **AI Platform** — how AI runs, route policies, prompts, tools, approval workflows
9. **Operations** — jobs, observability, deploys, scaling
10. **Admin UI** — workspace mode, playground, settings
11. **CLI** — terminal-based agent and approval workflows
12. **API Reference** — auto-generated from the live OpenAPI spec
13. **For Contributors** — architecture deep dive, ADRs, patterns, contributing workflow

This rewrite is being delivered in phases — some sections currently show a "Coming soon" placeholder. The corresponding [legacy docs](../legacy/old-home.md) remain available until the new section ships.

## What's next

- **Start now.** Run the [Quickstart](quickstart.md) — about 15 minutes from a fresh clone.
- **See the moving parts.** [Architecture at a glance](architecture-at-a-glance.md).
- **Learn the vocabulary.** Skim the [Glossary](glossary.md) before reading any deep page.
