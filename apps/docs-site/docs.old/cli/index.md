---
title: CLI
description: The Aonik CLI — terminal-driven agent chat, approval workflows, and operational commands.
sidebar_label: Overview
sidebar_position: 1
---

# CLI

:::warning Coming soon
This section is being written in **Phase 4** of the docs rewrite. Until then, browse the CLI source at `src/Aonik.Cli/`.
:::

## What this section will cover

A page per command surface:

- **Install** — building or installing `Aonik.Cli`
- **Authenticate** — `auth login` and `auth logout`
- **Chat** — `agent chat` for multi-turn conversations, `agent run <prompt>` for one-shots
- **Approvals** — `approvals list`, `approvals approve <id>`, `approvals reject <id>`
- **Operations** — `ops health`, `ops list-jobs`
- **Shell** — `shell` interactive mode
- **Output modes** — JSON, NDJSON, Table, Text

## In the meantime

- `src/Aonik.Cli/Commands/` — the canonical command source
- [Glossary](../getting-started/glossary.md) — Proposal, Approval, AiRun

## What's next

- [Quickstart](../getting-started/quickstart.md) — get the platform running before connecting the CLI
- [For Contributors](../for-contributors/index.md) — for CLI source-code conventions
