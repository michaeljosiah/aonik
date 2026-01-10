# Architecture Overview

AONIK follows Clean Architecture with a modular, vertical-slice layout.

## High-Level Shape

- **API (`src/Aonik.Api`)**: HTTP boundary implemented with FastEndpoints.
- **Application (`src/Aonik.Application`)**: Business logic, services, DTOs, abstractions.
- **Domain (`src/Aonik.Domain`)**: Anemic entities (data containers only).
- **Infrastructure (`src/Aonik.Infrastructure`)**: EF Core, external integrations, providers.
- **Worker (`src/Aonik.Worker`)**: Background processing.

## Key Conventions

- Domain entities are **anemic**: public properties, no behavior methods.
- Business rules live in **application services**.
- API endpoints translate API contracts to application DTOs and call services.

## Database

- EF Core `AonikDbContext` is registered in Infrastructure.
- Aspire AppHost can provision SQL Server via Docker for local orchestration.

See `docs/Architecture.md` for legacy details and history.
