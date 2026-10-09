# Operator storefront refunds

Issue #365 extends the existing Finance `Refund`, Stripe gateway, ledger writer, gift-card operations, loyalty operations and Order drawer. It introduces no wallet, scheduler, customer cancellation action or AI money tool. Human staff need `Payment.Refund` and the existing Admin write policy; reading refund history requires `Payment.Read` and the Admin read policy.

## Staff flow

1. Open the existing order drawer and select Refund. The server supplies the original paid goods, fees and tax components. Zero-price dish selections are not separate refundable purchases.
2. Choose each component's full remaining value or a positive GBP amount with no more than two decimal places. Supply a reason of 1–500 characters. Full remaining also handles fully points-funded lines with no cash value.
3. Review the server's exact cash return, gift-card return, earned-point reversal and redeemed-point restoration. Any edit requires another preview. A stale preview conflicts instead of silently changing the approved effects.
4. Confirm once. A durable refund ID is recorded before gateway submission. Requested, Pending and Unknown are not success. The UI retains the same ID and payload through uncertain network outcomes, drawer closure and reload.
5. Use Check or Reconcile on that request to recover a missing notification or retry after configuration repair. Do not create another refund to work around an unknown outcome.

Only one unresolved refund per order is admitted. The original payment's native SQL rowversion serializes admissions and financial outcomes; gift-card and loyalty owners retain their existing native balance protection. A failed request frees its budgets only after the gateway proves failure and no local financial effects were applied.

## Original allocation and accounting

The server reads saved Commerce coupon, points, gift and charge facts and checks them against actual Finance receipts and capture journals. Missing or inconsistent historical evidence is unsupported, rather than reconstructed from today's catalogue or prices. Refund amounts cannot exceed the original paid component or payment rail.

Saved discount and gift shares can have four decimal places. The existing proportional allocator normalizes refundable component amounts to pennies while conserving the original total. Partial gift returns use cumulative original component weights, rounded down to pennies, with the exact remainder on a complete return. Cash is the remaining monetary amount. A selection that cannot preserve the original rail limits must include the remaining components together. Earned and redeemed points use cumulative integer floors for each original line; completing the line returns its remaining point allocation exactly. Non-earning fees and tax do not reverse earned points.

Cash goes only to the original Stripe payment and merchant binding, never a new destination. A return posts debit to the original clearing account and credit to original cash. Gift-funded value goes back to the same instrument with its original validity, using debit to clearing and credit to its original liability. Purchased gift value must still be unspent and unreserved: admission reserves it on the existing Refund row before any cash request, then posts a liability reclaim on confirmed success. An already expired or unavailable card requires support resolution before admission; an accepted obligation remains protected if expiry passes while its outcome is unknown. No expiry extension, replacement instrument or gift-to-cash conversion is inferred.

A pending or completed purchase reclaim blocks new fulfilment-secret release through the existing gift service, so scheduled email and physical print cannot advertise the original face value after a refund. Ordinary spending still allows resend of the same instrument; a proven failed refund releases the delivery block.

The cash journal, gift operation, loyalty reversal/restoration, refund outcome and order history commit in one Finance transaction. The gateway call takes place outside that transaction. If local persistence fails after provider success, reconciliation reads the same provider refund and completes the local effects once. Points already spent can become a negative balance, following the existing loyalty rules and verified guest-claim ownership.

Original captured PaymentIntent and Payment amounts, invoice totals, and fulfilment are not rewritten. Refund status is a separate projection. Refunding does not cancel cooking/delivery, restock food, release delivery capacity or reopen a cart. **There is no automatic credit-note service:** any required invoice revenue/tax correction uses the merchant's manual accounting process. A funding-return journal is not an invoice credit note; the staff preview states this when an invoice exists.

## Stripe and recovery

The existing connector must retain access to its original Stripe merchant and mode, with refund permission. Subscribe the existing signed webhook endpoint to `refund.created`, `refund.updated` and `refund.failed`. Current account-bound provider reads, rather than the status embedded in an event, determine the outcome. A positive one-penny refund is valid; checkout's minimum new-card-payment rule does not apply.

Provider requests use the immutable local refund ID as their idempotency identity. A durable first-submission timestamp precedes HTTP. An uncertain create can repeat the exact request within the conservative 23-hour window. After that, bounded provider lookup can find the original operation; absence never authorizes another POST. The existing outbox handles transport retries and dead letters. Normal Pending observations await a later signed event or explicit staff reconciliation; there is no claim of indefinite automatic polling.

Before first submission, a complete bounded provider refund listing must match known local cash returns. Dashboard/external refunds are not assigned invented goods or points allocations: signed known-payment events are retained for reconciliation, and unexplained provider activity blocks new refunds. Such discrepancies require accounting support. No dashboard import/allocation editor or alternate payout is included.

Stripe may report a failure after success. Proven returned cash appends an inverse cash journal; it never deletes the original return. The request becomes NeedsReconciliation and continues to hold its commercial budget. Gift/points effects are not blindly clawed back, and another payout is not automatically issued. Unsupported bank-return evidence remains visible for support.

## API

All routes are tenant-scoped staff routes with private response headers:

- `GET /commerce/admin/orders/{orderId}/refunds`: components, remaining value, status and history.
- `POST .../refunds/preview`: `{ reason, selections: [{ componentId, amount }] }`, or a selection with `fullRemaining: true` and no amount.
- `POST .../refunds`: the same reason/selections plus `refundId` and the preview's `expectedPreviewVersion`; returns 202 with the recorded status.
- `GET .../refunds/{refundId}`: exact identity lookup after an uncertain response.
- `POST .../refunds/{refundId}/reconcile`: queues recovery of that already-authorized record.

Clients cannot supply tender splits, point quantities, provider references, bank destinations or ledger accounts. Public DTOs omit private snapshots, gift secrets and raw provider responses. The separate Abby storefront still needs to consume its order refund projection; this backend/Admin change does not deploy that frontend or configure a live Stripe merchant.
