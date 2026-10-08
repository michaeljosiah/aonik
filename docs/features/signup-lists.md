# Sign-up lists

The Platform API stores three independent, tenant-scoped lists: `newsletter`,
`delivery-availability` and `private-table`. This implements Aonik #357. The Abby's
Table behaviour guide removes newsletter signup from Order Confirmation only;
the footer signup remains a separate action.

Each accepted signup stores the submitted contact details and the server-configured
consent version, wording, form source and UTC timestamp. It creates no account or
Party and changes no account marketing preferences. Form submission is the scoped
affirmative action; a newsletter checkbox is not added to the other two forms.

## Publish the forms

Use the existing tenant settings writer, with its existing `Settings.Write`
permission: `PUT /tenant/settings/values/SignupLists.Configuration`. Set `value` to
a JSON-encoded document containing only the enabled lists. The setting is not
exposed by the generic public-settings API. There is no global fallback or automatic
production seed. This illustrative document shows the contract; publish the actual
wording displayed by the storefront and change its version when that wording changes:

```json
{
  "lists": [
    {
      "listType": "newsletter",
      "consentVersion": "newsletter-v1",
      "consentText": "Send me kitchen notes and offers. I can unsubscribe at any time."
    },
    {
      "listType": "delivery-availability",
      "consentVersion": "delivery-v1",
      "consentText": "Use my email only to let me know when delivery reaches my area."
    },
    {
      "listType": "private-table",
      "consentVersion": "private-table-v1",
      "consentText": "Use my details only to contact me about Private Table.",
      "services": [
        { "id": "recipe-development", "label": "Recipe development" },
        { "id": "recipe-development-and-meal-preparation", "label": "Recipe development and meal preparation" },
        { "id": "not-sure", "label": "Not sure yet" }
      ]
    }
  ]
}
```

The existing setting limit is 4,000 characters. Each definition requires a consent
version (up to 32 characters) and text (up to 1,000). Private Table requires 1–10
unique service IDs with labels. Unknown/duplicate list definitions, malformed JSON
or missing required properties fail closed: the public configuration returns an
empty list and capture is rejected. Removing a definition disables new capture but
does not disable unsubscribe or erase previously recorded consent.

## Capture

`GET /v1/signup-lists` returns the typed configuration with `Cache-Control: no-store`.
Display its wording and submit its version to
`POST /v1/signup-lists/{listType}`. Bodies are:

| List | Required body fields | Optional fields |
| --- | --- | --- |
| `newsletter` | `email`, `consentVersion` | None |
| `delivery-availability` | `email`, `consentVersion`, `postcode` | None |
| `private-table` | `email`, `consentVersion`, `name`, `country`, `service` | `phone` |

`country` is the two-letter country code resolved by the form. The optional phone
accepts human formatting (up to 32 characters and 6–17 digits, matching the form).
Irrelevant list fields are rejected. Delivery interest
preserves the full normalized UK postcode and its outward code. Syntax checking
does not prove that a postcode exists or is outside the delivery area: the frontend
must offer Notify me only after a real not-served result, never a lookup failure.
Coverage decisions remain the responsibility of #352.

Capture returns an empty `202` only after durable storage or a matching existing
record. Both cases look identical; no PII, IDs, subscription state or unsubscribe
token is returned. Invalid input, disabled lists and stale consent versions return
`422`, following the API's global validation convention. Responses use `no-store`
and `Referrer-Policy: no-referrer`.

The SQL unique key is tenant + list + trimmed/lowercase email for non-deleted rows.
It deduplicates concurrent submissions; email dots and plus suffixes are retained.
Repeat submissions preserve the original details, postcode and consent evidence,
including withdrawal. The same email may independently join different lists or
tenants. Updating an existing contact or re-subscribing after withdrawal needs a
separate verified flow; an unauthenticated retry cannot do either.

## Reporting and unsubscribe

`GET /admin/signup-lists/{listType}?pageNumber=1&pageSize=50` returns paged JSON
(page size 1–100), active subscribers by default. `includeUnsubscribed=true` includes
withdrawn records for administration. `GET /admin/signup-lists/delivery-availability/areas`
counts unique active subscribers grouped by outward code. These reads require
`AdminPolicy` and `Customers.Read`, use `no-store`, and are always tenant-scoped.
They are the export/reporting interface; no new dashboard or campaign engine is needed.

Active report rows include an `unsubscribeToken` protected with the existing persistent
Data Protection keys and bound to tenant, list and subscription ID. The communication
sender includes those values in its unsubscribe landing link. That page performs
no mutation on GET; after the user's action, it submits `{ "token": "..." }` to
`POST /v1/signup-lists/{listType}/{subscriptionId}/unsubscribe`. A valid token returns
`204`, including repeat/concurrent withdrawals. Missing or oversized tokens return
`422`; tampered or mismatched tokens return `404`, all without changes. The capability grants only withdrawal from
that one list and has no separate application expiry.

Never expose admin report tokens in a signup response, logs or analytics. Unsubscribe
responses use `no-store` and `no-referrer`. Suppressed contacts are absent from default
exports. This feature stores signups and supplies unsubscribe capabilities; it does
not send email, promise a reply, book Private Table, or depend on ACS availability.

## Persistence

`SignupSubscription` is Platform-owned and mapped in both PlatformDbContext and
canonical AonikDbContext. `AddSignupSubscriptions` is generated through the canonical
Infrastructure migration stream. Tests cover list isolation, consent validation,
report permissions, capability scope and SQL insert/withdrawal races.
