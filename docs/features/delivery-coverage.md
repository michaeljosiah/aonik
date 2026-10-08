# Delivery coverage

`GET /commerce/delivery/coverage?postcode=...` checks a current UK postcode against explicitly configured tenant coverage. The response contains `status` (`serves`, `not_served` or `unavailable`), `normalisedPostcode` and nullable `earliestDate`. A format check alone never produces `serves` or `not_served`: the enabled postcode provider must confirm the requested postcode first. `earliestDate` stays null until capacity-aware promises are available through #346.

Missing, repeated, malformed or nonexistent postcode input returns a 400 field error with code `commerce.invalid_postcode` and field `postcode`. A valid postcode outside the configured area returns `not_served`. Missing/disabled/malformed coverage configuration, an unconfigured provider or a technical lookup failure returns a normal coverage response with `unavailable`; clients should offer a retry rather than claim delivery is refused.

## Tenant configuration

Authorized staff use `GET`/`PUT /commerce/admin/delivery-coverage` under the existing AdminReadPolicy/AdminWritePolicy. GET returns 404 when no valid configuration is present. PUT replaces one existing-settings document, `Commerce.Delivery.Coverage`, at the current tenant scope. There is no global or user fallback, no seeded courier policy and no schema migration.

Both `isEnabled` and `allowedOutwardCodes` must be supplied. Optional `excludedOutwardCodes` defaults to an empty list; an optional `source` records where the operator obtained the policy. Lists accept exact outward codes, normalize casing and surrounding whitespace, and remove duplicates. Exclusions win. Prefix matches and wildcards are rejected; for example an allowed `SW1` does not implicitly include `SW1A`. An explicitly empty allowlist serves no postcode. Unknown JSON members are rejected so a misspelled exclusions property cannot silently widen coverage. The complete settings document is limited to the existing 4,000-character value capacity.

Courier approval remains a business input. A nonempty list must come from the approved courier's current policy, including any exclusions. The source field is metadata, not independent verification of that policy. Mainland coverage cannot be inferred from postcode syntax, geographic labels or this endpoint's existence.

## Postcode provider

Infrastructure supplies one small `IPostcodeLookup` adapter for the documented [Postcodes.io current-postcode lookup](https://postcodes.io/docs/api/lookup-postcode/). Set `Commerce:PostcodesIo:Enabled` to true only when the deployment has approved using that service. It defaults to false. A postcode is sent to its fixed HTTPS endpoint; no customer name, street address or cart token is sent. Request-path logging is disabled on this HttpClient because its URL includes the postcode. The existing OpenTelemetry HTTP trace configuration also excludes the exact `api.postcodes.io` host's `/postcodes/` paths, preventing the postcode from appearing in exported `url.full` attributes. HTTP metrics and unrelated traces remain enabled.

The client makes one request with a five-second timeout and a 64 KiB response limit, without inherited retries or redirects. A successful response must identify the same canonical postcode and outward code. A 404 is invalid input; timeouts, transport failures, throttling, other HTTP failures and malformed/mismatched results are unavailable. Caller cancellation propagates. There is no regex-only or cached-success fallback.

This verifies postcode existence, not a deliverable street address. A PAF-backed address selection integration remains pending the approved vendor. Existing manual address entry remains available, and it must pass coverage when checking out a box. Optional coordinates-to-postcode lookup is deferred.

## Checkout and rate limits

New dedicated box checkouts require a GB address and a current `serves` decision before calendar validation, stock reservations, order creation or payment initiation. The selected address can come from an explicit checkout payload or the saved draft. The canonical postcode is stored in the order's delivery snapshot without rewriting the draft or changing its version.

Invalid or unsupported-country input and confirmed noncoverage return 400; a failed coverage check returns 503 with `commerce.delivery_unavailable`. **Missing configuration therefore blocks new box payments after rollout.** Configure and verify the provider and approved tenant coverage before enabling the storefront. Generic non-box carts keep their existing delivery behavior. Authorized retries of an already-recorded checkout return the original result even during a provider outage or after coverage rules change.

The public checker and checkout share one native fixed-window limiter per resolved tenant and trusted remote IP. The default is 30 requests per minute, configurable through positive `Commerce:DeliveryCoverage:RequestsPerMinute`. The queue is empty; excess requests receive 429 and `Retry-After`, exposed through the existing CORS policy. Failed direct checkouts consume the same budget, so they cannot bypass the public checker's limit. This policy also bounds generic checkout requests. IPv4-mapped addresses share their IPv4 bucket, and caller-supplied forwarding headers do not override the existing trusted-proxy configuration.

Limits are per process. Multi-instance deployments need an ingress-wide policy if they require a cluster-wide budget; no new distributed limiter is introduced. Coverage/configuration/checkout responses use the existing no-store boundary, including errors and rate-limit responses.

## Remaining work for #352

The courier decision and actual allowlist/exclusions, approved PAF provider integration, and live deployment verification remain open. This implementation does not claim those decisions have been made, that address suggestions work, or that a delivery date is reserved. Tests use scripted postcode responses and verify configuration isolation, provider failure behavior, HTTP rate limits and checkout side-effect ordering without live provider calls.
