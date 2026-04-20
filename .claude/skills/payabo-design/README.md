# Payabo Design System

Payabo is the AI financial companion for Africans and the diaspora — bringing your money, bills, transfers, and financial insights into one place, with **Simi**, the in-app AI companion, helping you stay ahead and build a stronger future.

This project contains the design foundations, brand guidelines, and UI kit for building on-brand interfaces, mocks, and prototypes.

## Sources

- **Flutter codebase** (read-only mount): `payabo_mobile/`
  - Theme tokens: `lib/shared/theme/` (`payabo_palette.dart`, `payabo_colors.dart`, `payabo_gradients.dart`, `payabo_typography.dart`, `payabo_spacing.dart`, `payabo_radii.dart`)
  - Shared widgets: `lib/shared/widgets/` (buttons, cards, headers, nav, list rows, text fields)
  - Features: `lib/features/{dashboard,chat,spending,payments,profile,auth,setup_journey}`
  - Brand guide: `docs/brand_guide.md` (v1 baseline)
  - Real brand assets: `assets/images/` (Simi portrait, setup/spending hero art, payment mockups, country flags)

## Products

- **Payabo Mobile** — Flutter app (iOS + Android). Primary surface. Features:
  - **Home** — warm greeting, dark hero banner with balance/metrics, draggable warm sheet with bills, orders, support obligations
  - **Pay** — send money, pay bills, quick send to friends/family (Ghana / Nigeria / UK focus)
  - **Spending** — account cards, transactions, budgets, bills, Safe-to-Spend insight; connected via Plaid
  - **Chat (Simi)** — deep-brown conversational surface, voice + text, financial assistant
  - **Profile / Setup journey** — onboarding with hero imagery, OTP, country selection, account linking

## Index

- `README.md` (this file) — overview, content fundamentals, visual foundations, iconography
- `colors_and_type.css` — single source of truth for CSS variables, type scale, gradients
- `SKILL.md` — agent-invocable skill for generating Payabo-branded output
- `assets/` — real brand images, illustrations, flags
- `preview/` — HTML cards that populate the Design System tab
- `ui_kits/mobile/` — JSX components + interactive click-through of the mobile app

## Content Fundamentals

**Voice.** Warm, practical, encouraging, human. Financial calm without being sterile. Reads like a competent friend who happens to know money — not a bank form.

**Pronouns.** Second person "you" and "your". First person only when Simi speaks ("I'll keep an eye on your accounts and flag anything unusual."). Never "we" or "the company".

**Casing.**
- Sentence case for headings, labels, buttons' display strings, row titles, cards. ("Pay a bill", "Support your family, wherever they are.")
- **UPPERCASE** for the rendered button label — `PayaboButton` uppercases the string for you in code, so write the source string as sentence case.
- Currency shown with ISO code + space: `GHS 500.00`, never "$500".

**Tone examples (lifted from the codebase):**
- Welcome strip: "Welcome back / Kwame Mensah"
- Greeting logic: "Good morning / Good afternoon / Good evening" by hour — always followed by first name.
- Simi hero line: "Here's your spending overview. I'll keep an eye on your accounts and flag anything unusual."
- Support framing: "Support your family, wherever they are. / Send money, pay bills, track everything."
- Empty state (no bills): "no bills" / "1 bill" / "N bills" — pluralized with care.
- Pay screen verbs: "Pay a bill", "Send money", "Start" (CTA label on option cards).
- Status chips: "Completed", "Processing", "Failed" — title case, single word where possible.
- Transaction lines: "Transfer to Ama Serwaa", "DSTV subscription", "ECG prepaid top-up", "Water bill" — concrete, named, never "Payment #12345".
- Time: "Today, 09:42 AM", "Yesterday, 07:18 PM", "Monday, 11:05 AM", "May 3, 2026, 07:18 PM" — casual where possible, precise when receipts matter.

**Emoji.** Not used in UI copy. The brand voice is warm but not emoji-warm. Flags are SVG illustrations, not 🇬🇭.

**Unicode.** Curly apostrophes (`'`) in prose. Bullet `·` for separators is acceptable. No decorative chars.

**Numbers.** Balances in the hero can be hidden behind a toggle; when shown they use the display typescale. Amounts everywhere else use `title` weight + currency code.

**What we avoid.** Marketing-speak ("unlock your financial potential"), crypto hype, scarcity ("only 3 spots!"), exclamation marks except in genuine celebration (`"Completed ✓"` uses the check, not "!"), or jargon. If a bank would say it, rewrite it.

## Visual Foundations

**The signature move.** Almost every primary screen follows the same three-layer composition:
1. A **dark charcoal gradient hero** (`#242223 → #191718 → #0F0D0E`, top-to-bottom) occupies the top ~37% of the viewport. White text on it.
2. A **warm off-white `DraggableScrollableSheet`** (`--pay-warm-050`) pulls up over the hero with a 24px top radius that animates to 0 as the sheet reaches full extent.
3. A **bottom tab bar** with white surface, warm top border, and a **circular orange FAB** that breaks the nav's top edge by 18px — this is the brand's clearest visual signature.

The Chat screen swaps step 1's gradient for a warmer brown (`#261C16 → #1A130F → #120D0A`); step 2 becomes a deeper warm surface (`--pay-chat-screen-surface: #F8ECDD`) with orange glow "orbs" behind Simi's messages.

**Colors.** Orange `#F37920` is the single accent — used for FAB, primary buttons, focus rings, amount chips, and Simi's glow. Warm neutrals (cream → sand → clay → coffee) carry surfaces, borders, and supporting text. Cold grays appear only in status (info blue, danger red) — never as layout. The Safe-to-Spend card is the one green moment, and it's a dark forest gradient, not mint.

**Imagery.** Real photography of African subjects in warm-lit interiors — cafés, living rooms, the Ankara-print jacket in `setup-hero.png`, Simi's portrait against cinnamon brown. Never stock-library illustrations. For abstract concepts (empty states), 3D isometric renders with orange glow on dark ground (`spending-empty-hero.png`). No vector-flat illustrations.

**Typography.** Open Sans throughout, loaded via google_fonts. Display weights are 600 (semibold) with slight negative letter-spacing for hero numerics. Titles jump to 700. Body stays at 400. The scale is compact by modern standards — `bodyLarge` is 14px, `bodySmall` is 11px. Button labels are uppercased 12/16 700.

**Spacing.** A named token scale: `2 · 4 · 8 · 12 · 16 · 20 · 24 · 30 · 40`. Page padding = `20h / 16v`. Card padding = `20`. Large card = `30`. List row = `20h / 16v`. Gap between hero and sheet = ~10px.

**Radii.**
- `4px` — **the default**. Buttons, standard cards, list rows (this is unusually tight; don't round more without intent).
- `12px` — softer larger surfaces.
- `20px` — bottom sheets, expressive modules, sheet top corners.
- `50px` — pills, chips, badges, avatars.

**Backgrounds.**
- Warm screen (`warm050 → warm150`) is the default non-hero shell.
- Dark hero (`#242223 → #191718 → #0F0D0E`) on Home / Pay / Spending dashboards.
- Chat hero uses warmer browns.
- `spendingSafeToSpend` is a dark forest gradient; `spendingInsight` a peach gradient.
- No repeating patterns, no grain, no noise textures. Solid fills and smooth vertical gradients only.

**Animation.**
- `DraggableScrollableSheet` snaps between two sizes with spring physics — the sheet's top radius interpolates 24 → 0 in the last 5% of travel, at the same time the status-bar overlay fades in.
- `PayaboTypewriterText` types Simi's hero line character-by-character on first load.
- Page transitions use `go_router` defaults (platform-native — iOS slide, Android fade).
- Voice stage has a 4-phase pulse (idle → listening → thinking → speaking) with a radial glow scaling on audio.
- No bouncy overshoots, no rotate-on-hover. Motion is calm and financial.

**Hover / Press.**
- Primary button: background shifts to `#D55F0B` (brandPrimaryHover), same on hover and pressed. No elevation change.
- Secondary (outlined) button: 8% orange fill on hover, border + text shift to hover orange.
- Link button: white surface that shifts to `warm050` on press.
- List rows use Material's default ink ripple, bounded to the 4px radius.

**Borders.** Hairline 1px borders are common and critical — `borderDefault` (`#E5E9EA`) or `borderStrong` (a cool `rgba(180,191,195,0.4)`). Warm borders (`warm300 #DCCDB7`) appear on expressive surfaces only. Buttons use a 2px primary border (same color as the fill) so outlined/primary variants share silhouette.

**Shadows.** The code has empty shadow lists (relying on borders for definition), but the live product renders warm-tinted soft drops. Use `--pay-shadow-card` for lifted cards and `--pay-shadow-fab` for the orange center button. Nav uses a negative-Y 10px shadow so the bar floats above content.

**Transparency & blur.** The status-bar overlay fades in using opacity only — no backdrop-filter blur. Chat glow orbs use two low-alpha orange+sand radial fills. Otherwise transparency is reserved for disabled states (50% foreground opacity on muted surfaces).

**Cards.** Default: white surface, `borderStrong` 1px hairline, 4px radius, shadow-card on light backgrounds, no shadow on dark. Warm variant: `--pay-spend-card-warm-elev` (`#FFFBF8`) with `--pay-spend-quick-border` (`#F1DEC9`). Dashboard hero cards sit inside the dark gradient on `rgba(255,255,255,0.06)` with a 0.08 white stroke.

**Layout rules (fixed elements).**
- Bottom tab bar is 74px tall + safe area; 4 items arranged 1-1-[gap]-1-1 around the center FAB.
- App header pads `20 / 12 / 20 / 16` and pins while the sheet drags up over it; background fades in only in the last 5% of sheet travel.
- Screen title bar has a fixed 32px-wide slot for back and close icons regardless of presence (keeps title centered).

**Iconography.** Material Icons (rounded) in the app; `flutter_svg` for country flags. See ICONOGRAPHY below.

## Iconography

- **Primary icon system:** Flutter's built-in **Material Icons (rounded variants)** — `Icons.notifications_none_rounded`, `Icons.arrow_back_ios_new`, `Icons.chevron_right`, `Icons.person_outline_rounded`, `Icons.add`, `Icons.close`, etc. Stroke weight is the stock rounded style; sizes are 18 / 21 / 22 / 40 — never arbitrary.
- **For this design system (web):** we substitute **[Lucide](https://lucide.dev)** icons from CDN — rounded stroke, 2px, which is the closest match to Material Rounded's silhouette. **Flagged substitution** — update font / icon set if you want exact Material Rounded parity.
- **SVG assets:** country flags live in `assets/flags/` as real SVG flags (`ng.svg`, `gh.svg`, `gb.svg`, `zm.svg`, `zw.svg`, `bw.svg`) — loaded via `flutter_svg`. The empty-state spending illustration is also a SVG.
- **Emoji:** NOT used. Flags are SVG illustrations; status is conveyed by color + word; support for 🇬🇭-style emoji flags is explicitly absent.
- **Unicode:** `·` (middle dot) allowed as a separator. `'` curly apostrophe preferred in prose. No other decorative glyphs.
- **PNG assets:** used for photography (Simi portrait, setup-hero, slider images), 3D renders (spending-empty-hero, budget-hero), and logos. Vector is for UI iconography only.
- **App icon / brand mark:** Payabo itself doesn't ship a standalone wordmark in this repo — `mba_logo.png` belongs to "MyBillAfrica" (a related service referenced in the app). Until a Payabo logo is supplied, the word **Payabo** set in Open Sans 800 with the `·` orange accent is used as a wordmark stand-in. **Flagged:** needs real logo file.
