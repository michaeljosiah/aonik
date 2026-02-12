# Payabo Phase 1 Review and Phase 2 Delivery Plan

## Purpose

This document reviews implementation status for **Phase 1** from `docs/Payabo-MVP-Next-Steps.md` and defines an execution-ready **Phase 2** plan.

---

## Phase 1 implementation review

## Summary verdict

Phase 1 is **partially implemented**:

- ✅ **Status handling foundations are in place** (payment return and state-aware status page).
- ⚠️ **Saved card/instrument integration remains static demo data**.
- ⚠️ **Transaction history/details remain placeholders**.

As a result, Phase 1 acceptance criteria are **not yet fully met** end-to-end.

### 1) Complete order-to-payment status handling in Payabo UI

**Status: Mostly implemented**

What is implemented:

- Payment return route exists and normalizes provider return outcomes before redirecting to the status surface.
- Status page fetches payment intent status and related draft order data via public APIs.
- Status page maps backend status values into deterministic UI states (`success`, `pending`, `failed`).
- Dedicated status routes (`status bill paid`, `status failed`, `status order received`) all converge on the same stateful status component.

Remaining gaps to close:

- Add polling/retry UX for eventual consistency windows so users can re-check status without manual full-page refresh.
- Ensure provider-return query handling covers all partner variants and malformed values consistently.

### 2) Replace static saved-card UX with tenant/user-backed instruments

**Status: Not implemented**

What is currently true:

- `SelectCard` and `CardCheckout` still depend on hard-coded in-file `savedCards` arrays.
- There is no live instrument fetch path and no per-user persisted instrument list in the flow.

Impact:

- Returning user card selection does not represent real account state.
- MVP acceptance criterion for selecting an actual saved instrument is not satisfied.

### 3) Implement transaction history and details with real data

**Status: Not implemented**

What is currently true:

- Dashboard transactions page remains a placeholder.
- Transaction details page remains a placeholder.

Impact:

- End users cannot validate completed checkouts in a first-class history UI.
- Support/audit visibility in-app is incomplete.

---

## Phase 2 plan (Authenticated account surfaces)

Phase 2 from the MVP roadmap focuses on:

1. Real identity/auth session.
2. Real profile surfaces.

The plan below is scoped to preserve AONIK guardrails: clear separation of order intent, payment execution, and auditable state.

## Workstream A: Real identity and session integration

### Scope

- Replace `payabo.mockAuth` local storage auth with real identity integration.
- Route guards must rely on token/session validity, not local mock state.

### Implementation slices

1. **Auth API client and contracts**
   - Add `Payabo/src/api/auth.ts` with `login`, `register`, `refresh`, `logout`, `me`.
   - Define typed request/response DTOs with minimal PII in client state.

2. **Session store replacement**
   - Refactor `AuthContext` to use secure token/session lifecycle.
   - Keep in-memory access token and refresh through HTTP-only cookie-backed session where available.
   - Add bootstrap `me` check on app load for durable auth state.

3. **Route guard hardening**
   - Update `RequireAuth` to check resolved auth status (`loading`, `authenticated`, `unauthenticated`).
   - Add redirect with post-login return path.

4. **Auth page wiring**
   - Connect `Login` and `Register` forms to backend endpoints.
   - Implement form validation and backend error mapping.

5. **Logout/session expiry**
   - Server-aware logout.
   - Graceful forced sign-out on refresh failure or 401 loops.

### Acceptance criteria

- Login/register/logout represent backend truth.
- Protected routes are inaccessible without valid session.
- Browser reload restores auth status via backend/session bootstrap.

---

## Workstream B: Profile endpoint integration

### Scope

- Replace profile placeholders with real forms for personal and login details.
- Support get/update profile, email change, password change, and photo upload/delete.

### Implementation slices

1. **Profile API client**
   - Add `Payabo/src/api/profile.ts` wrappers for existing public/customer profile endpoints.

2. **Profile state model**
   - Introduce `useProfile` hook (fetch, mutate, pending, error).
   - Keep API IDs/references as canonical; avoid unnecessary raw PII duplication.

3. **Page conversion order**
   - `PersonalDetails` + edit name/country/phone pages first.
   - `LoginDetailsEmail` and `LoginDetailsPassword` next.
   - `PersonalDetailsUpdatePhoto` upload/delete flow last.

4. **UX quality and resilience**
   - Client validation + server validation surfaces.
   - Optimistic updates only where safe and reversible.
   - Explicit success/error toasts and inline field messages.

### Acceptance criteria

- Profile changes persist server-side and rehydrate after refresh.
- Password and email changes enforce backend validation rules.
- Photo upload/delete reflects current backend state.

---

## Dependencies and sequencing

### Recommended sprint sequence

1. **Sprint P2-A1**: Auth client + AuthContext/session refactor + route guarding.
2. **Sprint P2-A2**: Login/register/logout UI integration + error handling.
3. **Sprint P2-B1**: Profile API client + personal details pages.
4. **Sprint P2-B2**: Login details + photo upload/delete + regression pass.

### Cross-cutting technical requirements

- Standardize API error envelope handling in Payabo API client.
- Add request cancellation support for profile/auth mutations to avoid stale updates.
- Add telemetry around auth failures and profile mutation outcomes (ID-based, minimal PII).

---

## Risks and mitigations

- **Risk:** Incomplete identity endpoint parity for frontend needs.
  - **Mitigation:** Contract review before coding and stub fallback for non-blocking fields.

- **Risk:** Session expiration edge cases create redirect loops.
  - **Mitigation:** Single-flight refresh guard + max retry policy + hard logout fallback.

- **Risk:** Profile pages diverge by endpoint capability.
  - **Mitigation:** Capability-driven UI sections (feature flags / endpoint probes).

---

## Exit criteria to start Phase 3

Phase 2 can be marked complete when:

- Auth and route protection are fully backend-driven.
- Profile core surfaces are fully wired and persistent.
- Core happy paths and key failures are validated in smoke checks.
- Remaining Phase 1 gaps (saved instruments + transaction history/details) are either closed or explicitly queued with target sprint commitments.
