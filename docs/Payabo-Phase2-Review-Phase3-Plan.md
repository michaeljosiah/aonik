# Payabo Phase 2 Review and Phase 3 Delivery Plan

## Purpose

This document reviews implementation status for **Phase 2** from `docs/Payabo-MVP-Next-Steps.md` and provides an execution plan for **Phase 3**.

---

## Phase 2 implementation review

## Summary verdict

Phase 2 is **substantially implemented**, with a few hardening items remaining:

- ✅ Real auth API integration is in place (token + userinfo + registration).
- ✅ Route protection is wired through real auth state (`isLoading` + `isAuthenticated`).
- ✅ Profile pages are connected to backend profile endpoints (view/update/email/password/photo).
- ⚠️ Session hardening can be improved (refresh/logout endpoint handling and secure token storage strategy).

### 4) Integrate real identity/auth session (remove mock auth)

**Status: Implemented (baseline complete)**

Implemented evidence:

- Auth client now calls backend endpoints (`/auth/token`, `/v1/registrations/individual`, `/identity/userinfo`).
- `AuthContext` bootstraps user session from token + `userinfo`, manages login/register/logout, and handles 401 bootstrap failures.
- `RequireAuth` blocks protected routes until loading completes and redirects unauthenticated users to `/login`.

Remaining hardening:

- Add token refresh flow and single-flight refresh protection.
- Consider migrating from localStorage bearer token persistence to more secure session/cookie approach where backend supports it.
- Add server-backed logout endpoint call when available.

### 5) Wire profile pages to existing customer profile endpoints

**Status: Implemented (baseline complete)**

Implemented evidence:

- Profile API wrappers support get/update profile, email change, password change, and photo upload/delete.
- Profile screens call backend APIs and provide basic success/error messaging.

Remaining hardening:

- Add stricter client-side validation and consistent field-level error mapping.
- Add optimistic UX only where safe and include retry affordances for transient failures.
- Add broader integration tests around profile update scenarios.

---

## Phase 3 execution plan

Phase 3 objective from the MVP roadmap: **convert dashboard demo surfaces into useful live personal-finance shells and add frontend quality gates**.

## Workstream A: Live dashboard datasets

### Scope

- Replace static dashboard bill and transaction panels with real API-backed user data.
- Keep non-MVP widgets optional behind feature flags.

### Implementation slices

1. **Dashboard data contracts**
   - Add `Payabo/src/api/dashboard.ts` with typed methods:
     - `getUpcomingBills()` (from order drafts/scheduled bills source)
     - `getRecentTransactions()` (from order/payment history source)
   - Reuse shared API client and error envelope parser.

2. **Data hook layer**
   - Add `useDashboardData` hook for fetch lifecycle (`loading`, `error`, `refresh`, `data`).
   - Include cancellation support to avoid stale state updates.

3. **Dashboard page migration**
   - Replace `upcomingBills` and `recentTransactions` fixtures in `Dashboard.tsx` with hook data.
   - Maintain deterministic empty/loading/error states.

4. **History consistency pass**
   - Align dashboard recent transactions with `/transactions` list and transaction details pages so users see consistent statuses and identifiers.

5. **Feature flag optional panels**
   - Hide or clearly label non-MVP widgets (news/org cards/budget extras) behind a simple frontend feature toggle.

### Acceptance criteria

- Dashboard top panels render user-specific live data.
- New completed checkout is visible in both dashboard recent transactions and transactions page.
- Empty states are clear and actionable.

## Workstream B: Frontend quality gates

### Scope

- Introduce repeatable quality checks to reduce regressions for payment and account journeys.

### Implementation slices

1. **Package scripts**
   - Add scripts in `Payabo/package.json`:
     - `lint`
     - `typecheck`
     - `test`
     - `test:watch` (optional local workflow)

2. **Test stack**
   - Add `vitest` + `@testing-library/react` + `@testing-library/jest-dom`.
   - Add `jsdom` environment and test setup file.

3. **Core smoke tests**
   - Add journey smoke tests for:
     - Auth gate redirect behavior.
     - Provider/service selection to draft creation handoff.
     - Payment selection to checkout intent creation request.

4. **CI integration**
   - Ensure CI executes `build`, `typecheck`, and `test` for Payabo app.
   - Fail fast on type errors or route regressions.

### Acceptance criteria

- CI enforces type and smoke-test gates for Payabo.
- Core bill-pay flow regressions are caught pre-merge.

---

## Proposed sprint sequence (Phase 3)

1. **Sprint P3-A1**: Dashboard API contracts + `useDashboardData` hook + initial panel migration.
2. **Sprint P3-A2**: Transactions consistency + optional panel flags + UX polish.
3. **Sprint P3-B1**: Test tooling setup + lint/typecheck/test scripts + initial smoke tests.
4. **Sprint P3-B2**: CI wiring + flaky test hardening + release readiness pass.

---

## Exit criteria

Phase 3 is complete when:

- Dashboard primary panels are API-backed and user-specific.
- Transaction summaries are consistent between dashboard and detail views.
- Payabo frontend has enforceable quality gates in CI (`build`, `typecheck`, `test`).
- MVP non-core widgets are gated or clearly non-blocking.
