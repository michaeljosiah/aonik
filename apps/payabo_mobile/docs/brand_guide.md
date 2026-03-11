# Payabo Mobile Brand Guide

## Status
This is the current working brand guide for the Flutter mobile app in `apps/payabo_mobile`.

It is based on the code now centralized in:
- `lib/shared/theme/payabo_palette.dart`
- `lib/shared/theme/payabo_colors.dart`
- `lib/shared/theme/payabo_gradients.dart`
- `lib/shared/theme/payabo_typography.dart`
- `lib/shared/widgets/payabo_app_header.dart`
- `lib/shared/widgets/payabo_bottom_nav.dart`
- `lib/shared/widgets/payabo_warm_scaffold.dart`
- `lib/shared/widgets/payabo_screen_title_bar.dart`

This guide is ready for use as the first documented baseline.

## Brand Character
Payabo mobile should feel:
- warm
- practical
- encouraging
- human
- financially calm without looking sterile

The visual system uses a bright orange primary action color, soft warm neutrals for atmosphere, dark ink for core text, and restrained success/info colors for financial signals.

## Core Palette

### Brand
- `Brand Primary`: `#F37920`
- `Brand Primary Hover`: `#D55F0B`

### Ink and Neutrals
- `Ink 900`: `#1A1C20`
- `Neutral 500`: `#B4BFC3`
- `Neutral 050`: `#F7F8FA`
- `Neutral 100`: `#F2F4F4`
- `Neutral 200`: `#E5E9EA`

### Warm Neutrals
- `Warm 050`: `#FFFFFCF9`
- `Warm 100`: `#FFFFFBF7`
- `Warm 150`: `#F7EEE4`
- `Warm 200`: `#F4ECDE`
- `Warm 300`: `#DCCDB7`
- `Warm 500`: `#D7A14E`
- `Warm 600`: `#9B7A43`
- `Warm 800`: `#77594A`
- `Warm 900`: `#4D3120`

### States
- `Success`: `#4ACB64`
- `Success Soft`: `#ECFAEF`
- `Warning`: `#FF9E15`
- `Danger`: `#E60037`
- `Info`: `#2465E8`

## Semantic Color Roles
Use semantic roles in UI code instead of raw hex values.

### Text
- `textPrimary`: main body and headline text
- `textSecondary`: warmer high-emphasis copy
- `textMuted`: tertiary/supporting copy
- `textSubtleWarm`: warm supporting copy
- `textInverse`: text on dark or strong brand surfaces

### Surfaces
- `surfaceBase`: white cards and standard component backgrounds
- `surfaceSubtle`: neutral app surfaces
- `surfaceMuted`: muted backgrounds and disabled surfaces
- `surfaceWarm`: primary warm app background
- `surfaceWarmElevated`: elevated warm panels
- `surfaceWarmAccent`: warm accent surfaces behind icons and small UI moments

### Borders
- `borderDefault`: standard component border
- `borderStrong`: stronger card/shadow-adjacent border
- `borderWarm`: warm border for expressive surfaces

### Navigation
- `navBackground`
- `navBorder`
- `navSelected`
- `navUnselected`
- `navFabBackground`

### Header
- `headerTitle`
- `headerSubtitle`
- `headerIconSurface`
- `headerIconSurfaceAccent`
- `headerIconBorder`
- `headerIconAccent`
- `headerNotificationDot`

## Typography
Primary typeface: `Open Sans`

### Scale
- `displayLarge`: `60 / 66`, weight `300`
- `displayMedium`: `48 / 52`, weight `300`
- `headlineLarge`: `42 / 45`, weight `300`
- `headlineMedium`: `27 / 34`, weight `700`
- `titleLarge`: `20 / 27`, weight `700`
- `titleMedium`: `18 / 25`, weight `700`
- `titleSmall`: `16 / 24`, weight `600`
- `bodyLarge`: `16 / 24`, weight `400`
- `bodyMedium`: `15 / 22`, weight `400`
- `bodySmall`: `13 / 18`, weight `400`
- `labelLarge`: `14 / 20`, weight `700`
- `labelMedium`: `14 / 20`, weight `600`

### Usage Rules
- Use `headline` and `title` roles for structure, not custom font sizes by default.
- Use `bodyLarge` for primary explanatory copy.
- Use `bodySmall` and `labelMedium` for supporting metadata.
- Avoid ad hoc `TextStyle(...)` unless documenting a new reusable pattern.

## Spacing
Spacing is based on a compact token scale:
- `xxs`: `2`
- `xs`: `4`
- `sm`: `8`
- `md`: `12`
- `lg`: `16`
- `xl`: `20`
- `x2`: `24`
- `x3`: `30`
- `x4`: `40`

### Layout Guidance
- Standard page padding: `horizontal 20`, `vertical 16`
- Standard card padding: `20`
- Large card padding: `30`
- Standard list item padding: `horizontal 20`, `vertical 16`

## Radius
- `sm`: `4`
- `md`: `5`
- `lg`: `12`
- `xl`: `20`
- `pill`: `50`

### Usage Rules
- `4` for standard button and card corners
- `12` for larger surfaces when they need more softness
- `20` for sheets and expressive larger modules
- `50` for pill controls, chips, and rounded badges

## Background Treatments

### Default Warm Screen
The primary Payabo mobile screen treatment is:
- background color: `surfaceWarm`
- gradient: `warmScreen`
- top-to-bottom, calm, soft, atmospheric

This is the canonical shell for dashboard, profile, payment flows, spending flows, and other branded app areas.

### Chat Screen
Chat uses a distinct warmer conversational treatment:
- `chatScreenSurface`
- `chatScreen` gradient
- soft glowing accent orbs

This treatment should stay limited to conversational or assistant-led experiences.

## Component Rules

### Header
Use `PayaboAppHeader` for:
- profile entry point
- notifications affordance
- warm title/subtitle treatment

Do not recreate header icon colors or circular chrome in feature screens.

### Title Bars
Use `PayaboScreenTitleBar` for:
- payment flows
- profile detail screens
- any centered screen title with optional back or close control

### Warm Screen Shell
Use `PayaboWarmScaffold` whenever a screen follows the main branded app shell.

### Buttons
- Primary actions use `brandPrimary`
- Hover/pressed state uses `brandPrimaryHover`
- Disabled state uses muted neutral surfaces and text
- Primary CTA text should remain uppercase where already established in shared components

### Cards
- Default cards are white or soft warm surfaces
- Use warm elevated cards sparingly for key summary modules
- Prefer tokenized border and shadow treatments over local decoration values

### Bottom Navigation
- White surface
- Warm border line
- muted inactive labels/icons
- warmer selected labels/icons
- orange center action button

This pattern is part of Payabo’s brand signature and should remain visually stable.

## Feature Accent Patterns

### Spending
Spending is allowed a broader accent range for charts and account summaries:
- warm account gradients
- green safe-to-spend card
- orange-led chart palette with supporting success/info accents

These accents should still be sourced from semantic theme tokens.

### Chat
Chat is allowed:
- deeper warm browns for conversational text
- soft warm sand surfaces
- orange glow accents

Chat may feel more intimate and editorial than transactional screens, but it should still remain recognizably Payabo.

## Brand Do
- Use shared theme tokens before introducing new colors
- Use warm surfaces to keep finance screens approachable
- Keep core actions bright and obvious
- Keep typography simple and legible
- Favor reusable widgets for branded chrome

## Brand Do Not
- Do not hardcode new hex values in feature screens unless the pattern is genuinely new
- Do not replace warm neutrals with cold gray-heavy layouts
- Do not introduce a second primary accent that competes with orange
- Do not style headers or nav bars ad hoc
- Do not treat dashboard-only decorative colors as global brand defaults without tokenizing them first

## Normalization Note
Most of the app is now structured well enough to support this guide directly.

The main remaining normalization area is `dashboard_screen.dart`, which still contains several bespoke decorative colors that should be extracted into semantic dashboard tokens before a stricter v2 guide is published.

This does not block the current guide.

## Ready For Next Phase
The codebase is ready for:
- a published v1 mobile brand guide
- token enforcement on new work
- dashboard token extraction as a follow-up refinement
