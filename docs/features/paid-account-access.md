# Paid checkout account access

Issue [#350](https://github.com/michaeljosiah/aonik/issues/350) follows the Abby's Table page behaviour guide: account setup is optional, happens after payment, and never requests a password before payment. Authentication, registration and password reset continue through the configured identity provider.

## Paid order flow

1. Persist the customer's `createAccount` choice in the existing checkout draft. Checkout freezes that choice and the purchaser's email with the payment attempt. Later draft edits cannot change paid consent or the recipient.
2. Successful payment reconciliation issues one Platform account action for that exact guest payment. The existing outbox delivers the account-access email independently of the order receipt. Replayed payment events reuse the same action.
3. The storefront receives the opaque token in the configured action URL. Keep it out of analytics, logs and referrers. `POST /identity/account-access/resolve` accepts `{ "token": "..." }` and only reports whether the link is usable. Opening an email or resolving a link does not consume it or create an account.
4. The customer explicitly uses the normal identity-provider login/registration flow. The storefront then posts the token to `POST /identity/account-access/complete` with a valid API bearer token. The API requires trusted, verified email evidence matching the paid recipient. Knowing an email address or supplying claims in request JSON is insufficient.
5. Completion consumes the action and queues a reference-only verified event in one database transaction. Commerce rechecks the exact paid source and attaches the cart to the canonical customer party. It preserves the historical order/payment payer, ledger records and guest order capability. Existing account order queries use the cart's buyer party.

An unknown external subject can reach only the specifically marked completion endpoint after normal JWT validation. This exception does not enable ordinary tenant auto-registration. Existing inactive users and revoked sessions remain denied. A matching email on a different local subject is never silently linked.

## Expiry and resend

Each generation expires ten minutes after its first delivery attempt and shares one durable consumption state across all encodings. Delivery retries never extend that deadline. Invalid, expired, consumed, superseded or wrong-tenant links resolve to the same HTTP 410 result, with `Cache-Control: no-store` and `Referrer-Policy: no-referrer`. The storefront should render its existing “Link no longer valid” page without displaying an email or account-existence message.

`POST /identity/account-access/resend` accepts the original token and always returns an empty HTTP 202. An eligible expired action can rotate to a fresh generation; a consumed action cannot reopen. Delivery budgets are persisted: at most five sends per action and ten across the same normalized recipient in a fifteen-minute window. Email-change initiation also has a per-user budget. A shared tenant/client IP policy bounds requests before handlers; throttled resend/reset responses remain neutral.

The token uses the existing Data Protection keys and contains only action references. The database owns expiry, generation and consumption. Outbox payloads contain references, never email addresses or bearer capabilities. API and Worker must use the same persisted Data Protection key ring.

## Email changes and password reset

Email changes start through `PUT /profiles/customers/me/email`. The flow requires fresh trusted authentication evidence and sends confirmation to the new address before changing either the local user or the provider. `POST /identity/email-change/complete` explicitly confirms the pending action for the same subject. The action records an `Applying` state before the external update so a provider timeout can reconcile the same subject/target safely. Local email/contact changes, identity revision and session revocation follow confirmed provider state.

The Auth0 implementation supports the configured database connection and verifies the subject/connection and final verified address. Unsupported providers/connections fail closed. The old immediate email update is not used by this customer flow. An old JWT or verification code cannot overwrite a confirmed email change.

Password reset uses **themed identity-provider pages**, reusing `POST /identity/password/forgot`. Configure the provider's branding, default application login route and expired-link experience. Responses remain neutral when an account does not exist or the provider is unavailable. Aonik does not store passwords or implement another password-reset credential system.

## Configuration and deployment

The tenant-owned `Identity.AccountAccess` setting has this shape:

```json
{
  "isEnabled": true,
  "storefrontOrigin": "https://store.example.com",
  "setupPath": "/account/access",
  "emailChangePath": "/account/email-change"
}
```

Use the approved storefront HTTPS origin and actual landing routes. Missing configuration does not fabricate a link or discard the paid action; queued delivery can retry after setup.

`Auth:AccountAccess:EmailClaimType`, `EmailVerifiedClaimType` and `AuthenticationTimeClaimType` default to `email`, `email_verified` and `auth_time`. These claims must be issued by the trusted provider for the API audience. Auth0 API access tokens may require an Action and configured claim names. Missing, ambiguous, unverified or stale evidence fails closed. Do not populate these claims from customer-controlled metadata. Issuer validation must remain enabled. `Identity:SecurityRequestsPerMinute` defaults to 30.

Deployment still requires the real provider/tenant configuration, approved OIDC PKCE callbacks, hosted-page branding, required Auth0 management scopes/connection policy, ACS sender/domain setup, shared keys and a running outbox worker. Verify new and existing account access, expired/consumed links, resend limits, and an uncertain email-change response in the configured sandbox before enabling the tenant setting. These are operational inputs; the repository does not provision live credentials or storefront pages.
