:::warning Legacy content

This page predates the docs rewrite. It may be inaccurate or out of date. See the current sidebar for the new home of this topic.

:::

<!-- LEGACY_BANNER -->

# Validation

## Where validation lives

- **API layer**: request validation (shape/range) close to the boundary.
- **Application layer**: business validation and invariants.

## Recommendations

- Keep validation errors consistent and user-friendly.
- Prefer explicit guard clauses over hidden side effects.
