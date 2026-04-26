# AONIK — Design system

A ground-truth design system for **Aonik**, an AI-native finance + operations platform. Agents live inline on every surface — proposing, explaining, and applying changes under human oversight. The canonical phrase: **"Agents propose. Systems apply."**

The system is split into two skins:
- **In-product** (this system, `tokens.css`) — teal primary, coral secondary, charcoal dark mode. Used for Admin UI, dashboards, ledger, agent tools.
- **Marketing** — indigo / violet skin used only on the marketing site. Not covered here.

---

## Foundations

| File | What it is |
| --- | --- |
| `tokens.css` | Single source of truth. CSS variables for color, type, spacing, radius, shadow, motion. Also injects `@font-face` for Infra + DM Sans + JetBrains Mono, and primitive classes (`.btn`, `.input`, `.pill`, `.card`, `.hover-halo`, `.shimmer`, `.chat-*` theme scopes). |
| `fonts/` | Infra Regular / Medium / Bold `.otf` — Aonik's brand display face. |
| `assets/` | Logos, favicon, agent icon set, Payabo imagery. |

Load once at the root of any surface:
```html
<link rel="stylesheet" href="/tokens.css"/>
```

---

## Color — at a glance

| Role | Token | Hex | Use |
| --- | --- | --- | --- |
| Primary | `--brand-primary` | `#055a60` | CTAs, links, focus states, agent primary theme |
| Secondary | `--brand-secondary` | `#eb5c37` | Agent-apply actions, pending pills, the left rail on proposal cards |
| Favicon dot | `--brand-mark-dot` | `#e8a838` | Logo glyph only — never a utility color |
| Success | `--success` | `#4caf50` | Paid, settled, passing checks |
| Warning | `--warning` | `#ebc334` | Validation, policy band drift |
| Danger | `--danger` | `#cc2e2e` | Overdue, failed posts |
| Pending | `--pending` | `#eb5c37` | Awaiting human apply (same coral as secondary) |

Accent theme colors for scoping agent conversations: `--accent-violet`, `--accent-patrol`, `--accent-jade`, `--accent-team`, `--accent-ent`. Apply with `.chat-primary`, `.chat-team`, etc.

---

## Type

- **Infra** — brand display face. Reserve for logo, major display moments, and occasional H1s on marketing-adjacent pages.
- **DM Sans** — UI. Everything. Headings, body, tables.
- **JetBrains Mono** — numbers, codes, identifiers, IDs, diffs, tool-call names.

Numerical data is always tabular-nums mono. Never style amounts with sans.

---

## Components

Primitives ship as CSS classes in `tokens.css`; composed components ship as JSX in `ui_kits/aonik-admin/`.

- **Buttons** — `.btn-primary` (teal) for everything neutral-destination. `.btn-secondary` (coral) is **reserved** for "Apply proposal" — do not use it as a general CTA.
- **Inputs** — signature bottom-bar focus (`box-shadow: 0 4px 0 -2px var(--brand-primary-60)`). Do not replace with a ring.
- **Pills** — `pill`, `pill-tint`, `pill-success`, `pill-warning`, `pill-danger`, `pill-pending`.
- **Hover halo** — the micro-interaction behind every icon button. `.hover-halo` → 28×28 circle, tints on hover with a tinted halo ring.
- **Radius** — default card is 4–8px; 12px for large cards; never above 16px except pill.
- **Shadow** — `--shadow-sm` at rest, `--shadow-md` on hover/popover, `--shadow-lg` for modals. `--shadow-focus` for focus rings.

---

## Agent primitives

The four patterns every agent surface uses:

1. **Proposal card** — coral left-border, agent avatar + name + confidence, diff block, reasoning, and three actions: Apply (coral), Review (outline), Dismiss (ghost).
2. **Streaming chat** — bubbles with shimmer text while agents think. Scoped to agent theme via `.chat-primary` etc.
3. **Tool-call trace** — numbered steps with status dots; active step is tinted teal, done is green check, pending is gray.
4. **Agent selector** — single Orchestrator + N domain agents, with the Orchestrator always selected by default.

All four are demonstrated in `preview/agent-primitives.html` and used live in `ui_kits/aonik-admin/`.

---

## UI kit

`ui_kits/aonik-admin/index.html` — full design canvas showing three representative screens:

- **My Space dashboard** — greeting, 4 KPI tiles, cash timeline with today marker and projected band, agent proposals feed, recent activity.
- **Invoices** — data table with inline agent proposals expanded beneath flagged rows, filter bar, pagination.
- **Agent Command Center** — multi-agent roster, live tool-call trace, policy guardrails grid.

All three use the shared **AppShell** (sidebar + topbar + right agent rail). Shell, dashboard, ledger, and agent-center are split across four `.jsx` files; shared primitives live in `components.jsx`.

---

## Preview cards

`preview/` contains reviewable design-system cards (registered in the Design System tab): brand, three color palettes, two type cards, spacing+radius, and three component cards (controls, cards & surfaces, agent primitives).

---

## Rules of thumb

- Never invent a new color. Pick from tokens. If a token is missing, add it to `tokens.css` with a comment.
- Coral is scarce. If everything is coral, nothing is urgent.
- Numbers are always mono, tabular, right-aligned in tables.
- Every agent action must be attributable — name the agent, show the confidence, log the tool call.
- Never auto-apply above the policy ceiling. The system applies; humans approve.
- Dark mode is charcoal (`#1a1d21` → `#282c32`), not navy. Toggle via `[data-theme="dark"]` on `<html>`.
