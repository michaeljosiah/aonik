# Checkout drafts and cart versions

Issue #347 stores the checkout form on the existing tenant-scoped cart. It reuses `Cart.RowVersion`, cart authorization, `no-store` responses and the existing abandonment job. The two added fields are nullable `CheckoutDraftJson` and `LastActivityAtUtc`; there is no second draft aggregate or version.

## Save and resume

`PUT /commerce/carts/{cartId}/checkout-draft` replaces this small document in full. Omitted/null sections clear prior values. Send the guest capability in `X-Cart-Token`, or authenticate as the cart's owning party, and send the latest `cartVersion` in `X-Cart-Version`.

```json
{
  "purchaser": { "email": "customer@example.test", "firstName": "Pat", "lastName": "", "phone": "" },
  "address": null,
  "recipient": null,
  "deliveryDate": null,
  "notes": null,
  "gift": null,
  "createAccount": false,
  "discountCode": null
}
```

Incomplete fields are accepted while typing. Text is trimmed and bounded using the order-delivery field limits; notes and greeting messages allow line breaks and at most 1,000 characters. Codes have a 64-character bound. These are storage bounds, not confirmation of physical card-printing capacity. Unsupported control characters reject. The serialized document is bounded to 24,000 characters, including escaping. Passwords, provider secrets and client-supplied capacity reservations are not part of the document.

The response contains `cartId`, `cartVersion`, `status`, `orderId` and `draft`. Existing generic and box cart reads expose the same document as `checkoutDraft`. Box reads also expose `status` and `orderId`. Saving unchanged normalized values or reading the cart does not refresh activity. Clearing an already empty document is a no-op.

`gift` stores `giftIntent`, `hidePrices`, `includeGreetingCard` and `greetingCardMessage`. Turning gift intent off clears that section; deselecting the card clears its message. This is intent storage: checkout rejects an enabled gift until #353 implements pricing/fulfilment. `createAccount` records the preference only; secure post-payment setup belongs to #350.

## Conditional edits

All new line, bundle, size, extra, removal, continue, draft and checkout writes require the observed version after authorization. First adoption without an explicit keep/use choice also requires it. Existing explicit adoption choices retain their two body versions; a duplicate header version is unnecessary. New-cart creation has no prior version. Reads, quotes and authorized recorded checkout/adoption replays do not require a version.

Missing, malformed or stale versions return `409 commerce.cart_conflict` with current cart ID/version/status/order ID. An already locked cart returns `409 commerce.cart_locked`. Unauthorized callers still receive 404 without these facts. A SQL race after the initial check returns the existing 409 concurrency response; reload the cart before choosing what to resubmit. No client write is automatically replayed against a newer version. Native SQL rowversion remains the source; the empty InMemory test token is valid only when explicitly supplied.

Parent version/activity and child edits are saved together. Rejected writes discard only their cart's tracked graph so a later inventory save cannot commit them. Catalogue repairs update the parent version without extending user retention. Checkout repairs return the refreshed box/version before order or payment work. `CheckedOut` identifies completion in another tab; `Open` with an `orderId` identifies a locked payment attempt, not proof of payment success.

## Checkout and dependencies

Checkout uses the saved contact/address/date/notes if no explicit `delivery` is submitted. An explicit delivery object supplies the complete attempted delivery; fields are never mixed with another saved version. Full delivery/calendar validation still runs before inventory/order/payment effects. A null/omitted discount code uses the saved code; an explicit empty code clears it for that attempt. Authorized replay returns the original recorded order before validating a new version or input.

The selected draft's contents survive account adoption. `KeepGuest` retains the complete guest draft; `UseSaved` retains the complete account draft. Neither merges fields from two drafts.

The [retention policy](cart-retention.md) uses 24 hours with no dishes and seven days with dishes, configurable through existing settings. Background repairs preserve the last user activity time. Carts with an order remain excluded from expiry.

Issue #347 remains open for payment failure/cancellation recovery, which requires #344's authoritative provider outcome and retry lifecycle. A browser cancel URL, elapsed timer or unknown outcome cannot unlock a cart or clear its order. #346 owns server-created capacity holds; arbitrary client reservation IDs are not accepted as holds.
