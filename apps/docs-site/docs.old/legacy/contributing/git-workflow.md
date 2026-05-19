:::warning Legacy content

This page predates the docs rewrite. It may be inaccurate or out of date. See the current sidebar for the new home of this topic.

:::

<!-- LEGACY_BANNER -->

# Git Workflow

## Branching

- Use short-lived feature branches.
- Keep commits small and focused.

## Before opening a PR

- `dotnet build Aonik.sln`
- `dotnet test Aonik.sln` (note: some integration tests may be failing per `AGENTS.md`)

## Commit messages

- Use clear, intent-focused messages (why, not just what).
