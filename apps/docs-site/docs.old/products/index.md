---
title: Products
description: Aonik's three first-party products and what they share with the underlying platform.
sidebar_label: Overview
sidebar_position: 1
---

# Products

:::info
Aonik powers three first-party products today. Each one is a tenant-configurable vertical built on the same shared platform — same identity, same ledger, same agents.
:::

## Why this matters

You don't enable a product by flipping a single switch. A "product" is a coherent combination of platform capabilities, tenant settings, feature flags, and a client surface (web, mobile, or both). This section tells you what each combination looks like so you can configure your tenant for the products you actually want to ship.

## The three products

### Payabo — B2C personal finance

The consumer-facing product. Customers connect their bank accounts, see their transactions, set goals, share with their household, and talk to an AI financial assistant by chat or voice.

- **Surfaces.** Web (`apps/Payabo`), Flutter mobile (`apps/payabo_mobile`)
- **Defining capabilities.** Personal finance, Plaid linking, household, financial life graph, voice agents
- **Configure.** [Configure Payabo for a tenant →](payabo/configure.md)
- **Deep dive.** [Payabo overview →](payabo/index.md)

### MyBillAfrica — B2B billing & invoicing

The B2B billing product. Businesses issue invoices, take payments, handle allocations and dunning, and delegate work to a finance agent.

- **Surfaces.** Admin UI panels today; dedicated web app on the roadmap
- **Defining capabilities.** Invoicing, line items, allocations, dunning, finance agent
- **Configure.** [MyBillAfrica overview →](mybillafrica/index.md)

### RemitExchange — cross-border remittance

The remittance product. Customers send money across corridors using partner connectors, with FX, fees, and compliance baked in.

- **Surfaces.** Admin UI panels today; dedicated web app on the roadmap
- **Defining capabilities.** Partners, corridors, pricing & FX, compliance screening, remittance orders
- **Configure.** [RemitExchange overview →](remitexchange/index.md)

## Shared platform underneath

Every product runs on the same platform. The shared layer gives you identity, multi-tenancy, the ledger, orders and payments, notifications, audit logging, and the AI runtime. The matrix in [What you get out of the box](../getting-started/what-you-get.md) shows exactly which platform modules each product uses.

## What's next

- [Configure Payabo](payabo/configure.md) — the most complete product config flow today
- [Capability matrix](../getting-started/what-you-get.md) — see which platform modules each product depends on
- [Platform Capabilities](../platform-capabilities/index.md) — full module reference
