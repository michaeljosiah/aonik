---
title: Payabo
description: The B2C personal finance product — what it does, what it's built from, and how to operate it for a tenant.
sidebar_label: Overview
sidebar_position: 1
---

# Payabo

:::info
Aonik's consumer-facing personal finance product. Web + mobile shells over the platform's personal finance, agent, and voice capabilities.
:::

## Why this matters

If your tenant is going to serve individual consumers — money management, bills, goals, an AI financial assistant — Payabo is the product you'll configure. This page is the operator's mental model: what Payabo gives end-users, what platform capabilities it leans on, and where things are wired up in the codebase.

## What end-users get

A typical Payabo customer signs up, connects their bank accounts via Plaid, and within a few minutes can:

- See their balances, transactions, and recent spending
- Categorise transactions (with AI assistance) and attach receipts
- Set goals and budgets
- Share visibility with household members
- Chat with the **Personal Finance** agent for narrative spending insights
- Talk to the same agent by **voice** through the mobile app
- Approve or reject AI-generated proposals (e.g. "I detected a Netflix subscription — add to your recurring view?")

End-user help for these flows lives on the Payabo help site, not here.

## What the operator configures

You configure Payabo at the **tenant** level. The pieces are:

- **Base currency, supported countries, registration countries** — tenant settings
- **Personal finance feature flags** — which slices of personal finance are exposed (`PersonalFinance.Budgets.Create`, `.Goals.Tracking`, `.Subscriptions.Detection`, `.Bills.Reminders`, etc.)
- **Plaid integration** — sandbox or production, country codes, supported products
- **Voice / TTS profile** — which speech vendor + voice the mobile app uses for tenant agents
- **AI providers & route policies** — which model serves the Personal Finance agent
- **Mobile shell config** — Firebase project, deep link domains, push certs

Walk through them step-by-step in [Configure Payabo for a tenant](configure.md).

## Platform capabilities Payabo uses

| Capability                              | Used | Where it lives |
| --------------------------------------- | :--: | --- |
| Identity, users, roles                  |  ✓   | `src/Aonik.Platform/Services/Identity/` |
| Party & Profile                         |  ✓   | `src/Aonik.Platform/Services/Party/` |
| Registration (phone OTP, KYC)           |  ✓   | `src/Aonik.Platform/Services/Registration/` |
| Personal accounts & transactions        |  ✓   | `src/Aonik.Finance/Services/PersonalFinance/` |
| Plaid account linking                   |  ✓   | `src/Aonik.Finance/Services/PersonalFinance/Plaid*` |
| Statement import                        |  ✓   | `src/Aonik.Finance/Services/PersonalFinance/` |
| Categorisation rules                    |  ✓   | `src/Aonik.Finance/Services/PersonalFinance/` |
| Financial Life Graph                    |  ✓   | `src/Aonik.Finance/Entities/PersonalFinance/FinancialLifeGraph*` |
| Household                               |  ✓   | `src/Aonik.Finance/Services/PersonalFinance/Households/` |
| Orders, payment intents, payments       |  ✓   | `src/Aonik.Finance/Services/{Orders,Payments}/` |
| Ledger                                  |  ✓   | `src/Aonik.Finance/Services/Ledger/` |
| Catalog (billers / services)            |  ✓   | `src/Aonik.Finance/Services/Catalog/` |
| Pricing & FX                            |  ✓   | `src/Aonik.Finance/Services/Pricing/` |
| Notifications                           |  ✓   | `src/Aonik.Platform/Services/Notifications/` |
| Personal Finance agent (chat)           |  ✓   | `src/Aonik.Agents/` |
| Voice pipeline                          |  ✓   | `src/Aonik.Voice/` + `apps/payabo_mobile/lib/features/voice/` |

It does **not** use invoicing/line items (those belong to MyBillAfrica), partners/corridors (RemitExchange), or autonumbering.

## Client surfaces

| Surface     | Location                | What it serves |
| ----------- | ----------------------- | -------------- |
| Web shell   | `apps/Payabo`           | Marketing, sign-up, dashboard, transactions, chat |
| Mobile      | `apps/payabo_mobile`    | The full Payabo experience plus voice and push notifications |

Both surfaces talk to the same Aonik API. The mobile app additionally uses a WebSocket endpoint (`/ai/voice`) for the voice pipeline.

## How AI proposals flow in Payabo

Payabo end-users see proposals inside the chat surface. When the Personal Finance agent detects a recurring merchant or suggests adding a subscription to the Financial Life Graph, the user is asked to **approve** or **reject** — Aonik never mutates state from an agent without explicit consent. The approval flow is platform-wide and described in [How AI works in Aonik](../../ai-platform/index.md).

## What's next

- [Configure Payabo for a tenant](configure.md) — the step-by-step
- [Capability matrix](../../getting-started/what-you-get.md) — see Payabo's platform footprint in context
- [Glossary](../../getting-started/glossary.md) — Financial Life Graph, Household, Proposal, AiRun
