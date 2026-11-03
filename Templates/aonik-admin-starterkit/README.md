# Aonik admin starter kit

The design canvas is `index.html`. The focused editable Remittance canvas is `remittance.html`; chat designs are in `chat.html`. Business-specific marketing designs are in `showcase.html`. The screenshot gallery is `marketing/index.html`.

The main Admin UI now implements collaborative chat rooms using these designs and shared portraits. Open **AI Chat > People & agents > Shared rooms**. Production workspace previews are in the gallery; see `docs/features/collaborative-chat.md` for persistence, access rules, agent/approval behavior and verification. The editable artboards remain available as design references.

Serve this directory over HTTP (the showcase loads a local JSON snapshot):

```powershell

cd Templates/aonik-admin-starterkit

python -m http.server 4173 --bind 127.0.0.1

```

Open [the business showcase](http://127.0.0.1:4173/showcase.html) or [the screenshot gallery](http://127.0.0.1:4173/marketing/index.html). The original canvas continues to use its existing React/Babel CDN dependencies; the new showcase uses native browser JavaScript. Google Fonts supplies DM Sans and JetBrains Mono, with system fallbacks; Infra is bundled locally.

## Editable Remittance artboards

Open [the Remittance design canvas](http://127.0.0.1:4173/remittance.html). These twelve artboards are also registered in the main canvas under **Remittance · Operations & receipts**:

- New transfer and transfer review/confirmation.
- Beneficiaries, Add payout destination, and beneficiary details with Send transfer.
- Transfer details and downloadable receipt action.
- Send receipt, email history, delivered/bounced tracking, customer email review, Resend receipt, and uncertain-send recovery.

The editable source is `screens/remittance-operations.jsx`, with scoped semantic styles in `design-system/remittance-operations.css`. The mock data and scene inventory are defined at the top of that JSX file. These are native React controls and SVGs, not screenshots. Editing amounts invalidates/recalculates the mock quote; rail choices and verification change the destination form; receipt dialogs require a saved address and confirmation and append local history. They make no application API calls and cannot execute a transfer or send email. The canvas retains its existing focus, reorder and label-editing features; persistence of canvas annotations uses the existing host bridge.

These design references accompany the already implemented application workflows. They share the canonical icon paths, use the Remittance navigation profile's menu labels and semantic theme tokens, and retain explicit pending/unknown send states. Production APIs and constraints are documented in `docs/features/remittance-transfer-editor.md` and `docs/features/remittance-beneficiaries.md`.

Run `node Templates/aonik-admin-starterkit/scripts/verify-remittance-artboards.mjs` from the repository root to check all twelve native artboards and their interactions. It serves the kit temporarily, captures twenty-four light/dark previews as `marketing/screenshots/remittance-artboard-*.png`, and writes `remittance-artboards-report.json`. The focused canvas and the main canvas share the same source and scene registration function.

## CGI agent portraits

Open [the agent portrait gallery](http://127.0.0.1:4173/agent-portraits.html) to see and download all sixteen original headshots, including 20/32/44px avatar previews. The set covers ten runtime identities (nine domain agents and the orchestrator) plus six starter-kit personas; the gallery labels those groups separately.

AONIK has a dedicated main-assistant portrait: a fictional woman in her thirties with mixed Black and Chinese heritage, generated with the built-in image_gen tool. `assets/agent-portraits/aonik-prompt.json` preserves the exact prompt. The existing My Space and Notification Center artboards now show shared portraits, and the marketing gallery includes light/dark previews. Application chat preserves server-recorded authors; agent notifications use explicit `agentName` metadata. See `docs/features/agent-portraits.md` for the producer contract and browser checks.

The originals, catalog and exact image_gen prompts are in `assets/agent-portraits/`. Run `node Templates/aonik-admin-starterkit/scripts/build-agent-portraits.mjs` to copy the assets to the Admin UI and regenerate the browser/React name mappings; add `--check` to detect drift. Unknown agents keep their geometric fallback. The roster, agent detail pages, chat artboards and application agent selector share the same identities. See `docs/features/agent-portraits.md` for inventory and verification instructions.

## Editable chat designs

Open [the Chat design canvas](http://127.0.0.1:4173/chat.html) for six editable artboards: contextual slide-out response, scoped permission cards, a collaborative room with two humans and three agents, human review of a financial proposal, a mixed human/agent notification inbox, and a mentions-only inbox. They also appear in the main canvas under **Chat · People & agents**. The designs use shared SVGs, semantic light/dark tokens, named authors, participant permissions, explicit audience selection and separate approval/execution states.

The source is `screens/ai-chat.jsx` with scoped styles in `design-system/chat.css`. All messages, invitations and decisions are fictional local interactions. The application implements collaborative rooms and in-app message/mention notifications; the editable designs remain available for review and iteration. See `docs/features/chat-designs.md` for behavior and production boundaries.

Run `node Templates/aonik-admin-starterkit/scripts/verify-chat-artboards.mjs` to check the interactions and generate twelve clean light/dark marketing captures, twelve canvas previews and `chat-design-report.json`. Clean previews are available at `chat.html?capture=1&scene=collaboration&theme=light` (also `slideout`, `permissions`, `proposal-review`, `notifications`, `mentions`, and `theme=dark`).

## Production design alignment

`src/Aonik.AdminUi/src/index.css` owns the design tokens. This kit mirrors its shadcn semantic contract: white light surfaces, charcoal dark surfaces, teal primary controls (`#055a60` / `#3a9ea4`), neutral secondary controls, input borders and focus rings, status foreground/tint pairs, and a 10px base radius. Coral is reserved for explicit agent-proposal controls. DM Sans is the interface face, JetBrains Mono is for IDs and figures, and Infra is for branding.

The original canvas sidebar now uses the production dimensions (256px / 48px), neutral active states and inline collapsible groups. Its navigation remains a broad **design inventory**, including experimental screens; use the business showcase for profile-specific marketing menus.

`tokens.css` maps legacy variables to the semantic tokens so existing artboards inherit the refreshed design. `design-system/tokens.css` imports this single entry point rather than carrying a second palette. The showcase shell follows the same anatomy and tokens using standalone CSS; it does not bundle the application's React/shadcn component implementation.

Refresh or verify the copied tokens and profile metadata from the repository root:

```powershell

node Templates/aonik-admin-starterkit/scripts/sync-design.mjs

node Templates/aonik-admin-starterkit/scripts/sync-design.mjs --check

```

The script reads the current profiles, business-type mapping, provisioning packs, registered screen definitions, route requirements and module requirements. Missing screens or changed registry syntax fail the sync. Generated files are `design-system/semantic-tokens.css` and `showcase/profile-snapshot.json`.

## Business types investigated

| Showcase name | Internal key | Repository state | Demonstrated configuration |

| --- | --- | --- | --- |

| General business | `base` | Installed pack; `general` admin profile | Finance, AI, Agents, Documents explicitly enabled for a business operations example. The base pack itself declares no optional modules. |

| Personal Finance | `simi` | Installed pack; shares `general` profile | Personal Finance, Groups, Documents, AI, Agents, Voice, as declared by the pack. Finance and Commerce are absent. |

| Food Commerce | `food-commerce` | Installed pack and dedicated profile | Commerce, AI and Agents, plus core modules and the Finance/Ordering hard dependencies. Finances is visible to a permitted administrator. |

| Arke Kids | `arke-kids` | Dedicated admin profile; **no installed provisioning pack found** | AI and Agents assumed for this design. Customer registry and team administration remain platform-owned. |

| Remittance | `remittance` | Installed pack and dedicated profile | Finance, AI, Agents, Documents; focused transfer orders, corridors, partners, FX, ledger, compliance and approvals. |

Source evidence:

- `src/Aonik.SharedKernel/Abstractions/Packs/Data/{base,simi,food-commerce,remittance}.pack.json`

- `src/Aonik.AdminUi/src/modules/profileCatalog.ts`

- `src/Aonik.AdminUi/src/config/admin-profiles/{general,food-commerce,arke-kids,remittance}.json`

- `src/Aonik.AdminUi/src/modules/{core,platform,finance,commerce,agent-command-center,agent-extensions}/index.ts`

- `src/Aonik.AdminUi/src/modules/adminProfiles.ts`

The showcase expands each declared module set using the checked-in catalogue's core and hard dependencies, then resolves profile navigation against enabled modules, route requirements, tenant audience and available policy names. It assumes a fully permitted tenant administrator. It is a design renderer, **not an authorization boundary** or a replacement for the production resolver. Host-only menus are omitted. Empty groups disappear. Profile labels are preserved, including Food Orders, Menu, Menu Content and Business Overview. The Workspace shortcut is shell chrome, outside the profile's navigation sections.

Personal Finance is the user-facing name throughout the new showcase and image filenames; the installed `simi` key is retained for compatibility. The current general profile does **not** expose a separate budgets/goals admin section, so its marketing design shows companion usage and customer references. This is an operator view, not the consumer Personal Finance experience. A dedicated personal-finance admin profile would be a separate product change.

Payabo, MyBillAfrica and RemitExchange are product/platform context; they are not additional installed business-type pack keys in this checkout.

## Marketing scenes

Each scene has light and dark variants. Add `&theme=dark` to these URLs:

- `showcase.html?business=base&screen=home`

- `showcase.html?business=base&screen=finance.invoices`

- `showcase.html?business=simi&screen=ai.usage`

- `showcase.html?business=food-commerce&screen=commerce.orders`

- `showcase.html?business=food-commerce&screen=commerce.products`

- `showcase.html?business=arke-kids&screen=customers`

- `showcase.html?business=remittance&screen=finance.orders`

- `showcase.html?business=remittance&screen=finance.countries`

Add `&capture=1` to remove the design-studio controls. Search, status filtering, CSV export, record detail dialogs, theme switching, sidebar collapse and the screen palette work locally. Other existing menu destinations show an explicit placeholder rather than fabricated functional screens. Ctrl/Cmd+K opens the palette; Ctrl/Cmd+B toggles the sidebar.

All tenants, contacts, IDs, amounts, activity series and performance deltas are fictional and fixed to October 2026. These are marketing **design mockups**, not screenshots of a connected production deployment. Summary metrics represent an illustrative monthly window; tables show a small recent sample, not the source of those aggregates. Chart series are relative illustrative activity, not a financial calculation. The design retains a small demo label and fictional-data footer. Completed order details illustrate separate payment-intent and ledger references; they do not imply a live transaction. The food design explicitly leaves fulfilment untracked and does not invent dispatched/delivered states.

Generate the 16 included PNGs and run browser checks:

```powershell

node Templates/aonik-admin-starterkit/scripts/capture-showcases.mjs

```

Add `--canvas` to also verify the original React/Babel design canvas renders.

Requires Node 22+ and Chromium, Chrome or Edge. The script discovers a local Chromium installation; set `CHROME_PATH` if needed. It serves the kit on a temporary loopback port, uses an isolated browser profile, captures at 1600 × 1280 CSS pixels / **3200 × 2560 output pixels**, verifies key interactions and menu exclusions, and writes `marketing/screenshots/capture-report.json`. The gallery links to the full-resolution images and editable scenes.

## Remittance pack

Remittance is installed in the main application: `src/Aonik.SharedKernel/Abstractions/Packs/Data/remittance.pack.json` and `src/Aonik.AdminUi/src/config/admin-profiles/remittance.json`. The separate proposal files have been removed. Tenant creation exposes the new business type. The showcase snapshot reads these runtime sources directly.

The pack declares Finance, AI, Agents and Documents, with Platform/Ordering resolved through the core/dependency graph. It contains branding configuration and no sample entities, agent overrides, fixed models or operational partner/corridor/rate configuration. Transfer orders labels the unified order list, which offers a Remittance filter. The main application also includes a dedicated operator transfer editor and read-only execution details; see `docs/features/remittance-transfer-editor.md`. See `docs/features/remittance-business-pack.md` for the provisioned module set, route coverage, approval boundary and tests.

## Menu icon library

Open `menu-icons.html` to inspect the 92 SVG menu icons at 32px and 16px in light or dark. The icon library also opens directly from a local file. Each glyph uses a 24px viewBox, 1.75px strokes, rounded caps and joins, and `currentColor`, so it inherits the neutral/teal menu state in either theme.

- `assets/menu-icons.js` is the shared source: vector paths, explicit screen/group mappings, domain-specific overrides and legacy menu mappings.

- `assets/menu-icons/*.svg` contains individual reusable vector files; `sprite.svg` contains symbols named `aonik-menu-<name>`.

- The native showcase, command palette and both React canvas sidebar variants use the same paths. New screen IDs must receive an explicit mapping; no generic grid fallback is used for menu items.

- Food Commerce maps Products to the cutlery Menu icon and Orders to the food bag icon. Remittance uses transfer-order arrows; invoices, ledgers, chart of accounts, journal entries, corridors, partners and FX each have separate glyphs.

- These assets are integrated into both the starter kit and the production Admin UI. The profile resolver assigns shared icons to screens and groups; the sidebar, collapsed menus and command palette render them through `AonikTemplateIcon`. The selected profile determines domain overrides, so a host administrator retains general product glyphs even when viewing a food-commerce tenant.

Regenerate or check the SVG assets and the icon contact sheet from the repository root:

```powershell

node Templates/aonik-admin-starterkit/scripts/build-menu-icons.mjs

node Templates/aonik-admin-starterkit/scripts/build-menu-icons.mjs --check

```

The generator checks all 103 registered screens, all five business configurations and 75 menu entries across the two canvas shells. The capture script checks the rendered menu mappings and SVG geometry, refreshes the 16 business screenshots, and saves `marketing/screenshots/menu-icons-light@2x.png` and `menu-icons-dark@2x.png` as additional 3200 × 3600 contact sheets. Standalone SVGs carry a title; inline menu SVGs are hidden from assistive technology because the adjacent menu label supplies their accessible name.

The generator also writes `src/Aonik.AdminUi/src/lib/menuIcons.generated.ts`, containing the exact same paths and mappings as the kit. Production builds consume that local TypeScript file and do not load the template, a browser global or external SVG URLs. `menu-` references distinguish the new menu family from legacy icons used elsewhere. Unmapped extension screens can still use their registered icon; all current built-in screens and profile groups have explicit shared mappings. This integration changes presentation only and preserves the existing module, permission, policy and audience filters.

From `src/Aonik.AdminUi`, run `npm run sync:menu-icons` after changing the shared source and `npm run check:menu-icons` to detect drift. Tests also compare the generated paths and mappings directly with the starter-kit source and exercise the resolved navigation against the real module registry.

A sprite symbol can be used as follows (the containing control supplies its accessible label):

```html

<svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true">

  <use href="assets/menu-icons/sprite.svg#aonik-menu-invoices"></use>

</svg>

```

## Production transfer editor screenshots

The marketing gallery includes light/dark captures of the actual Admin UI transfer editor with fictional API fixtures, in addition to the eight standalone design scenes. Start the Admin UI with mock authentication on port 4175 and run `node src/Aonik.AdminUi/scripts/verify-transfer-editor.mjs` from the repository root. It checks quote invalidation, explicit confirmation, pre-execution rejection, lost-response recovery and read-only details. These captures are 3200 × 2560 pixels and do not execute live transfers.

The production Beneficiaries list and Add payout destination form are also captured in both themes. Run `node src/Aonik.AdminUi/scripts/verify-beneficiaries.mjs` with the same mock-auth development server. The checks cover customer scope, rail-specific inputs, provider failure/manual retry, notes and removal. The shared icon source now includes dedicated Beneficiaries and Mobile money glyphs. See `docs/features/remittance-beneficiaries.md`.

The transfer verification script also captures the Send receipt dialog in both themes and tests receipt downloads, email confirmation and recovery using fake delivery. First generate the server PDF fixtures with `$env:WRITE_RECEIPT_EXAMPLES='1'` and `dotnet test tests/Aonik.Infrastructure.Tests --filter FullyQualifiedName~RemittanceReceiptRendererTests`. See `docs/features/remittance-transfer-editor.md` for endpoints and send semantics. The icon generator also writes `src/Aonik.Infrastructure/Receipts/transfer-orders.svg`, which is embedded in the server receipt so production builds remain independent of the template folder.

The same script captures receipt email history in both themes and checks recorded masked recipients, request/outcome times, status labels, automatic/manual refresh, error and empty states, server pagination and read-only visibility. These scenes and `receipt-history-report.json` use fictional send records; reading the history never sends email.

Delivery tracking reuses the shared Delivered/Bounced SVGs. The eleventh artboard, Receipt delivery, distinguishes delivered, bounced, awaiting-report and unavailable older sends. See `docs/features/receipt-email-delivery.md` for live Event Grid setup; native artboards use fictional local data.

Bounced receipt rows include **Review email**, using the shared Bounced icon. The twelfth editable artboard lets you correct a saved customer email, confirm the update, then select and confirm the recipient in a separate resend step. These interactions stay local to the artboard. The production verification script also captures `remittance-review-receipt-email-{light,dark}@2x.png` and writes `receipt-email-review-report.json`.
