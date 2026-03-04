# Error Handling

## Principles

- Throw exceptions for invariant violations / invalid operations.
- Use `null` for not-found query results where appropriate.
- Prefer clear, actionable exception messages.

## API Behavior

Endpoints should translate errors into HTTP responses using FastEndpoints helpers:

- `Send.NotFoundAsync(ct)` for missing resources
- `Send.OkAsync(...)` / `Send.CreatedAtAsync(...)` for success

Avoid calling raw `SendAsync()` directly.
