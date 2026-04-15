# Feature Specification Template

> Keep contract sections stable: `Summary`, `Context`, `Scope`, `Constraints And Guardrails`, `Requirements`.
> Execution sections change during delivery: `Tasks`, `Decision Log`, `Done`.
> Give stable IDs to requirements, tasks, and decisions. Do not mark a requirement complete without evidence.

## Frontmatter

```yaml
spec_id: SPEC-<yyyy-mm-dd>-<slug>
title: <feature title>
status: draft # draft | approved | in_progress | blocked | complete
size: medium # small | medium | large
priority: high # high | medium | low
created: <yyyy-mm-dd>
last_updated: <yyyy-mm-dd>
target_release: <milestone or date>
repository: <repo name>
branch: <branch name or n/a>
target_paths:
  - <path>
related_docs:
  - <path or url>
dependencies:
  - <service, team, package, or doc>
```

### Size Guide

- **Small** — isolated change, 1-2 files, no cross-module impact. Fill: Summary, Context, Scope, Requirements, Tasks, Verification.
- **Medium** — multiple files or modules, new behavior. Fill all sections.
- **Large** — cross-cutting, new subsystem, or breaking change. Fill all sections. Consider splitting into multiple specs.

# Summary

<!-- 3-6 sentences. What is being built, what it achieves, and the most important boundary. Include success signals here. -->

<Write summary here.>

# Context

<!-- What exists today, what problem this solves, and why it matters now. One section replaces Background + Problem + Value. -->

<Write context here.>

# Scope

<!-- Prefix with + (in scope) or - (out of scope). -->

- `+` <In-scope item>
- `+` <In-scope item>
- `-` <Out-of-scope item>
- `-` <Out-of-scope item>

# Constraints And Guardrails

- Must not change: <public API, schema, behavior, security boundary, UX contract>
- Must follow: <existing pattern, library, coding convention, architecture>
- Must preserve: <backward compatibility, performance expectation, data integrity>
- Ask for approval before: <new dependency, migration, breaking change, broad refactor, infra cost>

# Agent Operating Instructions

- Read the files listed in `Current State` before editing.
- Prefer the smallest correct implementation that satisfies the requirements.
- Update `Tasks` and `Decision Log` as work proceeds.
- Stop and ask if the spec conflicts with the codebase or if a guardrail must be broken.
- Treat `Requirements` as the contract for completion.
- If the implementation changes materially from `Approach`, record the reason in `Decision Log`.

# Current State

<!-- List relevant files and a one-line description of existing behavior. -->

- `<path>`: <why it matters>
- `<path>`: <why it matters>

# Approach

<!-- Describe the intended solution at a high level. Keep it to a few paragraphs. Name the impacted areas (API, domain logic, UI, persistence, tests). Mention rejected alternatives inline only if the reasoning is non-obvious. -->

<Write approach here.>

# Requirements

<!-- Each requirement is its own acceptance criterion. Inline the verification method and linked tasks. -->

- [ ] `RQ1` <Requirement with observable outcome>
  Verify: `<test, build, command, or manual check>`
  Tasks: `T1`, `T2`

- [ ] `RQ2` <Requirement with observable outcome>
  Verify: `<test, build, command, or manual check>`
  Tasks: `T3`

- [ ] `RQ3` <Non-functional requirement: performance, security, reliability, observability, cost>
  Verify: `<test, build, command, or manual check>`
  Tasks: `T4`

# Tasks

<!-- Ordered checklist. Note dependencies with "after Tn" where needed. -->

Status: `[ ]` not started · `[-]` in progress · `[x]` done · `[!]` blocked

- [ ] `T1` Review current implementation and validate assumptions
- [ ] `T2` Implement core behavior
- [ ] `T3` Update or add tests
- [ ] `T4` Run verification steps and review results — after `T3`

# Verification

<!-- Flat checklist of how to confirm the spec is satisfied. -->

- Build: `<command>`
- Tests: `<command>`
- Manual: `<step>`
- Edge cases: `<case>`

# Decision Log

<!-- Record scope changes, assumption resolutions, and material implementation changes. -->

- `DEC1` <yyyy-mm-dd> — <Decision> — Reason: <why>

# Done

<!-- Fill when complete. What shipped, proof it works, and anything the next person should know. -->

- **Delivered**: <what was delivered>
- **Evidence**: <build, test, command, screenshot, or manual proof>
- **Follow-ups**: <optional remaining work>
- **Handoff**: <what the next human or agent should know>
