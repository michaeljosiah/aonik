---
name: aonik-cli
description: Use the AONIK CLI for agent-driven command-line interaction with AONIK systems. It supports authentication, agent commands, AG-UI streaming, approvals, and explicit operational actions. Use when an agent should operate AONIK through `src/Aonik.Cli` instead of calling HTTP endpoints directly.
compatibility: Requires .NET 10 SDK and an accessible AONIK API. Intended for agent-driven command-line workflows against AONIK systems.
metadata:
  owner: aonik
  version: "0.1"
---

## Purpose

This skill is for agents that need command-line interaction with AONIK systems through the local `Aonik.Cli` project.

It gives an agent a consistent CLI control surface for:

- authenticating against AONIK
- invoking orchestrated or direct agent workflows
- consuming AG-UI event streams
- resolving approval items
- executing explicit operational commands against platform and finance surfaces

Prefer this skill when the task is about using, validating, or automating AONIK through its CLI interface rather than building raw HTTP requests.

For a compact command matrix and examples, see `references/REFERENCE.md`.
For lightweight validation of this skill directory, run `scripts/validate-skill.sh`.

Use this skill when you need to:

- authenticate to AONIK through the CLI
- interact with AONIK agents from the command line
- stream AG-UI events in machine-readable form
- inspect or resolve approval items
- run explicit operations like jobs, ledgers, invoices, workflows, and payment intents

## Project Location

- CLI project: `src/Aonik.Cli`
- CLI tests: `tests/Aonik.Cli.Tests`

The CLI currently runs via `dotnet run --project src/Aonik.Cli -- ...`.

## Important Notes

- Prefer the CLI over direct API calls when the task is about command-line interaction with AONIK systems or validating CLI behavior.
- For harness or agent-to-agent usage, prefer `--output json` or `--output ndjson`.
- `agent stream` uses the AG-UI endpoint and is the best option for incremental machine-readable events.
- Current `approvals` commands target financial life graph proposal approval endpoints, not a generic platform-wide approval queue.
- `ops` commands are explicit operator actions. Keep financially material actions intentional and review the payload before execution.

## Build And Test

Build only the CLI project unless the task explicitly requires a full-solution build:

```bash
dotnet build src/Aonik.Cli/Aonik.Cli.csproj
```

Run CLI tests:

```bash
dotnet test tests/Aonik.Cli.Tests/Aonik.Cli.Tests.csproj
```

At the time this skill was written, `dotnet build Aonik.sln` is blocked by unrelated existing compile errors in `Aonik.Finance` around `WithTags` usage. Do not treat that as a CLI regression unless your changes touched those files.

## Authentication

### Login with an existing bearer token

```bash
dotnet run --project src/Aonik.Cli -- auth login --base-url https://localhost:5001 --access-token <TOKEN>
```

### Login with username and password

```bash
dotnet run --project src/Aonik.Cli -- auth login --base-url https://localhost:5001 --username <EMAIL> --password <PASSWORD>
```

Optional flags:

- `--tenant-id <GUID>`
- `--client-id <ID>`
- `--scope <SCOPE>`
- `--output json`

### Inspect or clear the local session

```bash
dotnet run --project src/Aonik.Cli -- auth status
dotnet run --project src/Aonik.Cli -- auth whoami
dotnet run --project src/Aonik.Cli -- auth logout
```

The CLI stores its local session in a file-backed store. You can override the location with `AONIK_CLI_SESSION_PATH`.

## Agent Commands

### List registered agents

```bash
dotnet run --project src/Aonik.Cli -- agent list --output json
```

### Send a standard orchestrator message

```bash
dotnet run --project src/Aonik.Cli -- agent run --message "List overdue invoices" --output json
```

Optional continuity flags:

- `--session-id <ID>`
- `--thread-id <ID>`

### Stream AG-UI events

Use this for harness workflows and incremental event handling.

```bash
dotnet run --project src/Aonik.Cli -- agent stream --message "Reconcile yesterday's settlements" --output ndjson
```

Optional flags:

- `--thread-id <ID>`
- `--run-id <ID>`
- `--agent-id <NAME>` to target a direct domain agent instead of the master orchestrator

Output guidance:

- `--output text` is human-friendly
- `--output json` writes one serialized event per line as JSON objects
- `--output ndjson` is best for agent harness consumption

### Inspect threads

```bash
dotnet run --project src/Aonik.Cli -- agent threads --output json
dotnet run --project src/Aonik.Cli -- agent thread <THREAD_ID> --output json
```

## Approval Commands

These currently work with pending financial life graph proposals.

### List pending approvals

```bash
dotnet run --project src/Aonik.Cli -- approvals list --output json
```

### Approve a proposal

```bash
dotnet run --project src/Aonik.Cli -- approvals approve <PROPOSAL_ID> --output json
```

### Reject a proposal

```bash
dotnet run --project src/Aonik.Cli -- approvals reject <PROPOSAL_ID> --reason "Not enough evidence" --output json
```

## Ops Commands

### Advisory workflows

```bash
dotnet run --project src/Aonik.Cli -- ops workflow --workflow-name reconciliation --input "Review today's unmatched items" --output json
```

### Scheduled jobs

```bash
dotnet run --project src/Aonik.Cli -- ops jobs list --output json
dotnet run --project src/Aonik.Cli -- ops jobs health --output json
dotnet run --project src/Aonik.Cli -- ops jobs trigger --job-name daily-reconciliation --output json
dotnet run --project src/Aonik.Cli -- ops jobs get daily-reconciliation --output json
dotnet run --project src/Aonik.Cli -- ops jobs pause daily-reconciliation --output json
dotnet run --project src/Aonik.Cli -- ops jobs resume daily-reconciliation --output json
dotnet run --project src/Aonik.Cli -- ops jobs runs daily-reconciliation --output json
```

### Ledger operations

```bash
dotnet run --project src/Aonik.Cli -- ops ledger list --output json
dotnet run --project src/Aonik.Cli -- ops ledger create --base-currency USD --output json
```

### Invoice lifecycle

```bash
dotnet run --project src/Aonik.Cli -- ops invoices list --output json
dotnet run --project src/Aonik.Cli -- ops invoices list --status Draft --output json
dotnet run --project src/Aonik.Cli -- ops invoices get <INVOICE_ID> --output json
dotnet run --project src/Aonik.Cli -- ops invoices create --customer-id <CUSTOMER_ID> --invoice-number INV-1001 --currency USD --due-utc 2026-05-01T00:00:00Z --lines-file lines.json --output json
dotnet run --project src/Aonik.Cli -- ops invoices issue <INVOICE_ID> --confirm --output json
dotnet run --project src/Aonik.Cli -- ops invoices cancel <INVOICE_ID> --confirm --output json
dotnet run --project src/Aonik.Cli -- ops invoices mark-paid <INVOICE_ID> --confirm --output json
```

`issue`, `cancel`, and `mark-paid` are financially material — they refuse to run without `--confirm`.

### Orders

```bash
dotnet run --project src/Aonik.Cli -- ops orders list --output json
dotnet run --project src/Aonik.Cli -- ops orders get <ORDER_ID> --output json
dotnet run --project src/Aonik.Cli -- ops orders create-bill-payment --payer-party-id <PARTY_ID> --origin-country GH --origin-currency GHS --items-file items.json --output json
dotnet run --project src/Aonik.Cli -- ops orders submit <ORDER_ID> --confirm --output json
dotnet run --project src/Aonik.Cli -- ops orders cancel <ORDER_ID> --reason "..." --confirm --output json
```

`submit` and `cancel` require `--confirm`.

### Payment intent operations

```bash
dotnet run --project src/Aonik.Cli -- ops payments create-intent --amount 100 --currency USD --reference PAY-1001 --order-id <ORDER_ID> --output json
dotnet run --project src/Aonik.Cli -- ops payments get <PAYMENT_INTENT_ID> --output json
dotnet run --project src/Aonik.Cli -- ops payments capture <PAYMENT_INTENT_ID> --output json
dotnet run --project src/Aonik.Cli -- ops payments cancel <PAYMENT_INTENT_ID> --output json
```

## Recommended Usage Pattern For Agents

1. Ensure the CLI builds: `dotnet build src/Aonik.Cli/Aonik.Cli.csproj`
2. Authenticate if needed with `auth login`
3. Use `agent stream --output ndjson` for streaming orchestration work
4. Use `approvals ...` for explicit approval resolution
5. Use `ops ...` for explicit operational actions
6. Run `dotnet test tests/Aonik.Cli.Tests/Aonik.Cli.Tests.csproj` if you changed CLI code

## Troubleshooting

### No active session found

Run:

```bash
dotnet run --project src/Aonik.Cli -- auth login ...
```

### API call failed

Check:

- the API base URL is correct
- the API is running
- the bearer token is still valid
- the tenant context is correct if `--tenant-id` is required

### Streaming output is too verbose for a harness

Use:

```bash
dotnet run --project src/Aonik.Cli -- agent stream --message "..." --output ndjson
```

### You need command details while working on the skill

Read the CLI command tree in:

- `src/Aonik.Cli/CliApplication.cs`
- `src/Aonik.Cli/Commands/AgentCommandHandler.cs`
- `src/Aonik.Cli/Commands/OpsCommandHandler.cs`
- `src/Aonik.Cli/Commands/ApprovalCommandHandler.cs`

For a quicker reference sheet, use `references/REFERENCE.md`.
