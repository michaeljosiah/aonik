# Checkout delivery details

Issue #345 adds one Commerce-owned delivery snapshot to the existing checkout and order-read paths. Orders still represent business intent; capturing a delivery date does not prove payment, reserve delivery capacity, or mean that an order has shipped.

## Checkout

`POST /commerce/carts/{cartId}/checkout` accepts a trailing `delivery` object:

```json
{
  "provider": "configured-provider",
  "paymentMethodType": "Card",
  "delivery": {
    "purchaser": {
      "email": "customer@example.test",
      "firstName": "Pat",
      "lastName": "Customer",
      "phone": "+44 7700 900123"
    },
    "address": {
      "line1": "1 Example Road",
      "line2": null,
      "city": "London",
      "region": null,
      "postcode": "SW1A 1AA",
      "countryCode": "GB"
    },
    "deliveryDate": "2026-11-05",
    "recipient": { "name": "Sam Recipient", "phone": "+44 7700 900456" },
    "notes": "Please ring the bell."
  }
}
```

This is a shape example, not an operational date, courier promise or payment-provider selection.

Dedicated box sessions (`BoxBundleProductId` is set) require delivery details. Generic Commerce carts can omit them; when supplied they receive the same validation and snapshot. If `recipient` is omitted, the submitted purchaser's name and phone become the recipient. An explicit recipient needs both fields. Nothing is inferred from a mutable customer profile, and an unverified purchaser email does not link or create an account.

Required fields are purchaser email/first name/last name/phone, address line 1/city/postcode/country, and delivery date. Values are trimmed; optional blank text becomes null; country and postcode are uppercased. Names are bounded at 100 characters each, email 254, phone 32, address lines 200, city/region 100, postcode 32 and notes 1,000. Human phone formatting is accepted with 6–17 digits. Address/contact control characters are rejected; notes may contain line breaks. A nonblank `windowId` rejects because no delivery windows are currently offered. No window or zone surcharge is invented.

Validation occurs after cart authorization and before inventory, order or payment work. Invalid details/calendar dates return the existing `400 commerce.storefront_validation` error; its message identifies invalid fields. Malformed JSON/date syntax follows the API's existing binding validation response. Unknown or unauthorized carts still return 404 before delivery validation.

The date must satisfy the tenant's active calendar, cutoff, lead time, delivery weekdays and blackouts. Missing, inactive, unresolvable or malformed calendar configuration cannot authorize a new shipping checkout. The stored timezone comes from that calendar, never from the request. Abby's Table must configure its real calendar with at least seven lead days before launch; other platform tenants retain their own calendar rules.

## Persistence and replay

`AnkOrderDeliveryDetails` stores bounded typed fields, soft-linked by `OrderId`, with one snapshot per tenant/order and an index for delivery-day planning. It is added only at checkout's final save alongside charge/selection facts and the cart's order binding. No historical addresses or dates are fabricated.

An authorized retry of an already recorded checkout returns its original result before validating new delivery input. It cannot rewrite the snapshot, and calendar or account-address changes do not alter historical order facts. Concurrent final saves preserve the winning snapshot and replay that winner's result. Failed staged rows are detached before cleanup so a shared inventory context cannot accidentally save them later. An incomplete recorded winner is an integrity error, not permission to cancel it or fill its missing data from another request.

This local save does not make provider calls, inventory, invoices and the Order module one distributed transaction. Real provider lifecycle and payment reconciliation remain #344.

The canonical context now registers `OrderFundingRef` before shared rowversion and tenant-filter configuration. This reconciles model-only drift: `InitialCreate` already created `AnkOrderFundingRefs.RowVersion` as a native rowversion, and no later migration altered it. The CLI-generated reconciliation migration therefore has documented no-op bodies under the repository's snapshot-reconciliation exception; its Designer and snapshot remain tool output. Both migrated databases and fresh model-created SQL test databases now match the runtime mapping. Delivery race tests use real Order creation, funding linkage and inventory; external payment-provider calls remain test doubles.

## Dates and order reads

`GET /commerce/config/delivery/dates?fromDate=2026-11-01&days=31` returns `earliestDeliveryDate`, `timezone`, inclusive `fromDate`/`toDate`, and an ascending `dates` array. `fromDate` defaults to the freshly computed earliest date; `days` defaults to 31 and accepts 1–62. This bounds a response, not how far in advance the business accepts bookings. A configured range with no eligible dates returns 200 with an empty array; unavailable configuration returns 404. The response is `no-store`. The original earliest-date endpoint remains compatible.

Customer and admin order summaries include nullable `deliveryDate`. Authorized customer details, protected guest-token details, and admin details include nullable `delivery`, using the same snapshot mapper. Legacy/nonshipping orders return null. The guest token remains a read capability, not account-claim proof. Customer reads remain party-scoped; tenant-wide admin order reads require staff roles (`PlatformAdmin`, `TenantAdmin`, `Operations`, `ReadOnly`), excluding `PersonalUser`. These order reads are `no-store`; guest reads retain `no-referrer`.

The existing admin order list, drawer and customer box-history card render the recorded date and delivery details. Calendar dates are formatted without shifting them through the browser timezone. Lists contain the date only; full contact/address data remains in authorized detail responses.

## Kitchen planning

The existing production-sheet and prep-list endpoints accept either their complete `fromUtc`/`toUtc` creation-time range or a single `deliveryDate=YYYY-MM-DD`. Missing, incomplete or mixed selectors reject. Responses retain the original `window` in creation mode; delivery mode returns `window: null` and the selected `deliveryDate`.

Delivery mode reads snapshot order IDs for that tenant/day and reuses the existing committed-order status filter, paged Order reads, bundle/personalisation aggregation, recipe explosion and stock netting. It does not filter by order creation time or infer dates for legacy orders. An empty matching set short-circuits before the Order API, whose empty-ID filter otherwise means unrestricted.

Production creation from a sheet accepts the same selector. Delivery mode requires an explicit `plannedFor` cooking timestamp; delivery date is not automatically cooking date. The existing `ProductionWindow` contract used by financial reporting is unchanged.

## Remaining launch dependencies

Address lookup and courier coverage are #352; calendar eligibility alone does not verify either. Actual weekdays, cutoffs, blackouts, coverage, capacity and any offered windows must be authored from approved operating decisions. Capacity reservations/payment grace are #346, persisted editable checkout drafts are #347, transactional confirmation is #349, and gift-card/hide-price controls are their own issues. This slice does not activate the parked calendar or treat prototype business values as configuration.
