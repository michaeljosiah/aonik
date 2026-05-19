---
title: Operations
description: Running Aonik in production — jobs, observability, audit, alerts, deploys, scaling, backup.
sidebar_label: Overview
sidebar_position: 1
---

# Operations

:::warning Coming soon
This section is being written in **Phase 5** of the docs rewrite. Until then, see the legacy operations content below.
:::

## What this section will cover

The runtime story end-to-end:

- Run the API and the Worker (Quartz)
- Scheduled jobs reference (CostGuard, SnapshotJob, StaleSession, etc.)
- Database migrations (Migrator, auto-migrate, the single-stream rule)
- Seed data (idempotent, what gets seeded)
- Observability (OpenTelemetry, dashboards, topology)
- Audit log (what's recorded, querying, retention)
- Alerts (configuration, escalation)
- Workflow management (Admin UI workflows editor)
- Compliance review (Admin UI compliance queue)
- Backup, restore, disaster recovery
- Deploy to Azure (Bicep, Container Apps)
- Deploy with GitHub Actions
- Performance & scaling (FusionCache, Qdrant, SQL tuning)

## In the meantime

- [Legacy Testing.md](../legacy/Testing.md) — test patterns and the InMemory DB strategy
- [Legacy Troubleshooting.md](../legacy/Troubleshooting.md) — common runtime issues and fixes
- [Legacy Docker setup](../legacy/deployment/docker.md)
- [Legacy local development](../legacy/deployment/local-development.md)
- [Legacy Azure deployment](../legacy/deployment/azure-deployment.md)
- [Legacy database migrations guide](../legacy/guides/database-migrations.md)

## What's next

- [Legacy Troubleshooting](../legacy/Troubleshooting.md)
- [Capability matrix](../getting-started/what-you-get.md)
