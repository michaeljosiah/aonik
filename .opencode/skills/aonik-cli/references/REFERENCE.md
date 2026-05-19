# AONIK CLI Reference

This file is a compact command matrix for the `aonik-cli` skill.

Use it when an agent needs command-line interaction with AONIK systems through `src/Aonik.Cli`.

## Run Pattern

Use the local CLI through:

```bash
dotnet run --project src/Aonik.Cli -- <command>
```

Build and test when changing CLI code:

```bash
dotnet build src/Aonik.Cli/Aonik.Cli.csproj
dotnet test tests/Aonik.Cli.Tests/Aonik.Cli.Tests.csproj
```

## Output Modes

- `text`: human-readable summaries
- `json`: structured JSON payloads
- `ndjson`: best for streaming AG-UI events and harness consumption

## Auth Commands

```bash
dotnet run --project src/Aonik.Cli -- auth login --base-url https://localhost:5001 --access-token <TOKEN>
dotnet run --project src/Aonik.Cli -- auth login --base-url https://localhost:5001 --username <EMAIL> --password <PASSWORD>
dotnet run --project src/Aonik.Cli -- auth status --output json
dotnet run --project src/Aonik.Cli -- auth whoami --output json
dotnet run --project src/Aonik.Cli -- auth logout
```

Useful options:

- `--tenant-id <GUID>`
- `--client-id <ID>`
- `--scope <SCOPE>`

## Agent Commands

```bash
dotnet run --project src/Aonik.Cli -- agent list --output json
dotnet run --project src/Aonik.Cli -- agent run --message "List overdue invoices" --output json
dotnet run --project src/Aonik.Cli -- agent stream --message "Reconcile yesterday's settlements" --output ndjson
dotnet run --project src/Aonik.Cli -- agent threads --output json
dotnet run --project src/Aonik.Cli -- agent thread <THREAD_ID> --output json
```

Streaming options:

- `--thread-id <ID>`
- `--run-id <ID>`
- `--agent-id <NAME>`

Common AG-UI event types seen from `agent stream`:

- `RUN_STARTED`
- `TEXT_MESSAGE_CONTENT`
- `TOOL_CALL_START`
- `TOOL_CALL_ARGS`
- `TOOL_CALL_END`
- `TOOL_CALL_RESULT`
- `REASONING_MESSAGE_CONTENT`
- `RUN_FINISHED`
- `CUSTOM` with `speech.render`

## Approval Commands

These currently resolve financial life graph proposals.

```bash
dotnet run --project src/Aonik.Cli -- approvals list --output json
dotnet run --project src/Aonik.Cli -- approvals approve <PROPOSAL_ID> --output json
dotnet run --project src/Aonik.Cli -- approvals reject <PROPOSAL_ID> --reason "Not enough evidence" --output json
```

## Ops Commands

### Workflow

```bash
dotnet run --project src/Aonik.Cli -- ops workflow --workflow-name reconciliation --input "Review today's unmatched items" --output json
```

### Jobs

```bash
dotnet run --project src/Aonik.Cli -- ops jobs list --output json
dotnet run --project src/Aonik.Cli -- ops jobs health --output json
dotnet run --project src/Aonik.Cli -- ops jobs trigger --job-name daily-reconciliation --output json
dotnet run --project src/Aonik.Cli -- ops jobs get daily-reconciliation --output json
dotnet run --project src/Aonik.Cli -- ops jobs pause daily-reconciliation --output json
dotnet run --project src/Aonik.Cli -- ops jobs resume daily-reconciliation --output json
dotnet run --project src/Aonik.Cli -- ops jobs runs daily-reconciliation --page 1 --page-size 20 --output json
```

### Ledger

```bash
dotnet run --project src/Aonik.Cli -- ops ledger list --output json
dotnet run --project src/Aonik.Cli -- ops ledger create --base-currency USD --output json
```

### Invoices

```bash
dotnet run --project src/Aonik.Cli -- ops invoices list --output json
dotnet run --project src/Aonik.Cli -- ops invoices list --status Draft --output json
dotnet run --project src/Aonik.Cli -- ops invoices get <INVOICE_ID> --output json
dotnet run --project src/Aonik.Cli -- ops invoices create --customer-id <CUSTOMER_ID> --invoice-number INV-1001 --currency USD --due-utc 2026-05-01T00:00:00Z --lines-file lines.json --output json
dotnet run --project src/Aonik.Cli -- ops invoices issue <INVOICE_ID> --confirm --output json
dotnet run --project src/Aonik.Cli -- ops invoices cancel <INVOICE_ID> --confirm --output json
dotnet run --project src/Aonik.Cli -- ops invoices mark-paid <INVOICE_ID> --confirm --output json
```

`issue`, `cancel`, and `mark-paid` refuse to run without `--confirm`.

### Orders

```bash
dotnet run --project src/Aonik.Cli -- ops orders list --output json
dotnet run --project src/Aonik.Cli -- ops orders list --status Draft --page 1 --page-size 20 --output json
dotnet run --project src/Aonik.Cli -- ops orders get <ORDER_ID> --output json
dotnet run --project src/Aonik.Cli -- ops orders create-bill-payment --payer-party-id <PARTY_ID> --origin-country GH --origin-currency GHS --items-file items.json --output json
dotnet run --project src/Aonik.Cli -- ops orders submit <ORDER_ID> --confirm --output json
dotnet run --project src/Aonik.Cli -- ops orders cancel <ORDER_ID> --reason "Reason" --confirm --output json
```

`submit` and `cancel` require `--confirm`.

### Payments

```bash
dotnet run --project src/Aonik.Cli -- ops payments create-intent --amount 100 --currency USD --reference PAY-1001 --order-id <ORDER_ID> --output json
dotnet run --project src/Aonik.Cli -- ops payments get <PAYMENT_INTENT_ID> --output json
dotnet run --project src/Aonik.Cli -- ops payments capture <PAYMENT_INTENT_ID> --output json
dotnet run --project src/Aonik.Cli -- ops payments cancel <PAYMENT_INTENT_ID> --output json
```

## Suggested Agent Usage

For harness-style flows:

1. log in once with `auth login`
2. prefer `agent stream --output ndjson` for conversational work
3. use `approvals` for explicit resolution
4. use `ops` for intentional operational actions

## Known Limitation

`dotnet build Aonik.sln` is currently blocked by unrelated existing `Aonik.Finance` endpoint compile errors around `WithTags`. Validate CLI changes using the CLI project and test project directly.
