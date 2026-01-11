# Clean Architecture

AONIK is structured to keep business logic independent of frameworks and infrastructure.

## Layers

- **Domain**
  - Entities are data-only (anemic model).
  - No persistence concerns.

- **Application**
  - Implements business logic in services.
  - Defines DTOs and abstractions (`IAonikDbContext`, providers, etc.).

- **Infrastructure**
  - Implements technical details (EF Core, auth integration, AI providers).
  - Provides `AonikDbContext` and DI registration.

- **API**
  - Accepts HTTP requests.
  - Maps API contracts to application DTOs.
  - Uses FastEndpoints response helpers (`Send.OkAsync`, `Send.CreatedAtAsync`, etc.).

## Dependency Rule

Dependencies point inward:

API → Application → Domain
Infrastructure → Application/Domain

The Application layer should not depend on Infrastructure.
