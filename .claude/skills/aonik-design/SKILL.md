# Aonik design skill

Aonik's in-product UI is **shadcn/ui (new-york, Tailwind v4) on Radix**, with Aonik brand tokens. See [Spec 098](../../../docs/specifications/098.admin-ui-shadcn-radix-alignment.html).

- **Admin UI (real code):** the source of truth is `src/Aonik.AdminUi/src/index.css` (shadcn semantic tokens) and `src/Aonik.AdminUi/src/components/ui/*`. Add components with `npx shadcn@latest add <name>` from `src/Aonik.AdminUi`, then apply brand deltas only.
- **Design canvases and mockups:** load `tokens.css` from this skill. Where it disagrees with the rules below, the rules below win.

## Hard rules

1. **Semantic tokens only.** Use `bg-background`, `text-foreground`, `text-muted-foreground`, `bg-muted`, `bg-accent`, `border-border`, `border-input`, `ring-ring`, `bg-primary`/`text-primary-foreground`, `bg-destructive`, and the status trios `success|warning|info` (`-foreground` for text on the `-subtle` tint). Never hardcode hex or raw Tailwind palette colours (`bg-gray-100`, `text-red-600`). The legacy `var(--color-*)` names are aliases scheduled for deletion; don't write new ones.
2. **Teal primary, coral for agent-apply only.** Button `variant="agent"` (coral) is reserved for "Apply proposal". Button `secondary` is shadcn's neutral secondary, not coral.
3. **One focus style: the shadcn ring** (`focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50`) on every focusable control. The old bottom-bar input focus is retired.
4. **Numbers in JetBrains Mono.** `font-mono tabular-nums`, right-aligned in tables. IDs, amounts, dates in tables, confidence scores, tool names.
5. **Infra for brand moments only.** The wordmark and auth/landing screens. Page titles are DM Sans 600.
6. **Radius from the scale** (`--radius: 0.625rem`): controls `rounded-md`, cards/dialogs `rounded-xl`/`rounded-lg`, badges `rounded-md`, avatars and switches `rounded-full`. No `rounded-[2px]`, `rounded-none` or other arbitrary radii.
7. **Overlays are Radix.** Dialog, AlertDialog (never `window.confirm`), Sheet, Popover, DropdownMenu, Tooltip (inverted `bg-foreground`). Stack them with the layer tokens (`z-(--z-overlay)`, `z-(--z-modal)`, `z-(--z-popover)`, `z-(--z-tooltip)`), not arbitrary z-indexes.
8. **Contrast is checked.** Any token change must pass `npm run check:contrast` (text 4.5:1, non-text 3:1, both themes).

## When agents appear in the design

Every agent touch-point must use one of four primitives from `ui_kits/aonik-admin/`:

- **Proposal card** — coral left border, agent avatar + name, confidence in mono, diff block (+ add / - remove / ctx lines), reasoning paragraph, Apply/Review/Dismiss actions.
- **Streaming chat** — `.chat-primary` scope, shimmer on streaming text (`.shimmer` class), right-aligned user bubbles in teal-10, left-aligned agent bubbles in gray-100.
- **Tool-call trace** — numbered steps, active step tinted teal with shimmer on the in-progress description, done steps show a green check.
- **Agent selector** — Orchestrator always pinned at top, domain agents grouped below.

Agents propose. Systems apply. Every action must be attributable (agent name + confidence) and reversible (show the diff before applying).

## When adding a new screen

1. Wrap in `AppShell` (sidebar + topbar + optional right agent rail).
2. Start with a header block: breadcrumb (optional) / H1 / description + actions row. No eyebrow label.
3. KPI row if the screen is a dashboard — always exactly 4, same width.
4. White page; use `Card` (`rounded-xl border shadow-xs`) where content is a discrete object (a KPI, a summary, a form section), not around every block.
5. If the screen has an agent dimension, expose it inline as proposal cards — do NOT dump it all in the right rail. The rail is for conversation; the page is for decisions.

## When the design doesn't fit the system

Add a token to `tokens.css` with a comment explaining why. Never inline a one-off color or shadow.

## Dark mode

Toggle with `[data-theme="dark"]` on `<html>`; Tailwind's `dark:` variant is bound to it (not the OS preference). Charcoal surfaces (`#1a1d21` → `#262a2f`), never navy. All tokens auto-flip. In dark mode primary and the status solids are light enough to read as text, so filled controls use `text-primary-foreground` / `text-destructive-foreground`, never `text-white`.

## Files

- `tokens.css` — all variables + primitive classes.
- `preview/*.html` — reviewable system cards.
- `ui_kits/aonik-admin/` — full React component kit (components + shell + three screens on a design canvas).
- `assets/` — logos, favicon, agent icons, Payabo imagery.
- `fonts/` — Infra otf.
