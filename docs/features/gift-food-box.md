# Gift food boxes

Issue [#353](https://github.com/michaeljosiah/aonik/issues/353) uses the existing food box and versioned checkout draft. This is a physical food gift; stored-value Gift Cards are separate work in #364.

## Storefront contract

Save `gift` through the existing checkout-draft endpoint: `giftIntent`, `hidePrices` (defaults to true), `includeGreetingCard` (defaults to false), and optional `greetingCardMessage`. Turning gifting off clears the gift draft; turning the card off clears its message. These edits preserve the food, extras, purchaser details and selected delivery date. They do not extend a capacity hold. Messages are plain text, at most 1,000 characters, and rendered as text, including line breaks.

A gift requires an explicit recipient name and phone at checkout. Purchaser details remain the contact for payment and account access. The recipient, address, delivery date, instructions and gift choices are frozen with the existing payment preparation and order delivery snapshot. Pending payment retries retain that snapshot. Editing becomes possible only after the existing recovery process proves the previous payment can no longer complete.

## Greeting-card price

Configure the tenant setting `Commerce.Storefront.GreetingCard` with a complete JSON value. For Abby’s Table, the design specifies:

```json
{"isEnabled":true,"currency":"GBP","amount":3}
```

There is no global or hard-coded tenant fallback. Public storefront configuration exposes `greetingCard: { amount, currency }` when enabled and valid. A selected card with unavailable or incompatible configuration blocks a new quote/checkout until removed or corrected. Unselected cards require no configuration. Amounts must be positive, have whole pennies and match the cart currency.

Quotes expose a separate `greetingCard` component. Checkout materializes one `GreetingCard` order item and invoice line, without a catalog product, food selection or inventory reservation. The fee belongs to subtotal and tax calculation but does not change the box price. Unrestricted discount campaigns can include it; selected-product campaigns cannot. Delivery and stored-value Gift Cards retain their exclusions.

Starting a checkout with a card requires `expectedTotal` equal to the returned payable quote. Every supplied expected total is checked. A missing required or changed total returns the existing `409 commerce.discount_price_changed` before payment or stock reservation. The prepared charge freezes once; later setting edits do not reprice an active payment. Proven unpaid recovery can remove gifting and replace the old snapshot on the same order.

## Administration and packing

Order summaries expose `isGift`; detailed purchaser/admin responses include `delivery.gift`. Financial records and purchaser totals remain complete. Authorized staff read `GET /commerce/admin/orders/{orderId}/packing` for captured, non-cancelled orders and print through the Commerce packing page. This endpoint requires the existing admin policy and tenant/module access.

When `hidePrices` is true, the server omits the entire packing `prices` envelope. The slip includes recipient delivery details, food/options, instructions and the escaped greeting message, without purchaser contact or financial payloads. The print view consumes only this packing response. Private responses use no-store headers. A normal box or a gift with visible prices includes the frozen monetary breakdown.

Margin reporting adds greeting-card net revenue to `nonCatalogRevenue` and unknown-cost revenue. It reuses frozen discount allocations and does not invent a zero-cost product or claim card margin is known. Points exclusion will be enforced with the points implementation in #363; this change does not create a loyalty system.

## Deployment and verification

The canonical EF migration adds four gift snapshot fields and the greeting-card charge, with false/zero defaults for existing orders. Configure the tenant setting and connect the separate Abby’s Table storefront to the draft/config/quote contract. This backend change does not deploy that storefront or provision tenant settings.

Regression coverage exercises quote/order/invoice/payment agreement, changed-price acknowledgement, frozen retries, unpaid recovery, exact payment completion, private packing, tenant isolation, native SQL row-version protection, Unicode messages, discount eligibility and reporting. Existing checkout, hold and payment services remain the execution path.
