:::warning Legacy content

This page predates the docs rewrite. It may be inaccurate or out of date. See the current sidebar for the new home of this topic.

:::

<!-- LEGACY_BANNER -->

# Domain Entities

AONIK uses an **anemic domain model**.

## What this means

- Entities are **data containers** only.
- No business logic methods on entities.
- No constructors enforcing invariants.
- Business logic lives in **application services**.

## Entity conventions

- Public `{ get; set; }` properties
- Collections are `List<T>` with public get/set
- Nullable reference types are respected (`string?` when applicable)

See [AGENTS.md](https://github.com/michaeljosiah/aonik/blob/main/AGENTS.md) for the authoritative rules.
