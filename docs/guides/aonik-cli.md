# AONIK CLI Guide

This guide explains how to use the AONIK CLI for command-line interaction with AONIK systems.

The CLI is implemented in `src/Aonik.Cli` and is designed to support both:

- human operators working from a terminal
- agent-driven workflows that need a stable command surface instead of raw HTTP calls

## What The CLI Is For

The AONIK CLI provides a thin command-line client over existing AONIK APIs.

Use it when you need to:

- authenticate against an AONIK environment
- run orchestrated agent conversations
- stream AG-UI events for harness consumption
- inspect and resolve approval items
- execute explicit operational commands such as workflows, jobs, ledgers, invoices, and payment intents

The CLI does not embed financial business logic locally. It calls AONIK services and surfaces their results.

## Project Location

- CLI project: `src/Aonik.Cli`
- CLI tests: `tests/Aonik.Cli.Tests`

## Build The CLI

```bash
dotnet build src/Aonik.Cli/Aonik.Cli.csproj
```

Run the CLI tests:

```bash
dotnet test tests/Aonik.Cli.Tests/Aonik.Cli.Tests.csproj
```

## Run Pattern

Use the CLI through `dotnet run`:

```bash
dotnet run --project src/Aonik.Cli -- <command>
```

Examples:

```bash
dotnet run --project src/Aonik.Cli -- auth status
dotnet run --project src/Aonik.Cli -- agent list --output json
dotnet run --project src/Aonik.Cli -- ops jobs list --output json
```

## Authentication

### Login with an Existing Bearer Token

```bash
dotnet run --project src/Aonik.Cli -- auth login --base-url https://localhost:5001 --access-token <TOKEN>
```

### Login with Username and Password

```bash
dotnet run --project src/Aonik.Cli -- auth login --base-url https://localhost:5001 --username <EMAIL> --password <PASSWORD>
```

Optional flags:

- `--tenant-id <GUID>`
- `--client-id <ID>`
- `--scope <SCOPE>`
- `--output json`

### Inspect the Current Session

```bash
dotnet run --project src/Aonik.Cli -- auth status
dotnet run --project src/Aonik.Cli -- auth whoami --output json
```

### Clear the Local Session

```bash
dotnet run --project src/Aonik.Cli -- auth logout
```

The current implementation uses a file-backed session store. You can override the session file location with `AONIK_CLI_SESSION_PATH`.

## Output Modes

The CLI supports three output modes depending on the command:

- `text` for human-readable terminal output
- `json` for structured payloads
- `ndjson` for machine-readable streaming events

For agent harnesses, prefer:

- `json` for standard request/response commands
- `ndjson` for `agent stream`

## Agent Commands

### List Available Agents

```bash
dotnet run --project src/Aonik.Cli -- agent list --output json
```

### Run a Standard Agent Request

```bash
dotnet run --project src/Aonik.Cli -- agent run --message "List overdue invoices" --output json
```

Optional continuity flags:

- `--session-id <ID>`
- `--thread-id <ID>`

### Stream AG-UI Events

Use this when incremental results or harness-friendly event streams are needed.

```bash
dotnet run --project src/Aonik.Cli -- agent stream --message "Reconcile yesterday's settlements" --output ndjson
```

Optional flags:

- `--thread-id <ID>`
- `--run-id <ID>`
- `--agent-id <NAME>`

Typical streamed event types include:

- `RUN_STARTED`
- `TEXT_MESSAGE_CONTENT`
- `TOOL_CALL_START`
- `TOOL_CALL_ARGS`
- `TOOL_CALL_END`
- `TOOL_CALL_RESULT`
- `REASONING_MESSAGE_CONTENT`
- `RUN_FINISHED`
- `CUSTOM` events such as `speech.render`

### Inspect Threads

```bash
dotnet run --project src/Aonik.Cli -- agent threads --output json
dotnet run --project src/Aonik.Cli -- agent thread <THREAD_ID> --output json
```

## Approval Commands

The current approval commands work with pending financial life graph proposals.

### List Pending Proposals

```bash
dotnet run --project src/Aonik.Cli -- approvals list --output json
```

### Approve a Proposal

```bash
dotnet run --project src/Aonik.Cli -- approvals approve <PROPOSAL_ID> --output json
```

### Reject a Proposal

```bash
dotnet run --project src/Aonik.Cli -- approvals reject <PROPOSAL_ID> --reason "Not enough evidence" --output json
```

## Ops Commands

These commands expose explicit operational actions.

### Run an Advisory Workflow

```bash
dotnet run --project src/Aonik.Cli -- ops workflow --workflow-name reconciliation --input "Review today's unmatched items" --output json
```

### Scheduled Jobs

```bash
dotnet run --project src/Aonik.Cli -- ops jobs list --output json
dotnet run --project src/Aonik.Cli -- ops jobs health --output json
dotnet run --project src/Aonik.Cli -- ops jobs trigger --job-name daily-reconciliation --output json
```

### Ledgers

```bash
dotnet run --project src/Aonik.Cli -- ops ledger list --output json
dotnet run --project src/Aonik.Cli -- ops ledger create --base-currency USD --output json
```

### Invoices

```bash
dotnet run --project src/Aonik.Cli -- ops invoices list --output json
dotnet run --project src/Aonik.Cli -- ops invoices list --status Draft --output json
```

### Payment Intents

```bash
dotnet run --project src/Aonik.Cli -- ops payments create-intent --amount 100 --currency USD --reference PAY-1001 --order-id <ORDER_ID> --output json
dotnet run --project src/Aonik.Cli -- ops payments get <PAYMENT_INTENT_ID> --output json
dotnet run --project src/Aonik.Cli -- ops payments capture <PAYMENT_INTENT_ID> --output json
dotnet run --project src/Aonik.Cli -- ops payments cancel <PAYMENT_INTENT_ID> --output json
```

## Interactive Shell

For manual exploratory work:

```bash
dotnet run --project src/Aonik.Cli -- shell
```

The shell currently exposes:

- session status
- whoami
- agent list
- send agent message
- stream agent message
- list threads
- list approvals

## Recommended Usage For Agents

If an agent is meant to operate AONIK through the CLI, use this sequence:

1. build the CLI project
2. authenticate with `auth login`
3. use `agent stream --output ndjson` for conversational or orchestration flows
4. use `approvals` for explicit approval resolution
5. use `ops` for explicit operational actions
6. run CLI tests if the CLI code changed

## Troubleshooting

### No Active Session Found

Run:

```bash
dotnet run --project src/Aonik.Cli -- auth login ...
```

### API Call Failed

Check:

- the API base URL
- whether the API is running
- whether the token is valid
- whether a tenant override is needed

### Full Solution Build Fails But CLI Build Passes

At the moment, `dotnet build Aonik.sln` may fail because of unrelated existing compile errors in `Aonik.Finance` around `WithTags` usage. Validate CLI changes with:

```bash
dotnet build src/Aonik.Cli/Aonik.Cli.csproj
dotnet test tests/Aonik.Cli.Tests/Aonik.Cli.Tests.csproj
```

## Related Files

- `src/Aonik.Cli/CliApplication.cs`
- `src/Aonik.Cli/Commands/AuthCommandHandler.cs`
- `src/Aonik.Cli/Commands/AgentCommandHandler.cs`
- `src/Aonik.Cli/Commands/OpsCommandHandler.cs`
- `src/Aonik.Cli/Commands/ApprovalCommandHandler.cs`
- `.opencode/skills/aonik-cli/SKILL.md`
