# API Swap Checklist (Mock -> Live)

## Goal
Replace mock repositories with live API-backed implementations without changing flow semantics (order draft first, payment intent second, status polling afterward).

## Repository Wiring
- [ ] Add `ApiCatalogRepository` and switch `catalogRepositoryProvider` by environment flag.
- [ ] Add `ApiOrderRepository` and keep order draft creation semantics intact.
- [ ] Add `ApiPaymentRepository` and keep payment intent + polling contract separate from order draft.
- [ ] Add `ApiDashboardRepository` and map all dashboard DTO fields used by UI.
- [ ] Add `ApiProfileRepository` for profile update and read operations.

## Contracts and Mapping
- [ ] Map API errors to user-safe messages used in UI.
- [ ] Keep IDs (`orderId`, `paymentIntentId`, `providerReference`) as first-class fields in app state.
- [ ] Verify request/response compatibility against `apps/Payabo/src/api/*.ts` references.
- [ ] Ensure null/optional API fields are handled defensively in DTO mappers.

## Auth and Security
- [ ] Replace mocked auth state with token/session based auth provider.
- [ ] Store auth token securely (`flutter_secure_storage`) and load on app startup.
- [ ] Add logout token/session clear behavior.

## Flow Safety
- [ ] Preserve sequence guard: service details -> payment selection -> checkout.
- [ ] Preserve payment status refresh/retry behavior with live status endpoint.
- [ ] Keep local draft recovery behavior aligned with backend source-of-truth updates.

## QA Before Enablement
- [ ] Run full widget + golden + integration test suite in mock mode.
- [ ] Run full integration suite in live/staging mode.
- [ ] Validate parity for both card and friend-help flows against expected statuses.
- [ ] Validate graceful degradation on API timeout, 4xx, and 5xx responses.
