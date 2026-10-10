---
spec_id: SPEC-2026-10-10-account-reorder-selection
title: Owned purchase preview and selected-dish reorder
status: in-review
created: 2026-10-10
last_updated: 2026-10-10
repository: aonik
branch: codex/account-reorder
---

# Owned purchase preview and selected-dish reorder

## Why
The approved Abby’s Table My Account design opens a per-dish Order Again sheet before creating a fresh box. The existing endpoint copied every purchased dish and accepted no subset or quantity choice.

## What changes
Add a read-only, no-store authenticated reorder-preview endpoint. Extend the existing reorder POST with optional `selections: [{selectionId, quantity}]`; an empty request retains whole-box behavior. Both paths use the same owner/tenant/captured-food-order validation and serialized current-catalogue rebuild.

## Requirements
- The service SHALL expose only purchased rows from the current party’s paid food box; foreign or unknown orders SHALL be indistinguishable.
- Preview SHALL create no cart, stock hold or delivery reservation. Availability SHALL reflect current variant, product, slot membership and inventory, capped at 99 for the UI.
- Selected creation SHALL reject empty, duplicate, foreign, nonpositive or over-99 choices before creating any cart. It SHALL copy only requested rows, re-normalize their purchased options and reprice using today’s catalogue.
- The new box SHALL be at least six spaces, or the chosen unit count when greater; the tenant’s size plan SHALL validate it. Old delivery, gifts, codes, checkout and payment state SHALL never be copied.
- An existing active box SHALL remain protected. Creation SHALL recheck catalogue and aggregate stock within the existing serialized transaction and return identifiable drift notices.

## Design
Reuse `OrderReorderService` and `BoxCartService.CreateFromOrderAsync`. The frontend receives immutable purchased selection IDs, not an unrestricted product/option seed list. No migration, ledger, payment or operational policy changes.

## Tasks
- [x] Service contracts, owner-scoped preview and optional selected reorder.
- [x] Service scenarios for subset/quantity, rejected choices and read-only preview.
- [x] API integration tests and full relevant suites.
- [ ] Reviewer sign-off and release configuration.


## Validation
3,339 application tests pass with two existing skips. All 33 CommerceBoxCartEndpointTests (including selected reorder) pass. API build succeeds with existing package advisory warnings. Invalid choices include null entries; no cart is created. No migration or operational policy change is needed. This change is stacked on gift-card quantity PR #391. Independent review and authenticated storefront interaction checks remain.
