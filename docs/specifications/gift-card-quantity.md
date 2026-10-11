---
spec_id: SPEC-2026-10-10-gift-card-quantity
title: Gift-card checkout drafts and funded purchase quantity
status: in_progress
size: large
priority: high
created: 2026-10-10
last_updated: 2026-10-10
repository: aonik
branch: codex/gifting-quantity
target_paths:
  - src/Aonik.Commerce
  - src/Aonik.Finance
  - src/Aonik.SharedKernel/Abstractions/GiftCards
  - src/Aonik.Infrastructure/Migrations
  - tests/Aonik.Application.Tests
---

# Summary

Support the approved Abby’s Table gift checkout quantity of 1–10. One cash-funded order can issue multiple separate gift instruments, each with its own order item, ledger journal and delivery record. Partial checkout drafts remain unpriced until the purchase is explicitly prepared. The browser never receives guest confirmation credentials from ordinary payment polling.

# Context

The previous public checkout accepted one issued card per cart. Abby’s Table #26/#27 require the approved quantity control rather than a reduced launch design. The user explicitly approved extending the backend on 10 October 2026. Work starts from master 6c02fcd11 in an isolated worktree, preserving unrelated local edits.

# Scope

- `+` Quantity in gift purchase and advertised capabilities; partial gift drafts; email-only purchaser.
- `+` Separate order lines, instruments, issuance proofs and delivery records; replay protection.
- `+` Card-specific refund selection and paid confirmation-proof recovery.
- `-` Activating tenant policies, applying migrations, taking live payments or deploying.
- `-` Bulk refund orchestration or changing the identity/email-change contract.

# Constraints And Guardrails

The ledger remains authoritative. Capture and original cash funding must be proven before issuance. Value is excluded from taxable sales and gift-funded spend does not earn points again. Single-card snapshots and their existing journal keys remain readable. Migration files are generated with EF tooling, not authored manually. No production database is changed.

# Current State

GiftCardPurchasePricing freezes Commerce order lines and delivery details. GiftCardService issues Finance instruments after capture. Uniqueness previously used cart/payment intent alone. Checkout JSON is the existing durable draft location.

# Approach

Expand the gift value into quantity-one order items at checkout. Preserve the first purchase in the existing shared contract and append optional additional purchases. Use card IDs as batch issuance journal keys while retaining the legacy payment-intent key for single cards. Broaden four unique indexes to include the order item or card. Snapshot the physical greeting card on the first card only, matching the one-envelope fee.

# Requirements

- [x] RQ1: Quantity 1–10 creates that many distinct instruments and delivery records, with one postage/greeting fee. Verify: GiftCardCartTests quantity scenarios; GiftCardServiceTests.Quantity.
- [x] RQ2: Capture replay cannot duplicate value or deliveries, and balances read from the correct issuance ledger proof. Verify: quantity/capture/replay/balance scenarios.
- [x] RQ3: Staff refunds identify the selected card by its order item. An ambiguous amount-only multi-card instruction is rejected. Verify: non-first-card reclaim, replay and unaffected sibling-balance scenarios.
- [x] RQ4: Partial gift drafts retain removed and incomplete fields without issuing value. Email-only purchasers are allowed while post recipients still require name, address and courier phone. Verify: draft and existing checkout validation suites.
- [x] RQ5: Read-only confirmation recovery is owner scoped and returns guest proof only after captured success. Ordinary payment polling omits that proof. Verify: CapturedGuestProof_IsReadOnlyOwnerScoped_AndAbsentFromOrdinaryPaymentPolling.

# Tasks

- [x] Implement contracts, checkout expansion and funding/delivery processing.
- [x] Generate GiftCardPurchaseQuantity EF migration and inspect its four index changes.
- [x] Run application tests and API build.
- [ ] Review, merge and apply migration through the normal release process.
- [ ] Validate configured live Stripe/order-email/posting journeys in a non-production environment.

# Decision Log

D1: User approved 1–10 rather than a one-card release limit.
D2: Storefront defaults follow the approved £50/£75/£100/£150, £1–£999 custom, £3.95 postage and £3 card. Authoritative live amounts remain tenant configuration; tests use their own policy fixture amounts.
D3: Existing refund processing holds one reclaimed gift card per refund. Multi-card purchases support selecting one card per staff refund; multiple cards require sequential requests. Requests selecting more than one card fail clearly rather than reclaiming the wrong instrument.
D4: Validity/never-expires and scheduled email send time remain release decisions. This PR does not invent or activate them.
D5: Rolling back the generated index migration after multi-card data exists requires reconciling that data first; old one-card uniqueness cannot accept it.

# Verification

`dotnet test tests/Aonik.Application.Tests/Aonik.Application.Tests.csproj --no-restore`: 3,331 passed, 2 existing skips, 0 failed.
`dotnet build src/Aonik.Api/Aonik.Api.csproj --no-restore`: 0 errors. Existing ImageSharp vulnerability warnings remain outside this change.

# Done

Implementation and automated checks complete. Review, migration deployment and live fulfillment verification remain outstanding; this document does not close storefront issues on unmerged main.
