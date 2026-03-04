# Technology Stack

## Runtime

- **.NET**: `net10.0`
- **C#**: latest language features (nullable enabled)

## API

- **FastEndpoints**: endpoint framework
- **Swagger/OpenAPI**: interactive docs (development)

## Persistence

- **Entity Framework Core 10**
- **SQL Server** in production
- **InMemory provider** for tests (and optional dev)

## Observability (Aspire)

- **.NET Aspire** service defaults for telemetry, health, service discovery
- Optional OTLP exporter via `OTEL_EXPORTER_OTLP_ENDPOINT`

## Testing

- **xUnit**
- **FluentAssertions**

See [AGENTS.md](https://github.com/michaeljosiah/aonik/blob/main/AGENTS.md) for commands and patterns.
