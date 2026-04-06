# AONIK CLI

The AONIK CLI is a command-line client for interacting with AONIK systems through `src/Aonik.Cli`.

It supports:

- authentication
- agent commands
- AG-UI streaming
- approvals
- explicit operational commands
- a small interactive shell

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
