---
title: Aonik
description: Self-hosted, open-core AI-native financial platform. Run your own tenants, wire your own integrations, ship branded products on top.
sidebar_label: Home
sidebar_position: 1
slug: /
---

# Run your own AI-native financial platform

Aonik is the open-core platform that powers [Payabo](products/payabo/index.md), [MyBillAfrica](products/mybillafrica/index.md), and [RemitExchange](products/remitexchange/index.md). You self-host it, configure identity and integrations, and ship branded financial products on top.

These docs are for **operators** — engineers deploying Aonik, configuring tenants, wiring integrations, and keeping the platform healthy. If you are looking for end-user help with the Payabo app, head to the Payabo help centre instead.

[Get started in 5 steps →](getting-started/quickstart.md)

## What's in the box

Aonik ships as a .NET 10 modular monolith fronted by [FastEndpoints](https://fast-endpoints.com), a React 19 admin UI, a .NET CLI, and a Flutter mobile app. Out of the box you get:

- **Identity & multi-tenancy** — Auth0 or Microsoft Entra ID, tenant-scoped data on every entity
- **Money movement** — orders, payment intents, double-entry ledger, billing & invoicing
- **Personal finance** — Plaid account linking, transactions, financial life graph, household sharing
- **AI agents** — domain agents with chat and voice surfaces, human-in-the-loop approval for any mutating action
- **Operations** — Quartz-scheduled jobs, audit logs, OpenTelemetry traces

[See the full capability matrix →](getting-started/what-you-get.md)

## Pick your path

- **New to Aonik?** Start with the [Quickstart](getting-started/quickstart.md).
- **Setting up identity?** Browse [Identity & Access](identity-access/index.md).
- **Wiring third-party services?** See [Integrations](integrations/index.md).
- **Configuring a product?** Choose [Payabo](products/payabo/index.md), [MyBillAfrica](products/mybillafrica/index.md), or [RemitExchange](products/remitexchange/index.md).
- **Looking up an API endpoint?** Open the [API Reference](/api/aonik-api).
- **Contributing code?** Head to [For Contributors](for-contributors/index.md).

## A note on this rewrite

These docs were rewritten in 2026 to focus on operators. The previous developer-focused content has been preserved under [Legacy docs](legacy/old-home.md) and will be retired once the new sections are complete. If you land on a page with a yellow banner, the topic has a newer home in the main sidebar.
