# components/ui

The Admin UI's only primitive layer: shadcn/ui (new-york, Tailwind v4) on
Radix, with Aonik tokens. Spec: `docs/specifications/098.admin-ui-shadcn-radix-alignment.html`.

Add a component with `npx shadcn@latest add <name>` (from `src/Aonik.AdminUi`),
then apply brand deltas only. Preview everything at `/dev/ui` and the shell at
`/dev/shell` (dev builds). Check your work with:

```
npm run check:design -- --only pages/orders --files
npm run check:contrast
```

Both run in CI (`Admin UI checks` job) with `check:design -- --strict`, so a new
hard-coded colour, raw palette class, arbitrary radius or z-index,
`window.confirm`, raw form/table element, legacy class or `var(--color-*)`
token fails the PR. A genuine exception carries `// guardrail-ignore: <reason>`
on the line (or the line above).

## Which primitive for which job

| Job | Use | Not |
| --- | --- | --- |
| Confirm a consequential action | `AlertDialog` (title names the action, confirm button repeats the verb, `variant="destructive"` for destructive) | `window.confirm`, a Dialog |
| 2 to 3 field action, or read-only detail | `Dialog` | a hand-built `fixed inset-0` overlay |
| Create/edit form up to ~10 fields, side detail panel | `Sheet` (`size` sm/md/lg, `SheetHeader`/`SheetBody`/`SheetFooter`) | a hand-built drawer |
| Short fixed option list | `Select` | raw `<select>` |
| Native behaviour needed (very long lists, mobile) | `NativeSelect` | raw `<select>` |
| Long or searchable list (countries, currencies, customers, agents) | `Combobox` | a hand-built dropdown |
| Date | `DatePicker` (`yyyy-MM-dd` in and out) | `<input type="date">` |
| Date and time | `Input type="datetime-local"` for now | — |
| On/off setting | `Switch` | a checkbox styled as a toggle |
| Multi-select option | `Checkbox` | `<input type="checkbox">` |
| One of several | `RadioGroup` (choice cards via `FieldLabel` wrapping `Field`) | `<input type="radio">` |
| Label, help text, error | `Field`, `FieldLabel`, `FieldDescription`, `FieldError` | hand-spaced `<label>` + `<p>` |
| Tabular data | `DataTable` (lists) or `Table` parts (static) | raw `<table>` |
| Status | `Badge` (`success`/`warning`/`info`/`destructive`/`secondary`/`outline`) or `Pill` | coloured `<span>`s |
| Inline message | `Alert` (`default`/`destructive`/`success`/`warning`/`info`) | coloured bordered `<div>`s |
| Nothing to show | `Empty` (title says what's missing, description says what to do) | "No data" text |
| Loading layout | `Skeleton` | centred spinners for whole sections |
| Loading inline / in a button | `Spinner` (keep the button label) | swapping the label for "Loading…" |
| Icon-only action | `Button size="icon-sm" variant="ghost"` + `aria-label` + `Tooltip` | `hover-halo`, raw `<button>` |
| Grouped surface | `Card` parts (`CardHeader`/`CardTitle`/`CardDescription`/`CardAction`/`CardContent`/`CardFooter`) | ad-hoc bordered divs |
| Menu of actions | `DropdownMenu` | a hand-built popover list |

## Colour

Use semantic utilities only; they flip with the theme.

| Meaning | Classes |
| --- | --- |
| Page / card / popover | `bg-background`, `bg-card`, `bg-popover` |
| Subtle fill, hover | `bg-muted`, `bg-accent` (hover), `bg-secondary` |
| Text | `text-foreground`, `text-muted-foreground` |
| Lines | `border` (defaults to `--border`), `border-input` for fields |
| Brand / action | `bg-primary text-primary-foreground`, `text-primary`, `bg-primary/10` |
| Agent apply (coral), only for applying proposals | `bg-agent text-agent-foreground`, `border-l-agent` |
| Success / warning / info as text on page | `text-success`, `text-warning`, `text-info` |
| Status chip or tinted box | `bg-success-subtle text-success-foreground` (same for warning, info); `bg-destructive/10 text-destructive` |
| Error text | `text-destructive` |
| Categorical series (charts, avatars, tags, workflow step kinds) | `var(--chart-1)` … `var(--chart-10)` (each clears 3:1 on the card) |
| Brand accents (logo only / agent tiers) | `var(--mark-dot)` (the gold dot on the mark, never a CTA or status), `var(--agent-team)`, `var(--agent-enterprise)` |

Mapping raw Tailwind palette classes:

- `gray|slate|zinc|neutral|stone`: text 400–600 → `text-muted-foreground`, 700–900 → `text-foreground`; bg 50–100 → `bg-muted`, 200 → `bg-accent`; borders → `border` / `border-input`.
- `green|emerald|lime`: `text-success` (on page), `bg-success-subtle text-success-foreground` (chips/boxes), `bg-success` (solid dots/bars).
- `red|rose`: `text-destructive`, `bg-destructive/10 text-destructive`, `bg-destructive`.
- `amber|yellow|orange`: warning equivalents.
- `blue|sky|indigo|cyan`: info equivalents (or `primary` when it is really the brand/action colour).
- `purple|violet|fuchsia|pink|teal` used as categories: `--chart-N` (1–10).
- Drop the matching `dark:` variant: tokens already flip.

Hex colours become the token they stand for (`#055a60` → `primary`, `#eb5c37` → `agent`, greys → foreground/muted/border, status hues → status tokens, series → `--chart-N`). A colour that is genuinely data (a partner's brand colour from the API, a user-picked colour) stays, with `// guardrail-ignore: <reason>`.

## Shape and type

- Radius from the scale: controls `rounded-md`, cards/panels `rounded-xl` (Card) or `rounded-lg`, chips `rounded-md`, avatars/dots/switch `rounded-full`. No `rounded-[Npx]`, no `rounded-none` (unless joining edges, with `guardrail-ignore`).
- z-index: `z-(--z-overlay)`, `z-(--z-modal)`, `z-(--z-popover)`, `z-(--z-tooltip)`; most overlays need none once they are Radix.
- Money, IDs, counts in tables, confidence scores: `font-mono tabular-nums`, right-aligned in tables (`numeric` on DataTable columns / Table cells).
- Labels are sentence case. No `uppercase tracking-wider` micro-labels.
- Infra (`font-brand`) only for the wordmark and auth screens.
