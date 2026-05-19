# AONIK CLI

The AONIK CLI is a command-line client for interacting with AONIK systems through `src/Aonik.Cli`.

It is designed to be invoked headlessly by agent harnesses and run directly by human operators. It does not embed a chat or interactive shell — harnesses provide their own chat surface, and humans drive the CLI through explicit commands.

It supports:

- authentication
- agent commands (one-shot, scriptable)
- AG-UI streaming
- approvals
- explicit operational commands

## Build

```bash
dotnet build src/Aonik.Cli/Aonik.Cli.csproj
```

## Run

```bash
dotnet run --project src/Aonik.Cli -- <command>
```

Examples:

```bash
dotnet run --project src/Aonik.Cli -- auth status
dotnet run --project src/Aonik.Cli -- agent list --output json
dotnet run --project src/Aonik.Cli -- agent stream --message "Reconcile yesterday's settlements" --output ndjson
```

## Tests

```bash
dotnet test tests/Aonik.Cli.Tests/Aonik.Cli.Tests.csproj
```

## Documentation

For the full guide, see `docs/guides/aonik-cli.md`.
