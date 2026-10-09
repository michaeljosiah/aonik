# Stripe checkout and payment recovery

Issue #344 uses hosted Stripe Checkout for GBP card payments. The existing pending order represents business intent; the Finance intent represents the payment attempt, and a balanced ledger entry proves receipt. A successful payment completes that same order. An unpaid, confirmed closed attempt permits editing and retrying the same cart and order.

## Deployment configuration

Use the existing partner connector and encrypted credential-bundle administration:

1. Create a tenant-owned `stripe-checkout-v1` Collection connector with flat string configuration `environment` (`sandbox` or `production`), `accountId` (the actual Stripe `acct_…`), and `returnOrigin` (the storefront HTTPS origin, without path/query/fragment).
2. Store `secretKey` and `signingSecret` in that connector's credential bundle. Key mode must match the configured environment. The API key must be permitted to read its own account and create/read/expire Checkout Sessions and read their PaymentIntents. API requests use the current secretKey. Webhook verification accepts the current signing secret and an unexpired previous signing secret through the existing rotation mechanism.
3. Activate the connector and set **the tenant value** of `Finance.Payments.StripeConnectorId` to its ID. Global/user settings are not a fallback. Existing attempts retain their original connector, account, environment and return origin even after the selected connector changes. Merchant, environment or return-origin changes require a different connector so frozen create requests remain recoverable; disabling new checkout does not block reconciliation of bound attempts.
4. Configure a Stripe webhook at `POST /integrations/stripe/webhooks/{connectorId}` with the corresponding signing secret and the API version supported by the pinned Stripe.net 53 SDK (`2026-09-30.endive`). Subscribe to `checkout.session.completed`, `checkout.session.expired`, `checkout.session.async_payment_succeeded`, `checkout.session.async_payment_failed`, `payment_intent.processing`, `payment_intent.requires_action`, `payment_intent.payment_failed`, `payment_intent.succeeded` and `payment_intent.canceled`.
5. Run the existing outbox worker and provision the tenant's ordinary Finance ledger/accounts. Completion and receipt-email delivery use the existing integration-event pipeline. Enable and test tenant delivery coverage before testing a shipped box checkout.

No provider credentials or merchant details are seeded. The production simulator registration is removed. Unsupported Stripe setup/recurring/bill-payment gateway paths fail closed; this connector implements one-off Commerce checkout only. Broader gateway work remains under #178.

Verify the configured sandbox end to end before enabling live payments: checkout, decline, required action, abandoned-session recovery, successful return, delayed/duplicate webhook and receipt delivery. This implementation's tests use signed fixtures and the official SDK with scripted HTTP; they do not charge a card or certify a deployment's credentials.

## Storefront contract

The existing `POST /commerce/carts/{id}/checkout` accepts the cart capability and observed cart version. It freezes the authoritative charge and delivery details, claims the cart, and persists an attempt before any provider request. Only Stripe/Card/GBP in whole pennies, between £0.30 and £999,999.99, is supported; the lower bound follows [Stripe’s GBP minimum](https://docs.stripe.com/currencies#minimum-and-maximum-charge-amounts). Supplied success/cancel URLs must use the configured HTTPS origin; omitted URLs return to that origin's `/checkout`. A return URL is navigation only, never proof of payment.

`GET /commerce/carts/{id}/payment` returns the recorded current attempt and cart version. `POST /commerce/carts/{id}/payment/recover` requires the current attempt ID and observed cart version, as well as the same cart authority. Recovery expires and re-reads the exact provider session before allowing edits. A stale attempt or stale tab cannot release a newer attempt's stock or overwrite its charge snapshot. Optional invoice-backed checkout requires staff assistance for recovery, so an issued invoice cannot silently retain stale lines. Customer identity continues to come from the authenticated party; guest authority comes from the unguessable cart token.

The cart status read returns `processing`, `succeeded`, `failed`, `cancelled` or `requires_action`; success waits for the payment completion event to finish the cart. Finance state values are `Pending`, `RequiresAction`, `Processing`, `Failed`, `Cancelled` and `Captured` (successful receipt). A decline can remain payable at the provider, so `Failed` alone does not authorize editing. Recovery must establish unpaid closure. A cancelled browser navigation likewise requires server recovery. The checkout URL is exposed only while the current attempt can accept payment; it is omitted for success or confirmed closure.

Guest purchaser details create a normal unverified Party scoped to the checkout attempt, with a Customer role referencing the cart. They do not create a User, claim an existing email address, adopt a saved cart or link an account. Once a pending order exists, its cart cannot change buyer through cart adoption; guest recovery retains guest authority. Generic nonshipping guest checkouts must supply purchaser details or have an existing buyer party.

## Failure and concurrency rules

- One server-generated attempt ID and idempotency key survive double submits, lost create responses and two tabs. Frozen provider parameters are persisted before the external call. No SQL transaction is held across Stripe HTTP.
- A timeout or missing provider response remains unknown. The exact same create request can recover its session within a conservative 23-hour window. Beyond that, operator reconciliation is required because Stripe may discard old idempotency keys; the code never automatically creates another session for an unresolved attempt.
- A persisted local Pending attempt that has never started its external request can be cancelled with a native row-version claim. A missing intent is **not** proof of cancellation: the same frozen checkout can resume, but the cart stays protected.
- A valid signature durably records the event and reconciliation work together. Event data supplies correlation only. A fresh authenticated provider read must match tenant, connector, merchant, environment, intent, order, amount and currency before any financial state changes.
- The payment receipt, ledger posting, intent success and completion outbox event commit together. Repeated or out-of-order notifications cannot downgrade captured money or post it twice. Admin authorize/capture/cancel endpoints cannot bypass provider reconciliation for Stripe-bound intents.
- The existing inventory and cart-maintenance paths protect preparing/awaiting-payment carts. Stock is released only after confirmed unpaid closure. Delivery-date capacity and the 15-minute hold plus payment grace are separate #346 work; a provider timeout must never be treated as permission to release them.
- Agent-initiated `commerce_checkout` is High risk and executes through an approved durable `Commerce.Checkout` proposal, preserving the approved cart version. It never calls Stripe directly in-band. A stale approval cannot resume a cart already claimed by another request. If proposal execution times out after claiming its own attempt, the ordinary authorized checkout/recovery flow resumes that attempt; the dispatcher does not silently refresh approval or bind it to a new attempt.

## Storage and operations

Apply the canonical EF migrations before enabling checkout. The Payment row-version reconciliation is a documented snapshot-only no-op: InitialCreate already made the physical column native rowversion. The following feature migration adds nullable fields and filtered uniqueness; existing duplicate tenant/order charge summaries or overlong legacy provider references require operator investigation rather than silent deletion/truncation.

The implementation extends existing Cart, PaymentIntent and Payment receipt rows and reuses OrderChargeSummary, PartnerWebhookEvent, the outbox, credential bundles and ledger capture posting. Provider create JSON is private server state and contains references/return URLs, not credentials. Raw webhook card/customer data is not retained. SDK telemetry and request logging are suppressed; money-action logs use order/intent references and sanitized outcomes.

An unresolved attempt must be inspected at the originally bound merchant. Never clear its provider IDs, mark it locally cancelled, delete its payment row or release its holds to make checkout work. Repair credentials/configuration or reconcile the actual provider outcome, then use the normal recovery path. Keep #346's capacity policy and #349's live email verification on the launch checklist.
