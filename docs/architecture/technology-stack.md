# Technology Stack

## Runtime

- **.NET 10** (`net10.0`)
- **C#** latest language features (nullable reference types enabled globally)

## API

- **FastEndpoints 7.1.1** — high-performance endpoint framework
- **Swagger/OpenAPI** — interactive docs (development)

## Persistence

- **Entity Framework Core 10.0.1** — ORM with module-scoped DbContexts
- **SQL Server** — production database
- **InMemory provider** — for tests and optional local development

## AI & Agents

- **Microsoft Agent Framework (MAF)** — agent orchestration, `ChatClientAgent`, `AIFunctionFactory`, `AgentSession`
- **Microsoft.Extensions.AI** — `IChatClient` abstraction, `ApprovalRequiredAIFunction` for human-in-the-loop, `DelegatingChatClient` for audit middleware
- **Model Context Protocol (MCP)** — domain MCP servers per module, `McpToolProvider` for tool integration

## Orchestration

- **.NET Aspire** — service defaults, telemetry, health, service discovery
- **Quartz** — background job scheduling (Worker project)

## Admin UI

- **React 19** — UI framework
- **Vite** — build tool
- **Tailwind CSS** — utility-first styling
- **Dockview** — workspace panel management

## Testing

- **xUnit** — test framework
- **FluentAssertions** — assertion library
- **WebApplicationFactory** — API integration testing

## Key NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| FastEndpoints | 7.1.1 | HTTP endpoints |
| Microsoft.EntityFrameworkCore | 10.0.1 | ORM |
| Microsoft.Agents.AI | RC | MAF agent framework (ChatClientAgent, AgentSession) |
| Microsoft.Extensions.AI | latest | AI abstractions (IChatClient, ApprovalRequiredAIFunction) |
| ModelContextProtocol | latest | MCP server/client |
| Quartz | latest | Job scheduling |
| FluentAssertions | 8.8.0 | Test assertions |

See [AGENTS.md](../../AGENTS.md) for build commands and coding patterns.
