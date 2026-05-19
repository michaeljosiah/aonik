---
title: Integrations
description: The third-party services Aonik talks to, and how to wire each one.
sidebar_label: Overview
sidebar_position: 1
---

# Integrations

:::warning Coming soon
Per-integration pages land in **Phase 2** of the docs rewrite. This index lists every supported integration today.
:::

## What this section will cover

Each supported integration gets its own page, following the same template — prerequisites, config, verify, troubleshoot:

| Integration                              | Used by                                | Config root |
| ---------------------------------------- | -------------------------------------- | ----------- |
| **Plaid** (bank account linking)         | Payabo                                 | `Finance:PersonalFinance:Plaid:*` |
| **Stripe** (card / bank payments)        | All products                           | `Finance:Payments:Stripe:*` |
| **Twilio** (SMS, WhatsApp)               | Notifications                          | `Notifications:Sms:Twilio:*` |
| **Firebase** (push, analytics)           | Payabo mobile                          | `apps/payabo_mobile/lib/firebase_options.dart` |
| **ElevenLabs** (text-to-speech)          | Payabo voice agents                    | `Voice:Tts:ElevenLabs:*` |
| **OpenAI / Anthropic / Azure OpenAI**    | AI Platform                            | `Ai:Providers:*` |
| **Qdrant** (vector store)                | AI Platform (RAG, user memory)         | `Qdrant:Url`, `Qdrant:ApiKey` |
| **Webhooks** (inbound + outbound)        | All products                           | per-tenant configuration |

For every integration: minimum credentials, where the keys go, sandbox vs production switch, expected error modes, and a curl/log verification.

## In the meantime

- [Capability matrix](../getting-started/what-you-get.md) — which integrations each product needs
- [Configure Payabo](../products/payabo/configure.md) — covers Plaid and ElevenLabs at the tenant level
- [Legacy ai-integration feature page](../legacy/features/ai-integration.md) — for AI provider context

## What's next

- [Configure Payabo](../products/payabo/configure.md)
- [Capability matrix](../getting-started/what-you-get.md)
