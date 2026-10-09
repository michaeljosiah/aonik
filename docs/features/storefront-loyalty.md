# Storefront loyalty points

Issue #363 implements the final Abby's Table loyalty rules over the existing Finance journals, checkout attempts and verified guest-account access flow. It adds a price benefit, not another payment method. Gift-card tender is a separate feature.

## Calculation

- Earn 2 points per GBP 1 of eligible spending after coupons and redeemed points. Delivery, postage and greeting-card charges earn no points. Gift-card value purchases may earn; spending gift-card-funded value must not earn again.
- 100 points reduce the price by GBP 1. Customers choose whole points; there is no minimum and no expiry.
- The redemption ceiling is 20% of the order after vouchers and before points, including delivery and tax in that ceiling. Eligible item value and the available positive balance can lower it further. Points cannot reduce ineligible fees or gift-card value.
- Earned points are rounded down once for the order. That integer total is allocated across eligible lines by largest remainder, with item index breaking ties. The saved allocations make later reversals exact. This documents the rounding convention where the design does not specify one.
- The existing discount allocation helper divides the monetary price benefit across eligible goods. Tax is then recalculated from the reduced taxable amount. Coupon and points reductions remain separate in the order, invoice and API.
- Requests above the available maximum return a quote reason. Checkout rejects them rather than silently choosing another amount. Redeeming requires acceptance of the current payable total. Stripe's existing GBP 0.30 minimum still applies.

Both cart quotes and checkout use the same calculator. Reading a quote or saving `requestedPoints` in the checkout draft does not reserve or spend points. Product and coupon exclusions can be configured separately for earning and redemption; a displayed saving or a dated price is not treated as promotion provenance.

## Tenant activation and accounting

Loyalty is disabled unless the tenant has an enabled `Commerce.Storefront.Loyalty` setting. This change does not activate that setting or create ledger accounts. Configure these fields through the existing tenant settings administration:

| Field | Meaning |
| --- | --- |
| `enabled` | Explicit activation |
| `version` | Operator's policy version label |
| `ledger.ledgerId` | Existing tenant GBP ledger |
| `ledger.liabilityAccountId` | Existing Liability account for nominal point obligations |
| `ledger.earnExpenseAccountId` | Existing Expense account debited when points are earned |
| `ledger.redeemExpenseAccountId` | Existing Expense account credited when the price benefit releases the obligation |
| `earnExcludedProductIds`, `redeemExcludedProductIds` | Optional product exclusion lists |
| `earnExcludedDiscountIds`, `redeemExcludedDiscountIds` | Optional coupon exclusion lists |

The enabled policy uses nominal GBP 0.01 per point: earning debits the configured expense and credits the reward liability; redemption debits that liability and credits its configured expense offset. The sale and card payment stay at the net price. Tenant finance owners must map accounts appropriate to this treatment before activation; this is not a universal revenue or tax policy. Wrong-tenant, missing, non-GBP or incompatible accounts fail closed. No account codes or currencies are invented.

The effective policy version fingerprints the validated settings, including exclusions. An edit cannot silently reuse a quote by keeping the same display version. Accepted attempts retain their original ledger/account references. Changing settings does not move existing liabilities or hide historical balances.

## Payment and ledger lifecycle

Finance creates the payment intent and point reservation in one database transaction before contacting Stripe. An account row supplies concurrency control, not a balance column. Two carts cannot spend the same available points. A known policy/balance rejection creates a cancelled attempt that cannot be revived by retry; transient failures are not treated as cancellation.

Capture posts redemption and earning in the same Finance transaction as the payment receipt and completion event. Unknown provider outcomes keep their reservations. Confirmed cancellation and the existing provider-start deadline release them. There is no separate loyalty expiry job.

Balances sum actual posted liability journal lines linked to immutable loyalty operations. Operations carry source identities and original line allocations for audit and idempotency. Negative balances are allowed after reversals; available redemption is never negative. Operator corrections post balanced entries and require `Ledger.Write`, a reason and an idempotent adjustment ID.

Refund reversal/restoration is an internal primitive with original-order, line and cumulative-point bounds. Issue #365 supplies real provider refund orchestration and derives the quantities from the actual refund. This issue does not expose a pretend refund action or change a payment to refunded without a provider result.

## Customer and guest access

The authenticated customer API resolves the current party on the server:

- `GET /commerce/storefront/loyalty`: signed balance, reserved and available points, monetary value and the display marker.
- `GET /commerce/storefront/loyalty/history?page=1&pageSize=20`: paged activity with running balances.
- `POST /commerce/storefront/loyalty/seen`: explicitly acknowledge the highest GBP 5 step seen. The marker is monotonic and independent of the financial balance; GET does not update it.
- `POST /commerce/admin/loyalty/adjustments`: permission-gated operator correction.

These responses are private and non-cacheable. Customer bodies do not select another party. The existing cart draft accepts `requestedPoints`; cart and box quotes expose a nested loyalty quote and a separate `pointsAppliedValue`.

Guest earning is attached to the paid order immediately. An accountless guest is not told points have been added to an account. Optional account setup uses the existing purpose-bound paid-access event. Commerce verifies and links that source first; Finance then transfers the original award through balanced journal entries. A retried event or another access action cannot claim the same award twice or move it to another owner. Email equality, cart tokens and guest order-read tokens cannot claim points.

Order responses distinguish unpaid estimates from posted earning and account-setup visibility. The Admin order charge view and margin report include the points reduction. Receipt models expose separate coupon and points values; `discount_total` includes both for existing customised templates that still have one discount row. Fresh default templates show them separately. Existing tenant templates are not overwritten.

## Integration boundaries

No new wallet, pricing framework, scheduler, payment provider or account-signup flow is introduced. The schema adds loyalty ownership, immutable operation references and attempt reservations, plus the Commerce charge snapshot. Only the canonical `AonikDbContext` migration stream applies it.

The separate Abby's Table storefront still needs to wire its points controls and account display to these APIs. Operational ledger configuration and frontend deployment are not implied by this repository merge.
