# Payabo MVP Next Steps

## Why this review exists

Payabo already has a strong UI migration baseline and partial public API integration for the bill payment flow. This document defines the highest-impact next work to move from "prototype" to a working MVP.

## Current State Snapshot

### What is already in place

- Payabo has a dedicated React/Vite app hosted through the Aonik app host.
- Public catalog endpoints are wired into provider and service discovery.
- Bill-payment draft orders are created and retrieved through `/public/orders/bill-payments/drafts`.
- Public payment intents are created through `/public/payments/intents`.

### What is still MVP-blocking

- Many customer-facing pages are still placeholder screens (dashboard subpages, payment status/confirmation pages, and profile pages).
- Auth is local-storage mock auth, not real identity integration.
- Dashboard content is still mock-data driven and disconnected from APIs.
- Card management and friend-payment checkout use static demo data, not persisted user instruments or recipient records.
- No explicit post-checkout status reconciliation path in the Payabo UI flow.
- No frontend test harness or quality gate in `package.json` (build only).

## MVP Definition (Payabo)

A Payabo MVP should allow a customer to:

1. Sign in using real identity.
2. Select country, provider, and service from live catalog.
3. Validate service fields and create an order draft (business intent).
4. Fund the order through a payment intent.
5. See deterministic status screens from pending -> success/failure.
6. View basic transaction history and the transaction detail page for that order.
7. Manage profile basics (name, email, phone, password, photo).

## Recommended Work Plan

## Phase 1 — Close the core payment journey gaps (highest priority)

### 1) Complete order-to-payment status handling in Payabo UI

**Why now**: The app can create drafts and payment intents, but MVP trust depends on showing customers reliable final status.

**Tasks**

- Add a payment return handler page that reads provider return signals and confirms order/payment state from API.
- Replace placeholder status pages with stateful pages sourced from order/payment APIs.
- Add robust retry/refresh behavior for eventual consistency windows.

**MVP acceptance**

- From `CardCheckout`, user always lands on success/failed/pending screens backed by real order/payment state.
- Refreshing the status page remains deterministic.

### 2) Replace static saved-card UX with tenant/user-backed instruments

**Why now**: Checkout currently uses hard-coded cards, which blocks real repeat usage.

**Tasks**

- Introduce a public-safe payment instrument read API (masked details only) for authenticated users.
- Wire `SelectCard` and `CardCheckout` to load saved instruments for current user.
- Keep "pay with new card" path explicit when no instruments exist.

**MVP acceptance**

- Returning users can select an actual saved payment instrument.
- New users can still pay without pre-existing instruments.

### 3) Implement transaction history and details with real data

**Why now**: Payment confidence requires auditable history.

**Tasks**

- Replace dashboard transactions placeholder and static rows with order/payment timeline query.
- Implement transaction details page using order + payment + ledger-linked references.

**MVP acceptance**

- Completed checkout appears in transaction history.
- Transaction details show identifiers and statuses needed for support/audit.

## Phase 2 — Make authenticated account surfaces production-credible

### 4) Integrate real identity/auth session (remove mock auth)

**Why now**: `localStorage` auth is not secure and blocks realistic user flows.

**Tasks**

- Connect Payabo auth pages to identity endpoints and token/session handling.
- Replace `payabo.mockAuth` storage with secure session/token strategy.
- Ensure route guarding uses real auth state and expiration handling.

**MVP acceptance**

- Login/register/logout represent real backend identity state.
- Protected routes are inaccessible without valid session.

### 5) Wire profile pages to existing customer profile endpoints

**Why now**: Profile endpoints already exist in API; wiring them unlocks user self-service quickly.

**Tasks**

- Replace profile placeholders with forms for get/update profile, email, password, photo upload/delete.
- Add validation, optimistic UX, and error states.

**MVP acceptance**

- Profile edits persist and rehydrate correctly after refresh.

## Phase 3 — Convert demo dashboard into useful personal-finance MVP shell

### 6) Replace dashboard mock panels with minimal live datasets

**Why now**: Dashboard is currently mostly static and can mislead users.

**Tasks**

- Fetch upcoming bills from user order schedule/drafts where applicable.
- Pull recent transactions from order/payment list API.
- Keep non-MVP sections (news/org cards) clearly marked optional or hidden by feature flag.

**MVP acceptance**

- Dashboard top panels reflect actual user activity, not static fixtures.

### 7) Stabilize frontend quality gates

**Why now**: MVP velocity improves with fast regression feedback.

**Tasks**

- Add scripts for lint + typecheck + test (`vitest` or equivalent).
- Add a basic smoke test for core journey: provider -> service -> payment selection -> checkout intent creation.

**MVP acceptance**

- CI can fail fast on broken routes/types/core flow regressions.

## Architectural guardrails for implementation

- Preserve **Order as intent**: Payabo UI should always create/track order first, then payment execution.
- Keep **payment execution separate from ledger truth**: status screens should consume order/payment state and expose references; ledger posting remains backend concern.
- Use **IDs over raw PII** in UI state and telemetry.
- Keep AI features optional at MVP stage unless they follow proposal/approval/application patterns for financially material actions.

## Suggested sequence (2-4 sprints)

1. **Sprint A**: Payment status end-to-end + replace status placeholders.
2. **Sprint B**: Real instruments in checkout + transaction list/detail.
3. **Sprint C**: Real auth integration + profile pages.
4. **Sprint D**: Dashboard live data + frontend quality gates.

## Out of MVP scope (for now)

- Advanced budgeting/goals intelligence surfaces.
- Agent-driven autonomous financial actions.
- Deep household collaboration workflows beyond minimal profile/account functionality.

These can follow once the core bill-pay intent -> funding -> status -> history loop is stable.
