# Storefront gift cards

Issue #364 adds gift-card purchase, delivery and payment to existing Commerce checkout and Finance journals. The final `design/abbys-table-page-behaviour-guide.md` governs the points rule: eligible new-money gift-card purchases earn points; gift-funded spending does not earn them again. This supersedes the older issue's blanket points exclusion. Coupons and points cannot reduce gift-card face value.

## Scope and configuration

The initial scope is one purchased gift card per cart and one gift-card tender per checkout. Face value equals its charged value. A cart purchasing a gift card cannot use gift-card tender. There is no discounted-face campaign engine, new wallet, payment provider or scheduler.

Purchasing is disabled until both tenant settings are explicitly configured through existing settings administration:

| Setting | Required configuration |
| --- | --- |
| `Finance.GiftCards.Policy` | `enabled`, `version`, `currency: "GBP"`, `ledger` with existing same-tenant `ledgerId`, `cashAccountId`, `clearingAccountId`, `liabilityAccountId`; `validity` with `neverExpires` or `validForDays`; `termsVersion`; `fundingAllocation: "Proportional"` |
| `Commerce.Storefront.GiftCards` | `enabled`, `version`, published non-placeholder `productVariantId`, `values` and/or `customMinimum`/`customMaximum`, allowed `deliveryMethods`, `postage`, `greetingCardPrice`, `timezone`, `emailSendTime`, `postingDays`, `taxTreatment: "ExcludeFaceValue"` |

Delivery method values are `Email`, `Post` and `InFoodBox`. Dates are bounded to the next year when selected. `postingDays` uses the .NET day-of-week numbers (Sunday 0 through Saturday 6). Postal delivery currently accepts GB addresses; manual address entry is supported without inventing an address-lookup vendor. Postage and greeting fees are configured in GBP whole pennies, independently of food-box charges. No prototype denomination, GBP 3.95 postage, validity period, accounting account or send time is activated by this change.

The supported initial tax treatment excludes gift-card face value from the existing tax calculator and leaves other charges on that calculator. This is an explicit activation choice, not a universal tax policy. A tenant must confirm that treatment and map the existing ledger accounts appropriately before enabling issuance. Unsupported or incomplete configuration fails closed.

Options return a fingerprint of the effective purchase and Finance policy. The purchase selection must include that `acceptedVersion`; later terms, validity, price or account changes require a fresh selection. An authorized cart remains readable with an unavailable purchase quote and its saved amounts so the customer can remove or replace a stale selection. New checkout revalidates delivery dates and rejects delivery beyond configured validity; prepared attempts retain their original snapshot. Accepted Finance attempts retain their original policy. Disabling new issuance does not strand the existing gift-card liability: redemption uses the instrument's original validated binding.

## Cart and checkout

- `GET /commerce/storefront/gift-cards/options` returns safe purchase options and their version.
- `PUT` / `DELETE /commerce/carts/{cartId}/gift-card-purchase` selects or removes the purchase. A selection supplies face value, method, recipient, accepted version, and relevant email/send date or postal details. `InFoodBox` requires an existing food box.
- `PUT` / `DELETE /commerce/carts/{cartId}/gift-card-tender` applies or clears a code and requested amount. Codes travel only in the request body. The cart stores a protected, tenant/instrument/cart-bound grant, never the raw code.
- `POST /commerce/storefront/gift-cards/balance` accepts a bounded code in the body and returns masked balance, reserved/available value, status and expiry.

Cart writes retain the existing guest-token/party authorization and `X-Cart-Version` precondition. Generic item routes cannot sell the configured gift variant as ordinary inventory. The semantic `GiftCardValue` line uses the existing catalogue for identity and purchased name, fills no food-box slot, and creates no stock reservation or kitchen demand. Postage and greeting charges are separate order lines. Generic checkout-draft replacement cannot change the private tender grant or gift purchase selection.

Quotes expose the requested and available gift amounts, gift/card split and a rejection reason. They do not reserve value. Checkout requires `expectedTotal` and, when using gift tender, `expectedCardAmount`. Changed balances cannot silently raise the card charge or lower the requested gift amount. A zero card remainder is supported; a positive remainder below Stripe's GBP 0.30 minimum requires a revised quote.

Cart and box quotes expose `giftCardPurchaseStatus` when a saved purchase is unavailable. Its `gift_card.purchase_unavailable` code means the customer must remove or replace the selection before checkout; the displayed face value and fees remain the saved amounts, and earning is not promised for that unavailable selection.

Points first reduce eligible prices and recalculate tax. Gift tender then funds the unchanged net sale total, including tax and fees. The configured proportional convention allocates gift funding over net charged lines and a separate tax weight using the shared four-decimal allocation helper. It is an explicit implementation convention where the design does not specify allocation order. The existing loyalty calculator consumes those shares, excluding gift-funded amounts from earning. Purchase postage and greeting fees earn no points.

Standalone email/post purchases use a validated purchaser contact in the frozen checkout snapshot without fabricating food-delivery details. The existing receipt and optional paid-account setup flows reuse that contact. Customer and staff order detail responses expose gift and card funding separately from discounts. No ordinary cart, order, receipt or list response exposes a raw gift code or private grant.

## Finance lifecycle

`PaymentIntent.Amount` remains the complete net sale funding requirement. Stripe receives only the cash remainder. A gift-only attempt uses the existing reconciler without a Stripe session or credentials. Its receipt records the `GiftCard` payment method and an exact posted liability debit, rather than a pretend card charge. Completion requires receipt totals to reconcile exactly to the intent amount.

Finance freezes gift and points reservations atomically with the payment attempt before an external request. Native instrument versions arbitrate concurrent claims. Known rejection or proven closure releases both reservations; an unknown provider outcome retains them. Replayed cancelled attempts cannot rearm a gift reservation.

Balances derive from actual posted liability journal lines referenced by immutable operations. There is no editable balance column. For a GBP 100 purchase, the receipt debits cash and credits clearing; issuance debits clearing and credits gift liability. Redeeming GBP 100 debits that liability and credits clearing. A mixed order's external receipt posts only its actual cash remainder. Invoice settlement preserves the already-issued liability by clearing the gift-purchase portion instead of recognizing it again as revenue. Food invoices funded by gift cards use that instrument's original ledger and clearing account after receipt and journal verification. Manual invoice payment cannot issue value before the matching funding/issuance exists.

Codes contain 128 random bits. Lookup uses a tenant-separated hash; the original is encrypted using the existing persistent Data Protection key ring for authorized delivery and printing. Codes confer bearer spending authority, independently of purchaser identity. Keep the shared key ring available across API/Worker instances.

## Delivery and operations

The checkout transaction stages a source-bound delivery snapshot before any payment-completion event can arrive. Finance's existing outbox publishes issuance. The Commerce handler verifies that exact instrument and source, then schedules the existing Platform task action `commerce.send_gift_card` for email, or exposes physical work for staff.

The internal scheduler accepts an optional stable task ID for an explicitly timed one-off job. It reuses the existing primary key and `StartAtUtc` as the original due time, compares immutable action bindings, and returns an already-completed or cancelled job without rearming it. Public `/tasks` requests cannot set that internal identity. No scheduler table or parallel worker is added.

- `GET /commerce/storefront/gift-cards` lists the authenticated purchaser's sent cards with masked codes and recipient emails.
- `POST /commerce/storefront/gift-cards/{deliveryId}/resend` sends the same instrument to its original email recipient. Existing tenant/IP limiting, a five-minute cooldown and a maximum of three resends per 24-hour resend window apply. Repeated in-flight requests reuse their delivery sequence.
- `GET /commerce/admin/gift-card-deliveries` lists physical delivery work.
- `POST /commerce/admin/gift-card-deliveries/{deliveryId}/print` returns a private printable payload with the same code and records the operator.
- `POST /commerce/admin/gift-card-deliveries/{deliveryId}/complete` requires the observed version and records `Posted` or `Enclosed` after printing.

Sensitive routes use `no-store` and `no-referrer`, including early error responses. Physical operations use existing staff/customer permissions. Email submission uses the existing template sender and ACS configuration; `Sent` means accepted submission, not proven inbox delivery. As with the existing email infrastructure, a crash between external submission and recording completion can cause a duplicate email, but never a second instrument or second issuance.

The separate Abby's Table frontend must wire these contracts and the operational team must configure issuance, tax, delivery and email before launch. This repository change does not deploy that frontend, activate live payment/email services, or add a new Admin workspace. Refund orchestration and original-tender restoration follow in #365; this change preserves their source facts and does not expose a pretend refund action.
