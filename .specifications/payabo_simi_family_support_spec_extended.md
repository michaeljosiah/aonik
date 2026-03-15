# Payabo Feature Specification
## Family Support Planning + AI Persona (Simi) + Post-Setup AI Processing

Version: 1.1  
Target App: `apps/payabo_mobile`  
Tech Stack: Flutter, Riverpod, go_router, Material 3  

---

# 1. Overview

This feature introduces **Family Support Planning** to Payabo, a core capability designed specifically for diaspora users who regularly support family or community members financially.

Rather than treating remittances as isolated transactions, Payabo will treat them as **structured financial commitments** that can be planned, forecasted, and optimized.

To reinforce Payabo’s AI-first experience, the onboarding process will introduce the assistant **Simi**, who will guide the user through setup and initial financial personalization.

After the user completes the initial setup journey, instead of immediately navigating to the dashboard, the system will show an **AI Processing Sequence** where Simi processes the onboarding information and prepares the user’s personalized financial assistant.

This specification extends the original feature with:

- a post-setup AI processing sequence
- a stronger dashboard handoff pattern
- a beneficiary capture strategy
- agent skills recommendations for implementation support
- implementation guidance for Flutter + Riverpod + go_router + Material 3

---

# 2. Goals

## Primary goals

- Introduce **Simi** as Payabo’s AI assistant
- Create a moment where the system feels intelligent and personalised
- Prepare the user’s dashboard using the onboarding signals
- Introduce the **Family Support Planning** capability
- Encourage users to add beneficiaries naturally

## Secondary goals

- Avoid overwhelming users during initial setup
- Maintain low friction onboarding
- Reinforce trust and transparency in AI actions
- Keep future AI execution proposal-based rather than automatic

---

# 3. AI Persona: Simi

## Name
Simi

## Personality

Simi should feel:

- calm
- intelligent
- encouraging
- financially responsible
- non-judgmental
- premium but warm

She should never sound:

- pushy
- overly robotic
- overly playful
- comedic
- childish
- manipulative

## Tone example

> “Hi, I’m Simi. I’ll help you stay on top of your finances and make smarter decisions with your money.”

## Product role

Simi is not just a chatbot.

She is the user-facing expression of Payabo’s intelligence layer and should be treated as:

- onboarding guide
- dashboard narrator
- planning assistant
- recommendation engine voice
- financial support coach

---

# 4. User Flow Summary

## High-level flow

1. User completes registration and login
2. User completes guided setup
3. User enters **Simi AI Processing Screen**
4. Simi prepares personalised financial context
5. User lands on dashboard
6. Dashboard shows **Simi introduction card**
7. User can optionally add beneficiaries
8. User can later create support plans

---

# 5. Post-Setup Processing Screen

## Trigger

Occurs immediately after the user completes the onboarding setup flow.

### Route example

`/setup/processing`

---

## Purpose

The processing screen exists to:

- avoid a jarring jump from setup to dashboard
- make the AI feel active and intentional
- communicate that Payabo is shaping the user experience
- build anticipation
- seed trust that the system is tailoring itself to the user

---

## UX concept

A full-screen experience showing Simi preparing the user’s financial assistant.

This screen should feel:

- calm
- premium
- alive
- intentional
- trustworthy

It should not feel fake or overly theatrical.

---

# 6. Processing Animation Sequence

The animation will show a sequence of steps.

Each step represents Simi analysing different parts of the onboarding data.

## Step 1

**Message**

> “Hi, I’m Simi. Let me prepare your financial assistant.”

---

## Step 2

**Message**

> “Understanding your financial priorities…”

**Based on**

- use cases selected in onboarding

---

## Step 3

**Message**

> “Mapping your accounts and money sources…”

**Based on**

- UK bank accounts
- Nigerian bank accounts
- mobile wallets
- manual tracking

---

## Step 4

**Message**

> “Organising your bills and responsibilities…”

**Based on**

- rent
- utilities
- subscriptions
- family support

---

## Step 5

**Message**

> “Learning who you support financially…”

This step only appears if family or community support was selected.

---

## Step 6

**Message**

> “Preparing your financial dashboard…”

---

## Step 7

**Message**

> “Your financial assistant is ready.”

---

# 7. Animation Behaviour

## Recommended Flutter patterns

- `AnimatedSwitcher`
- `AnimatedOpacity`
- `AnimatedSlide`
- subtle progress bar or radial progress
- soft icon or pulse animation for Simi
- optional shimmer for loading states

## Timing guideline

- each step: ~1.0 to 1.4 seconds
- total duration: ~6 to 8 seconds
- total time must feel believable

## Rule

If backend work completes early, the sequence may skip ahead gracefully.

Do not artificially force a long delay.

---

# 8. Transition to Dashboard

After processing completes:

### Navigation

`/dashboard`

However, the dashboard will immediately show a **Simi introduction card** at the top.

This card should be treated as the first post-setup dashboard moment.

---

# 9. Simi Introduction Card

## Purpose

The dashboard card should:

- acknowledge what the user selected in setup
- prove that Simi has understood something meaningful
- introduce Family Support Planning if relevant
- offer a clear next action
- avoid pushing the user into a form too early

---

## Card example

### Title

**Simi — Your Financial Assistant**

### Message

> “You mentioned supporting family. I can help you plan and manage those payments so they never catch you off guard.”

### Actions

- Add someone you support
- Not now

---

## Alternate non-support card example

If the user did not indicate family support:

> “I’ve organised your financial goals and priorities. Next, we can connect your accounts or build your first plan.”

Actions:

- Connect accounts
- Review plan

---

# 10. Beneficiary Capture Strategy

## Recommended approach

Do **not** ask for beneficiary names during the initial setup flow.

Instead:

- collect only support intent during setup
- surface the dashboard card after AI processing
- let the user add beneficiaries in a dedicated flow

## Why

This keeps setup:

- fast
- emotionally light
- low-friction
- AI-first rather than form-heavy

It also avoids forcing the user to enter details before they understand the value.

---

# 11. Add Beneficiary Flow

If the user selects **Add someone you support**:

### Route

`/support/add-beneficiary`

---

## Screen: Add Beneficiary

### Fields

- Name
- Relationship
- Country
- Typical support amount (optional)
- Frequency (optional)

### Example

- Name: Mum
- Relationship: Parent
- Country: Nigeria
- Typical support: £150
- Frequency: Monthly

---

## UX rule

This should feel like a light, supportive flow.

It should not feel like:

- a remittance form
- compliance onboarding
- a banking beneficiary registration wall

---

# 12. Family Support Planning

## Core concept

Payabo should treat support obligations like **planned financial commitments**, not just payments.

## Example support plan

- Mum
- £150 / month
- Next due: 5th

## Dashboard integration

Support plans should appear alongside bills.

### Example

**Upcoming**

- Rent — £900 — 1st
- Mum — £150 — 5th
- Electricity — £80 — 10th

This gives the user a realistic view of their finances.

---

# 13. AI Insight Examples

Once support plans exist, Simi can generate insights.

## Example 1

> “Based on your upcoming bills and your usual £150 transfer to Mum, you’ll still have £420 remaining this month.”

## Example 2

> “You usually send money to your parents around the first week of the month. Would you like me to remind you earlier next time?”

## Example 3

> “You are paying fees sending money home each month. I can help you compare better timing or lower-cost routes.”

---

# 14. Safety Principles

The system must follow these rules:

- never move money automatically
- all actions require explicit user approval
- AI suggestions must be transparent
- user must be able to disable reminders
- planning must be separated from execution
- future automation must follow proposal → approval → apply

---

# 15. Domain Model

## Beneficiary

```dart
class Beneficiary {
  final String id;
  final String name;
  final RelationshipType relationship;
  final String countryCode;
  final Money? typicalAmount;
  final Frequency? frequency;
}
```

## SupportPlan

```dart
class SupportPlan {
  final String id;
  final String beneficiaryId;
  final Money amount;
  final Frequency frequency;
  final DateTime nextDueDate;
}
```

## SupportPlanningState

```dart
class SupportPlanningState {
  final List<Beneficiary> beneficiaries;
  final List<SupportPlan> supportPlans;
}
```

---

# 16. Suggested Enums

```dart
enum RelationshipType {
  parent,
  sibling,
  child,
  spouse,
  relative,
  community,
  church,
  other,
}

enum Frequency {
  weekly,
  monthly,
  quarterly,
  occasionally,
  custom,
}
```

---

# 17. Dashboard Integration Rules

The dashboard should adapt based on the user’s setup signals.

## If support selected

Prioritise:

- Simi support card
- upcoming support plan
- bill timeline
- connect account reminder if skipped

## If savings/goals selected

Prioritise:

- goals summary
- safe-to-save insight
- budget and cash-flow module

## If bills selected

Prioritise:

- upcoming bills
- recurring obligations
- reminders

---

# 18. Post-Setup Seed Model

At the end of AI processing, generate a dashboard seed object.

```dart
class DashboardSetupSeed {
  final String greetingVariant;
  final List<String> suggestedModules;
  final List<String> quickActions;
  final List<String> nudges;
}
```

## Example seed

- greetingVariant: “support-planner”
- suggestedModules:
  - upcoming-bills
  - support-plans
  - goals
- quickActions:
  - add-beneficiary
  - connect-account
- nudges:
  - setup-first-support-plan

---

# 19. Flutter Implementation Guidance

## Feature folders

Recommended additions inside `apps/payabo_mobile/lib/features/`:

- `setup_journey/`
- `support_planning/`

### Suggested structure

```text
lib/features/setup_journey/
  application/
  domain/
  presentation/
  widgets/
  models/

lib/features/support_planning/
  application/
  domain/
  presentation/
  widgets/
  models/
```

---

## Key screens

### Setup journey

- `setup_journey_screen.dart`
- `setup_processing_screen.dart`

### Support planning

- `add_beneficiary_screen.dart`
- `support_planning_overview_screen.dart` (future)
- `support_plan_detail_screen.dart` (future)

---

## Core widgets

- `SetupAiMessagePanel`
- `SetupActionCard`
- `SetupProcessingStep`
- `SimiDashboardCard`
- `BeneficiaryFormCard`

---

# 20. Riverpod Guidance

## Providers to add

### Setup journey

- `setupJourneyControllerProvider`
- `setupJourneyStepProvider`
- `setupJourneySeedProvider`

### Processing sequence

- `setupProcessingControllerProvider`

### Support planning

- `supportPlanningControllerProvider`
- `beneficiariesProvider`
- `supportPlansProvider`

## Rule

Do not bury step logic inside widgets.

Keep:

- typed state
- typed events
- derived providers
- UI as rendering layer

---

# 21. go_router Guidance

## Suggested routes

- `/setup`
- `/setup/processing`
- `/support/add-beneficiary`

## Rules

- preserve current auth flow
- setup processing should run after initial setup completion
- beneficiary flow should be accessible from dashboard and later from profile or support section

---

# 22. Material 3 and Design Rules

The feature must use the existing Payabo design system and shared theme tokens.

## Must preserve

- warm surfaces
- premium spacing
- calm typography
- action hierarchy
- existing button patterns
- safe area handling

## Must avoid

- introducing a disconnected visual language
- neon AI aesthetics
- cold sterile dashboard look
- overuse of animation

---

# 23. Performance and Device Considerations

The feature must work well on mobile-first devices and constrained environments.

## Requirements

- smooth on mid-range Android devices
- low dependency on large animation packages
- no heavy Lottie requirement unless already justified
- minimal layout overflow risk
- responsive on small screens

---

# 24. Testing Requirements

At minimum add:

- provider tests for setup and processing state
- widget tests for processing screen step progression
- widget tests for dashboard Simi card rendering
- widget tests for add beneficiary flow validation
- route rendering tests for new screens

---

# 25. Acceptance Criteria

## Post-setup processing

- user completes setup and sees Simi processing screen
- processing sequence advances through meaningful states
- user lands on dashboard after completion

## Dashboard handoff

- dashboard shows a Simi card aligned to setup data
- if family support was selected, the support card appears
- if support was not selected, an alternative personalisation card appears

## Beneficiary capture

- user can add a beneficiary later from dashboard
- beneficiary flow collects name, relationship, country, and optional plan data
- data persists in state/repository layer

## Family support planning

- support plan can appear in upcoming timeline
- support commitments are visually separated from generic transactions

---

# 26. Agent Skills Recommendations

This section describes skills that would be beneficial to create for the coding agent or engineering workflow supporting this feature.

According to the Agent Skills specification, a skill is a directory that must include a `SKILL.md` file with YAML frontmatter, and may optionally include `scripts/`, `references/`, and `assets/` directories. The key required fields are `name` and `description`, while optional fields include `license`, `compatibility`, `metadata`, and `allowed-tools`. The spec also recommends progressive disclosure: keep `SKILL.md` concise and move deeper details into referenced files. Validation can be done with `skills-ref validate`. citeturn776119view0

## Skill 1: flutter-feature-implementation

### Purpose

Guide an agent to build new Flutter features inside `apps/payabo_mobile` using existing repo conventions.

### What it should help with

- feature folder scaffolding
- Riverpod providers/controllers
- go_router integration
- Material 3 component composition
- widget and provider test scaffolding
- preserving theme/token usage

### Suggested contents

- `SKILL.md`
- `references/repo-structure.md`
- `references/flutter-patterns.md`
- `assets/feature-checklist.md`

---

## Skill 2: payabo-theme-guard

### Purpose

Ensure new Payabo screens conform to existing brand and theme rules.

### What it should help with

- token usage
- spacing rules
- typography consistency
- button hierarchy
- bottom sheet and card styling
- avoiding ad hoc colors/styles

### Suggested contents

- `SKILL.md`
- `references/brand-rules.md`
- `references/component-examples.md`

---

## Skill 3: setup-journey-designer

### Purpose

Help agents design and implement guided AI onboarding/setup flows.

### What it should help with

- multi-step setup architecture
- single-select vs multi-select decisions
- processing screen patterns
- dashboard handoff logic
- Simi copy patterns

### Suggested contents

- `SKILL.md`
- `references/setup-step-patterns.md`
- `assets/simi-copy-library.md`

---

## Skill 4: support-planning-modeler

### Purpose

Help agents model Family Support Planning in a way that aligns with Payabo product goals.

### What it should help with

- beneficiary models
- support plan models
- timeline integration
- reminder model design
- dashboard card logic

### Suggested contents

- `SKILL.md`
- `references/domain-mapping.md`
- `references/example-state-models.md`

---

## Skill 5: agent-copy-tone-simi

### Purpose

Ensure all AI-facing microcopy for Simi stays on-brand.

### What it should help with

- onboarding messages
- processing messages
- dashboard cards
- reminders
- support planning nudges

### Tone constraints

- warm
- concise
- clear
- trustworthy
- non-judgmental

### Suggested contents

- `SKILL.md`
- `references/tone-guide.md`
- `assets/message-templates.md`

---

# 27. Example Skill Structure

The Agent Skills specification defines a skill as a folder containing at minimum `SKILL.md`, with optional `scripts/`, `references/`, and `assets/` folders. `SKILL.md` must contain YAML frontmatter followed by markdown instructions. The `name` must match the folder name and use lowercase letters, numbers, and hyphens only. The `description` should explain both what the skill does and when to use it. citeturn776119view0

## Example

```text
flutter-feature-implementation/
├── SKILL.md
├── references/
│   ├── repo-structure.md
│   └── flutter-patterns.md
├── assets/
│   └── feature-checklist.md
└── scripts/
    └── create_feature_stub.py
```

---

# 28. Example SKILL.md Frontmatter

The Agent Skills specification requires YAML frontmatter followed by markdown content. Required fields are `name` and `description`. Optional fields include `license`, `compatibility`, `metadata`, and `allowed-tools`. citeturn776119view0

```md
---
name: flutter-feature-implementation
description: Create production-quality Flutter features in apps/payabo_mobile using Riverpod, go_router, shared theme tokens, and repo-aligned structure. Use when building new mobile features, screens, providers, or navigation flows.
license: Proprietary
compatibility: Designed for Flutter development in the AONIK repository with Dart, Riverpod, go_router, and Material 3.
metadata:
  product: payabo-mobile
  owner: engineering
---
```

---

# 29. Recommended Skill Creation Order

Create skills in this order:

1. `flutter-feature-implementation`
2. `payabo-theme-guard`
3. `setup-journey-designer`
4. `support-planning-modeler`
5. `agent-copy-tone-simi`

This order gives the strongest practical leverage first.

---

# 30. Future Skills

Potential future skills:

- `open-banking-account-linking`
- `nigeria-account-link-fallback`
- `dashboard-personalization`
- `proposal-driven-financial-actions`
- `payabo-widget-test-author`

---

# 31. Summary

This feature transforms Payabo from a payments-oriented experience into a financial partner that understands real obligations.

The combination of:

- Simi as AI persona
- post-setup AI processing
- Family Support Planning
- beneficiary capture after setup
- dashboard handoff intelligence

creates a more distinctive, emotionally resonant, and strategically differentiated Payabo experience.

The additional agent skills recommended here will help engineering agents implement the feature more consistently, with stronger repo alignment and better reuse.
