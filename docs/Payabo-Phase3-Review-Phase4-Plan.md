# Payabo Phase 3 Review and Phase 4 Delivery Plan

## Purpose

This document reviews implementation status for **Phase 3** from `docs/Payabo-MVP-Next-Steps.md` and defines an execution-ready **Phase 4** plan focused on MVP completion and production hardening.

---

## Phase 3 implementation review

## Summary verdict

Phase 3 is **partially implemented**:

- ✅ Dashboard top panels now load from API-backed summary data via a dedicated data hook.
- ✅ Optional non-MVP dashboard sections are gated behind a feature flag.
- ⚠️ Transaction history consistency is incomplete (`/transactions` still uses local storage history, not API-backed order/payment history).
- ⚠️ Frontend quality gates exist, but are still lightweight smoke checks rather than full lint + typed component/integration test coverage.

As a result, Phase 3 acceptance criteria are **mostly met for dashboard migration**, but **not fully met for quality enforcement and history consistency**.

### 6) Replace dashboard mock panels with minimal live datasets

**Status: Implemented with one consistency gap**

Implemented evidence:

- Dashboard data contract added with `getDashboardSummary(userId)` calling `/public/dashboard/summary`.
- `useDashboardData` hook manages loading/error/refresh lifecycle and stale-request protection.
- `Dashboard.tsx` now renders upcoming bills and recent transactions from live hook data.
- Optional dashboard panels are explicitly controlled by `VITE_PAYABO_SHOW_OPTIONAL_DASHBOARD_PANELS`.

Remaining gap:

- Dashboard transaction list deep-links to details, but `/transactions` page still reads `localStorage` (`payabo.payment-history`) rather than the same API-backed history source.

### 7) Stabilize frontend quality gates

**Status: Partially implemented**

Implemented evidence:

- `package.json` includes `lint`, `typecheck`, and `test` scripts.
- A smoke script validates route and API-integration guardrails for core bill-pay + dashboard wiring.

Remaining gaps:

- `lint` and `test` currently route to the same smoke script (no real lint engine or component test suite).
- No `vitest` + Testing Library setup yet.
- CI enforcement for Payabo `build` + `typecheck` + `test` is not documented in this phase artifact.

---

## Phase 4 plan (MVP completion + release hardening)

Phase 4 goal: **close remaining MVP-blocking gaps and harden Payabo for predictable pre-release operations** while preserving AONIK guardrails (Order as intent, payment execution/status separation, auditable IDs-first flow).

## Workstream A: Transaction history source-of-truth alignment

### Scope

- Remove divergence between dashboard “recent transactions” and the `/transactions` experience.
- Make transaction list/detail consistently API-backed using order/payment references.

### Implementation slices

1. **History API contract consolidation**
   - Add a typed API layer for transaction list/detail from order/payment timeline endpoints.
   - Keep status mapping deterministic (`pending`, `succeeded`, `failed`) with backend identifiers (orderId, paymentIntentId, providerReference).

2. **Transactions page migration**
   - Refactor `Dashboard/Transactions.tsx` to use API history instead of `paymentHistory.ts` local storage.
   - Preserve clear loading, empty, and retry states.

3. **Transaction details parity**
   - Ensure detail page payload includes support/audit fields needed by customer support.
   - Keep display references ID-based and avoid unnecessary PII duplication.

4. **Legacy fallback removal**
   - Retire or strictly scope `paymentHistory.ts` to non-production dev fixtures.

### Acceptance criteria

- A completed checkout appears consistently in dashboard recent transactions, `/transactions`, and transaction details.
- History survives browser refresh/device switch via backend state, not local storage.

---

## Workstream B: Real quality gates and testability baseline

### Scope

- Replace placeholder quality scripts with enforceable lint/type/test checks.

### Implementation slices

1. **Lint stack**
   - Introduce ESLint (TS + React + hooks) and wire `npm run lint` to real static analysis.

2. **Test stack**
   - Add Vitest + Testing Library + jsdom setup.
   - Keep existing smoke script as a fast preflight gate, but run it separately (`test:smoke`).

3. **Core regression tests**
   - Add focused tests for:
     - Auth guard redirect behavior.
     - Provider/service -> draft order handoff.
     - Payment selection -> intent creation call contract.
     - Dashboard and transactions rendering for loading/empty/data/error states.

4. **CI wiring**
   - Ensure CI executes `build`, `typecheck`, `lint`, `test`, and optional `test:smoke` for Payabo.

### Acceptance criteria

- Pull requests fail fast on route/type/lint/test regressions.
- Core customer payment journey has automated coverage beyond string-based smoke checks.

---

## Workstream C: Checkout and status resilience hardening

### Scope

- Improve reliability in eventual-consistency windows after payment execution.

### Implementation slices

1. **Status polling policy**
   - Add bounded polling + explicit “refresh status” UX on payment return/status pages.

2. **Error taxonomy and UX**
   - Distinguish retriable transport errors from terminal payment failures.

3. **Observability hooks**
   - Emit client telemetry keyed by IDs (orderId/paymentIntentId/AiRunId where relevant), not raw PII.

### Acceptance criteria

- Users receive deterministic status outcomes with clear retry options.
- Support can correlate user-reported payment states with backend references.

---

## Workstream D: Release readiness checklist for MVP launch

### Scope

- Prepare Payabo for controlled MVP release.

### Implementation slices

1. **Feature-flag manifest**
   - Document defaults for optional dashboard panels and non-MVP surfaces.

2. **Operational runbook**
   - Add troubleshooting flow for failed/pending payment statuses and profile/auth issues.

3. **Go-live quality bar**
   - Define pass/fail criteria for auth flows, checkout flow, transaction history, and status determinism.

### Acceptance criteria

- MVP release decision is based on explicit checklist evidence, not ad hoc manual validation.

---

## Proposed sprint sequence (Phase 4)

1. **Sprint P4-A1**: Transaction API consolidation + `/transactions` migration.
2. **Sprint P4-A2**: Transaction detail parity + legacy local-storage fallback removal.
3. **Sprint P4-B1**: ESLint + Vitest setup + first regression tests.
4. **Sprint P4-B2**: CI gate enforcement + flaky test stabilization.
5. **Sprint P4-C1**: Checkout/status resilience hardening and observability.
6. **Sprint P4-D1**: Release runbook + feature-flag manifest + go-live checklist.

---

## Exit criteria for Phase 4 completion

Phase 4 is complete when:

- Transaction history and detail are fully API-backed and consistent across dashboard + transactions surfaces.
- Payabo quality gates are enforceable (`build`, `typecheck`, `lint`, `test`) and running in CI.
- Payment status experience is deterministic under real-world eventual consistency conditions.
- MVP release checklist and operational runbook are documented and validated.
