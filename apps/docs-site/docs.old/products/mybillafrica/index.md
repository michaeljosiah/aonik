---
title: MyBillAfrica
description: The B2B billing & invoicing product — configuration, catalog, invoicing lifecycle, allocations, dunning, finance agent integration.
sidebar_label: Overview
sidebar_position: 1
---

# MyBillAfrica

:::warning Coming soon
The full MyBillAfrica configuration story ships in **Phase 4** of the docs rewrite. The platform-side modules are live today — only the dedicated docs section is pending.
:::

## What this section will cover

- Overview & architecture
- Configure MyBillAfrica for a tenant (numbering, currencies, dunning)
- Catalog — billers, services, fields, loading reference data
- Invoicing lifecycle (Draft → Issued → Paid → Cancelled)
- Allocations & dunning (payment matching, dunning plans)
- Finance agent integration (mutating tools, approvals)

## What works today

MyBillAfrica is wired into the platform — invoicing, line items, autonumbering, dunning, the finance agent tools, the catalog. There is no dedicated client app yet; operators interact through the Admin UI and the API.

## In the meantime

- [Capability matrix](../../getting-started/what-you-get.md) — which platform modules MyBillAfrica uses
- [Legacy Billing feature page](../../legacy/features/billing.md)
- [Legacy Pricing & FX feature page](../../legacy/features/pricing.md)
- [API Reference → Billing endpoints](/api/aonik-api)

## What's next

- [Capability matrix](../../getting-started/what-you-get.md)
- [Configure Payabo](../payabo/configure.md) — same configuration shape, different product
