# Storefront order history and reorder

Issue [#360](https://github.com/michaeljosiah/aonik/issues/360) extends the existing storefront and admin order reads. Orders remain business intent; payment state and fulfilment progress are separate facts. Points earned and redeemed remain dependent on #363.

## Purchased facts

New orders receive a human reference from the existing tenant `Order` autonumber profile. Configure that profile through the existing Autonumbering settings page; a prefix such as `AT-` and the starting sequence are tenant choices. Without a profile, orders retain the platform's `ORD` fallback format. An explicitly disabled, exhausted or invalid profile fails instead of silently changing format. Concurrent allocation uses the profile's native version, and references are unique per tenant. Failed or competing requests can leave sequence gaps. Idempotent order retries retain the winning reference.

Checkout freezes item and dish names alongside the existing purchased selections. The optional tenant setting `Commerce.Storefront.SignatureTag` names a catalog product tag used to determine the purchased Signature flag. With no valid designation, that flag remains unknown; a surcharge never implies Signature. Subsequent catalog, tag or setting edits do not rewrite purchased facts. Historical orders without recorded names or flags remain unknown, with durable SKU/type fallbacks in administration. No current catalog name is presented as a historical purchase name.

Order cards expose the reference, purchased dishes, payment status, gift marker, frozen discount code and amount, and delivery details. Detail, packing and default receipt templates use the same recorded names and reference. Hidden-price packing slips retain their existing privacy rules.

## Accepted Terms

Configure `Commerce.Storefront.SaleTerms` with the approved version and its permanent HTTPS URL, for example:

```json
{"version":"sale-v1","url":"https://your-store.example/terms/sale-v1"}
```

This change does not author or approve legal terms. Public storefront configuration exposes `saleTerms`. The checkout form records an explicit acceptance by saving `acceptedTermsVersion` in the existing versioned checkout draft. When the tenant configures terms, a new box checkout requires the exact current version and freezes its version, URL and server timestamp with the prepared payment. Invalid configured terms block checkout. Tenants without a terms setting retain existing behavior; no acceptance is invented for them or for historical orders.

An existing payment retry keeps its original snapshot. After proven unpaid recovery, a new checkout must satisfy the current terms. Purchaser and admin details expose `delivery.saleTerms`; recipient packing slips do not include purchaser consent data.

## Fulfilment progress

Staff explicitly advance a paid order through `Confirmed`, `Cooking`, `OutForDelivery` and `Delivered`. A captured order without a recorded stage displays Confirmed, without inventing an initial event time. Dates do not advance progress. Cancelled, failed or expired order intent takes precedence in the read projection.

`PUT /commerce/admin/orders/{orderId}/fulfilment` uses the existing Commerce admin write policy and accepts `status` and the observed `expectedVersion`. Only the next stage is allowed. The existing delivery row stores the stage and a bounded history of three transitions, including authenticated actor IDs and server times, atomically under its native row version. Repeating the current stage is idempotent; a conflicting advance returns `409 commerce.fulfilment_conflict`. This operation neither moves money nor changes the core order/payment state.

Customer summaries expose `historyGroup`: Delivered and Cancelled are `Past`, other captured orders are `Upcoming`, and unresolved payments are `PendingPayment`. The existing paginated order list remains an All-orders page with a total for that same query. Clients group the loaded cards and retain shared pagination; there is no post-pagination group filter or invented group count. Admin detail exposes transition history and the version needed for updates; public responses omit staff actor history and concurrency tokens.

## Order again

Authenticated customers call `POST /commerce/storefront/orders/{orderId}/reorder`. The source must belong to the current tenant and party and have a captured payment and a completed box cart. Guest read tokens do not authorize reorder. The operation reuses box creation and the one-active-box rule; an existing open box produces the existing conflict instead of being overwritten.

Only purchased dishes are copied into a fresh box at current prices. Current size plans, slots, options, availability and stock checks apply. Removed or unavailable dishes are skipped with `changes` notices that identify the source dish; stored options use the existing normalization and drift notices. An invalid source size/composition fails. The customer can finish or change a partially rebuilt box before checkout.

The batch commits as one cart creation, including native concurrency and uncertain-commit recovery. It copies no extras, delivery date, capacity hold, address, gift, greeting card, discount, accepted Terms or payment state. No stock or delivery capacity is reserved by reorder. The response is the existing `BoxCartDto`, owned by the signed-in party, with no guest token.

## Deployment and verification

The canonical migration adds nullable purchased display facts, Terms and fulfilment fields, and changes order-reference uniqueness to tenant scope. It does not backfill unverifiable history. Configure the tenant's approved numbering, Terms and Signature tag and connect the separate Abby's Table storefront to these contracts. Merging AONIK does not deploy that storefront or publish tenant configuration.

Tests exercise frozen history, Terms validation, tenant/party access, current-price reorder and skip notices, native active-box races and lost commits, concurrent number allocation, and versioned fulfilment transitions. These changes reuse the order spine, cart creation, payment snapshots, catalog normalization, settings and autonumbering rather than adding parallel workflows.
