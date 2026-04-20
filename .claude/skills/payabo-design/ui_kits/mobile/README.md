# Payabo Mobile — UI kit

High-fidelity recreation of the Payabo mobile app (Flutter, iOS form-factor).

## Included screens
- **Home** (`HomeScreen.jsx`) — warm screen, balance hero, quick actions, bills due, recent activity.
- **Pay** (`Screens.jsx · PayScreen`) — dark hero, send-to contacts, country corridors, recent activity.
- **Spending** (`Screens.jsx · SpendingScreen`) — dark hero, Safe to spend, donut breakdown, Simi insight.
- **Chat / Simi** (`Screens.jsx · ChatScreen`) — warm dark background, bubbled AI conversation, voice input.
- **Transaction detail** (`Screens.jsx · TxnDetailScreen`) — warm screen, amount hero, metadata list.

## Included components (`components.jsx`)
- `PayButton` · primary/secondary/link/ghost · sm/md/lg (40/48/52) · uppercase labels, 4px radius
- `PayField` · Material-style underline text field with animated label + error state
- `PayChip` · success/warning/danger/info/neutral/warm tones (status pills & categories)
- `PayCard` · white / warm / dark / flat variants
- `PayTxnRow` · avatar · title · sub · amount · optional chip
- `PayBottomNav` · 4 items + center orange FAB (matches app's real bottom bar)
- `Icon` · inline outline icons, Material Rounded feel (stroke 2)

## Constants
`PAY` object exports brand colors and `payHero` / `payWarmScreen` / `payChatHero` / `payChatScreen` / `paySafe` gradients.

## Source of truth
All values come from `payabo_mobile/lib/shared/theme/` (colors, type scale, button heights) and the screen presentation code under `lib/features/`.

## Not included
- Real Flutter state management (persistence, auth, live data) — UI-only recreation.
- Full bills catalog, provider picker, and multi-step payment flow wizards — see codebase.
- Camera scan / KYC capture screens.
