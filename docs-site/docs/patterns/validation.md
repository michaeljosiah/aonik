# Validation

## Where validation lives

- **API layer**: request validation (shape/range) close to the boundary.
- **Application layer**: business validation and invariants.

## Recommendations

- Keep validation errors consistent and user-friendly.
- Prefer explicit guard clauses over hidden side effects.
