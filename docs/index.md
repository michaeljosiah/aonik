# Documentation Index

Welcome to the AONIK documentation. The main navigation hub is [README.md](README.md).

## Quick Links

- **[README.md](README.md)** — Full documentation navigation
- **[Getting Started](guides/getting-started.md)** — Setup and first run
- **[Architecture Overview](architecture/overview.md)** — Modular monolith design
- **[AGENTS.md](../AGENTS.md)** — Coding guidelines for AI agents and developers

## Common Tasks

```bash
# Build the solution
dotnet build Aonik.sln

# Run tests
dotnet test Aonik.sln

# Run API with Aspire
dotnet run --project src/Aonik.AppHost

# Run API directly
dotnet run --project src/Aonik.Api

# Create migration
dotnet ef migrations add <Name> --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
```

## Project Structure

```
aonik/
├── src/
│   ├── Aonik.SharedKernel/     # Cross-cutting primitives, interfaces, events
│   ├── Aonik.Platform/          # Platform module (Identity, Party, Compliance, etc.)
│   ├── Aonik.Finance/           # Finance module (Billing, Ledger, Payments, Orders, etc.)
│   ├── Aonik.Ai/               # AI module (providers, models, prompts, evals)
│   ├── Aonik.Agents/           # Agents module (agents, proposals, orchestration)
│   ├── Aonik.Application/      # Shared application abstractions
│   ├── Aonik.Infrastructure/   # EF Core migrations, external adapters
│   ├── Aonik.Api/              # FastEndpoints HTTP API (composition root)
│   ├── Aonik.Worker/           # Background jobs (Quartz)
│   ├── Aonik.AppHost/          # .NET Aspire orchestrator
│   ├── Aonik.ServiceDefaults/  # Aspire service defaults
│   ├── Aonik.Migrator/         # Database migration runner
│   ├── Aonik.Finance.Mcp/     # Finance MCP server
│   └── Aonik.Platform.Mcp/    # Platform MCP server
├── tests/
│   ├── Aonik.SharedKernel.Tests/
│   ├── Aonik.Application.Tests/
│   ├── Aonik.Infrastructure.Tests/
│   └── Aonik.Api.Tests/
├── docs/                        # Documentation (this directory)
├── AGENTS.md                    # Coding guidelines
├── CHANGELOG.md                 # Version history
├── README.md                    # Project overview
└── Aonik.sln                    # Solution file
```

## Need Help?

1. Check the [Troubleshooting Guide](Troubleshooting.md)
2. Review the [CHANGELOG](../CHANGELOG.md) for recent changes
3. Consult [AGENTS.md](../AGENTS.md) for coding patterns
