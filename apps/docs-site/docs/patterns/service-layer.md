# Service Layer Patterns

AONIK uses application services as the home for business logic.

## Conventions

- Prefer `IService` + `Service` implementation.
- Constructor injection for dependencies.
- Return DTOs (records) rather than EF entities.
- Async methods include `CancellationToken cancellationToken = default`.

## Mapping

- Keep mapping helpers private and static where possible.
- Avoid leaking EF Core tracking entities across layers.

See [AGENTS.md](https://github.com/michaeljosiah/aonik/blob/main/AGENTS.md) for examples.
