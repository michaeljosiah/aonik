:::warning Legacy content

This page predates the docs rewrite. It may be inaccurate or out of date. See the current sidebar for the new home of this topic.

:::

<!-- LEGACY_BANNER -->

# DTO Mapping

AONIK keeps transport models and persistence models separate.

## Guidelines

- API contracts belong in `src/Aonik.Api/Contracts`.
- Application DTOs belong in `src/Aonik.Application`.
- Domain entities belong in `src/Aonik.Domain`.

Endpoints map API contracts → application DTOs and return DTOs.
