# Discount codes

Issue [#355](https://github.com/michaeljosiah/aonik/issues/355) extends the existing discount service and checkout draft. The server calculates money; clients display the returned quote and supply its total when starting a discounted checkout.

## Cart flow

- `POST /commerce/carts/{cartId}/discount/validate` accepts `{ "code": "SAVE10" }` and returns the candidate quote, including an applicability reason. It authorizes the cart before inspecting the code and does not reserve a use, change the draft or renew a delivery hold.
- `PUT /commerce/carts/{cartId}/discount` applies a valid code. `DELETE` on the same route removes it. Both require the observed `X-Cart-Version` and preserve the other checkout fields. An invalid replacement leaves the previous choice intact; normalized no-op edits do not renew activity.
- Cart and box quotes expose the selected code, its reduction and any typed rejection reason. A full draft save can retain incomplete form input; it does not establish coupon eligibility. Checkout rejects an invalid selection.
- A new checkout using a code requires `expectedTotal` equal to the current full payable quote. Every supplied expected total is checked, even when a code is cleared; existing callers without a code can omit it. A missing required or changed total returns `409 commerce.discount_price_changed` before stock, order or provider work. This is customer acknowledgement, never an authoritative amount supplied by the client. Cart versions still protect concurrent cart edits.

The new validation read does not write. Existing box reads retain their documented catalogue-drift repair behavior; adding coupon calculation does not renew capacity holds. Pending and paid checkout reads use their frozen charge summary, regardless of subsequent campaign edits.

## Eligibility and ordering

Codes are trimmed and uppercased. Negative/zero fixed discounts, unknown kinds and malformed legacy definitions cannot produce a surcharge. Percentage values allow at most four decimal places and fixed amounts two. Ordinary applicability failures return stable `commerce.discount_*` codes: `invalid`, `expired`, `already_used`, `not_eligible`, `currency_mismatch` and `inactive`.

Campaigns apply to goods by default or to a bounded nonempty list of tenant catalog product IDs. A box is one priced bundle; extras retain their own product identity. There are no invented per-dish prices. Delivery and stored Gift Card value are excluded. New orders store exact per-item allocations so reports and later refunds do not move a discount onto excluded items.

Discounted charge lines must fit the order store's four decimal places. Unsupported fractional amounts are rejected before a reservation or provider call, rather than silently changing the accepted price when SQL stores it.

Current order: existing product/promotional price, voucher, existing tax calculation, then delivery. Points and Gift Card tender remain the dependent #363/#364 work. Gift Card tender will be funding, not a discount that reduces taxable goods. Zero payable orders remain subject to the current payment capability; this change does not fabricate successful free payments.

## Payment and concurrency

One small reservation binds a discount to the cart and exact payment attempt. Checkout rechecks the campaign and allocations while claiming its existing cart transaction and touches the discount's native row version. Two carts cannot both submit the last permitted use. There is no separate voucher timer or mutable reserved counter.

Confirmed payment consumes that reservation and increments usage once, using the frozen price even if the campaign has changed. Unknown payment outcomes retain it. Existing recovery releases it only after proven closure, alongside stock and delivery capacity. A new attempt can then reuse the released row. Existing checkouts without this reservation retain their legacy completion path.

During rollout, unresolved preparations created by the real-payment checkout continue occupying a use without a reservation row; their frozen snapshots are unchanged. Earlier summary-only checkouts already recorded usage at submission. Confirmed recovery frees the pending legacy use; paid completion moves it to the existing usage counter.

Approved agent checkout uses the same expected total and cart version from the durable proposal; the server never refreshes either behind the approver's back.

## Administration

`GET /commerce/admin/discounts` provides bounded paging and optional search/active filters. Existing create remains; full `PUT /commerce/admin/discounts/{id}` requires `expectedVersion` and explicit editable fields. Code identity is immutable. Null clears expiry, usage cap or product restriction; an empty selected-product list is invalid. The usage cap cannot drop below redeemed plus reserved uses.

The Commerce **Discount codes** screen reuses the existing table, dialog, form controls and catalog search. Conflicts retain the editor draft and require an explicit refresh. Both general and food-commerce navigation profiles expose it. The canonical migration adds the reservation table and eligibility/allocation fields; the existing unique campaign and order-summary indexes remain.
