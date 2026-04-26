# Aonik design skill

When designing ANYTHING for Aonik, load `tokens.css` once and build from there.

## Hard rules

1. **Load tokens first** — `<link rel="stylesheet" href="/tokens.css"/>` at the root. Never hardcode hex; read from CSS variables.
2. **Teal primary, coral for agent-apply only** — `.btn-secondary` (coral) is reserved for "Apply proposal" actions. Do not use it as a generic CTA.
3. **Bottom-bar focus on inputs** — `var(--shadow-focus)`. Do not replace with a full ring.
4. **Numbers in JetBrains Mono** — tabular-nums, right-aligned in tables. IDs, amounts, dates, confidence scores, tool names.
5. **Infra for brand moments only** — logo, major display. Everything else is DM Sans.

## When agents appear in the design

Every agent touch-point must use one of four primitives from `ui_kits/aonik-admin/`:

- **Proposal card** — coral left border, agent avatar + name, confidence in mono, diff block (+ add / - remove / ctx lines), reasoning paragraph, Apply/Review/Dismiss actions.
- **Streaming chat** — `.chat-primary` scope, shimmer on streaming text (`.shimmer` class), right-aligned user bubbles in teal-10, left-aligned agent bubbles in gray-100.
- **Tool-call trace** — numbered steps, active step tinted teal with shimmer on the in-progress description, done steps show a green check.
- **Agent selector** — Orchestrator always pinned at top, domain agents grouped below.

Agents propose. Systems apply. Every action must be attributable (agent name + confidence) and reversible (show the diff before applying).

## When adding a new screen

1. Wrap in `AppShell` (sidebar + topbar + optional right agent rail).
2. Start with a header block: eyebrow / H1 / subtitle + actions row.
3. KPI row if the screen is a dashboard — always exactly 4, same width.
4. Main content in `Card` primitives (12px radius, `--shadow-sm`).
5. If the screen has an agent dimension, expose it inline as proposal cards — do NOT dump it all in the right rail. The rail is for conversation; the page is for decisions.

## When the design doesn't fit the system

Add a token to `tokens.css` with a comment explaining why. Never inline a one-off color or shadow.

## Dark mode

Toggle with `[data-theme="dark"]` on `<html>`. Charcoal surfaces (`#1a1d21` → `#282c32`), never navy. All tokens auto-flip.

## Files

- `tokens.css` — all variables + primitive classes.
- `preview/*.html` — reviewable system cards.
- `ui_kits/aonik-admin/` — full React component kit (components + shell + three screens on a design canvas).
- `assets/` — logos, favicon, agent icons, Payabo imagery.
- `fonts/` — Infra otf.
