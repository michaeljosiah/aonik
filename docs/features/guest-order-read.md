# Guest order confirmation and polling

Successful guest checkout returns `guestOrderToken` alongside `orderId` in the
existing checkout response. An authorized checkout retry returns the same recorded
order and payment, with a usable token for that order. Tokens already returned remain
valid. Checkout of a party-owned cart returns null for this field; signed-in customers
use the existing `/commerce/storefront/orders` routes.

Read the guest order with:

```http
GET /commerce/storefront/guest-orders/{orderId}
X-Tenant-Id: {tenantId}
X-Order-Token: {guestOrderToken}
```

The response uses the existing storefront order detail: placed time, order status,
currency, charge breakdown, box size, items and selections. `paymentStatus` comes
from the durable checkout charge summary and remains separate from the business
order's `status`. Poll this same order after refresh or a payment redirect. Reads have
no side effects and do not initiate another checkout or payment. Payment completion
converges the recorded status to `Captured`; the real-payment integration owns other
provider outcome reconciliation (#344).

The token grants only a read of this tenant/order. Missing, malformed, tampered,
wrong-order and wrong-tenant tokens return the same `404` as an absent order. A cart
token cannot authorize this read. The order must still have its durable cart and
charge summary. Guest confirmation remains readable with its originally issued token
if the order is subsequently linked to an account.

The detail response excludes payment client secrets, payment checkout URLs, raw order
metadata and party/account identifiers. Keep both checkout and guest-order responses
out of caches: the endpoints send `Cache-Control: no-store` and
`Referrer-Policy: no-referrer`. Transmit the token only in its dedicated header, never
in a URL. Treat it as a secret and keep it out of logs and analytics.

## Implementation and lifetime

A small Commerce helper uses the platform's existing ASP.NET data-protection provider
and database-persisted key ring. Its protection purposes include the capability type,
tenant and order. There is no new token table, signing-key configuration or migration.
All guest checkout issuance paths run after cart authorization and a durable checkout
result, including retries and concurrent-checkout replay.

This read capability is reusable and has no separate application expiry; the existing
data-protection key lifecycle applies. It is separate from single-use, expiring account
setup or password links. The old storefront's two-hour snapshot cookie is not the
source of order truth or a token lifetime rule. The storefront should retain the
returned token with its order reference and fetch current state from this endpoint.
