:::warning Legacy content

This page predates the docs rewrite. It may be inaccurate or out of date. See the current sidebar for the new home of this topic.

:::

<!-- LEGACY_BANNER -->

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
