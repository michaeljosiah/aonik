---
title: What you get out of the box
description: The capability matrix across Payabo, MyBillAfrica, and RemitExchange, plus what works locally vs what needs external integration.
sidebar_label: What you get
sidebar_position: 4
---

# What you get out of the box

:::info
A capability-by-capability view of which Aonik modules each product uses, and a list of what works locally vs what requires external integration.
:::

## Why this matters

Before you decide which product to configure or which integration to wire first, it helps to see what Aonik already does for you and which third parties you actually need accounts with. This page is a quick reference for that conversation.

## Modules × Products matrix

| Capability                              | Payabo | MyBillAfrica | RemitExchange |
| --------------------------------------- | :----: | :----------: | :-----------: |
| Identity & Tenancy                      |   ✓    |      ✓       |       ✓       |
| Users, Roles & Permissions              |   ✓    |      ✓       |       ✓       |
| Party & Profile (people / businesses)   |   ✓    |      ✓       |       ✓       |
| Compliance & KYC/KYB                    |   ✓    |      ✓       |       ✓       |
| Notifications                           |   ✓    |      ✓       |       ✓       |
| Settings (tenant + user)                |   ✓    |      ✓       |       ✓       |
| Reference data (countries, currencies)  |   ✓    |      ✓       |       ✓       |
| Registration                            |   ✓    |      ✓       |       ✓       |
| Audit log                               |   ✓    |      ✓       |       ✓       |
| Orders                                  |   ✓    |      ✓       |       ✓       |
| Payments & payment intents              |   ✓    |      ✓       |       ✓       |
| Ledger (double-entry)                   |   ✓    |      ✓       |       ✓       |
| Invoicing & line items                  |   ·    |      ✓       |       ·       |
| Catalog (billers / services)            |   ✓    |      ✓       |       ·       |
| Personal finance (transactions, goals)  |   ✓    |      ·       |       ·       |
| External account linking (Plaid)        |   ✓    |      ·       |       ·       |
| Financial Life Graph                    |   ✓    |      ·       |       ·       |
| Household sharing                       |   ✓    |      ·       |       ·       |
| Pricing & FX                            |   ✓    |      ✓       |       ✓       |
| Partners (corridors, payouts)           |   ·    |      ·       |       ✓       |
| AI agents (chat)                        |   ✓    |      ✓       |       ✓       |
| Voice agents                            |   ✓    |      ·       |       ·       |
| Autonumbering                           |   ·    |      ✓       |       ✓       |
| Scheduled jobs (Quartz)                 |   ✓    |      ✓       |       ✓       |
| Observability                           |   ✓    |      ✓       |       ✓       |

`✓` actively used  ·  `·` not used by this product

MyBillAfrica and RemitExchange currently exist as platform-side modules and configuration paths; their dedicated client applications are tracked on the roadmap.

## Works locally with no external config

Most of the platform runs the moment you finish the [Quickstart](quickstart.md):

- **Ledger** — double-entry, immutable, all SQL
- **Orders, payment intents, billing** — fully local
- **Tenants, users, roles, permissions** — local; identity provider only required for actual logins
- **Personal finance entities** — accounts, transactions, categorization rules, financial life graph, household — all local
- **Notifications** — in-process queue + database persistence
- **Reference data** — countries, currencies, biller catalog — seeded on first migration
- **Compliance** — case and document entities work locally
- **Audit logging** — captured automatically by the base DbContext
- **Autonumbering** — sequence reservations stored in SQL
- **Scheduled jobs** — Quartz runs in-process inside the Worker
- **AI tracing** — every `AiRun` is written to SQL; no external observability stack required
- **Qdrant** — runs as a Docker container managed by Aspire

## Requires external integration

These need accounts and credentials before they work end-to-end. Add them in the order you actually need them — none are required for the [Quickstart](quickstart.md).

| Capability               | Provider                                  | Config root                              |
| ------------------------ | ----------------------------------------- | ---------------------------------------- |
| User authentication      | Auth0 **or** Microsoft Entra ID           | `Auth:Provider`, `Auth:Auth0:*`, `Auth:AzureAd:*` |
| LLM inference            | OpenAI, Anthropic, or Azure OpenAI        | `Ai:Providers:*`                         |
| Bank account linking     | Plaid                                     | `Finance:PersonalFinance:Plaid:*`        |
| Card / bank payments     | Stripe (or another `IPaymentGateway`)     | `Finance:Payments:Stripe:*`              |
| SMS / WhatsApp           | Twilio                                    | `Notifications:Sms:Twilio:*`             |
| Push notifications       | Firebase Cloud Messaging                  | `Notifications:Push:Firebase:*`          |
| Mobile app analytics     | Firebase (Analytics, Crashlytics)         | `apps/payabo_mobile` — `firebase_options.dart` |
| Text-to-speech           | ElevenLabs (or another TTS vendor)        | `Voice:Tts:ElevenLabs:*`                 |
| Speech-to-text           | Azure Speech, Google, OpenAI, etc.        | `Voice:Stt:*`                            |
| Vector store             | Qdrant                                    | `Qdrant:Url`, `Qdrant:ApiKey`            |
| Outbound webhooks        | Your destination URLs                     | per-tenant configuration                 |

Each provider gets a dedicated page in [Integrations](../integrations/index.md) (Phase 2 of the docs rewrite).

## What's next

- [Configure Payabo](../products/payabo/configure.md) — most operators start here
- [Identity & Access](../identity-access/index.md) — wire Auth0 or Entra ID
- [Integrations](../integrations/index.md) — Plaid, Stripe, Twilio, Firebase, providers, Qdrant
