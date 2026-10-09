# Delivery capacity and date reservations

Issue #346 adds capacity to the existing fulfilment calendar and box checkout. Calendar eligibility still comes from the tenant's authored timezone, cutoff, lead days, delivery weekdays and blackouts. For Abby's Table, configure and verify the approved UK calendar and at-least-seven-day promise; these are product operating rules, not defaults for every AONIK tenant.

## Business configuration

`GET /commerce/admin/delivery-capacity?fromDate=YYYY-MM-DD&days=31` reads configured budgets and occupancy. `PUT /commerce/admin/delivery-capacity/{date}` accepts `unit`, `capacity` and `expectedVersion`. Creation omits the version; an update must send the last observed version. Admin read/write permissions and the Commerce module gate apply.

The supported unit is explicitly `box`: one dedicated box cart consumes one place, including every permitted box size and extra. The business must confirm that its authored figure safely covers this definition. There are no seeded production figures or fallback budgets. Missing capacity or an unsupported stored unit is **unknown**; an authored zero means **fully booked**. Capacity cannot be reduced below current occupancy.

Budgets are independent per delivery date, one of the alternatives allowed by the issue. A cooking run shared by several dates or capacity measured in weighted portions needs an approved operating rule before implementation. Do not represent a shared budget by copying its total to each date. Admin screens are tracked separately in #362; these endpoints support authoring now.

## Storefront contract

`GET /commerce/config/delivery/dates` retains its bounded calendar range and returns `availability` entries with `available`, `fully_booked`, `no_delivery` or `unknown`, plus `serverNowUtc`. The existing `dates` list contains available dates only. `earliestDeliveryDate` is nullable. `GET /commerce/config/delivery` returns the first eligible date with known capacity, or 404 when no truthful next date can be given, including an unknown earlier date. Both responses use `no-store` because holds immediately change availability.

The cart's guest capability or authenticated owner and current `X-Cart-Version` authorize date writes:

- `PUT /commerce/carts/{cartId}/delivery-reservation`, body `{ "deliveryDate": "YYYY-MM-DD" }`, selects or atomically replaces the date and updates the existing checkout draft.
- `GET` on the same route returns the current reservation, cart version and server time. It does not extend the hold.
- `DELETE` releases an unpaid selection and clears the draft date. Payment-pending and committed reservations cannot be deleted through this route.

Saving a changed date through the existing checkout-draft endpoint uses the same reservation logic. If the new date is full or unknown, the previous hold and draft remain unchanged. Replaying the same active selection does not extend it. After expiry, explicitly selecting again is required; unrelated draft edits never renew scarce capacity. Expiry preserves the box and checkout details.

A reservation lasts 15 minutes from active selection. The server returns its timestamps so the browser can display a timer. Checkout revalidates calendar eligibility and requires that cart's own active hold, including when it owns the last place. The first payment start freezes a deadline ten minutes later, necessarily within 25 minutes of selection. Retrying checkout cannot extend that deadline. There is no second customer pressure timer during payment.

`commerce.delivery_date_full` is a 409 suitable for “this date just filled.” `commerce.delivery_availability_unknown` is a blocking 503. Expired or conflicting reservations return distinct 409 codes. Unknown availability must not enable payment. Responses expose no other cart's reservation data.

## Payment recovery and consistency

Capacity changes share the cart transaction with its draft or checkout state. The capacity row's native SQL Server rowversion arbitrates competing occupancy changes; occupancy is counted from reservations, with no duplicate mutable counter. Expired unpaid holds stop counting. Payment-pending and committed reservations continue counting regardless of elapsed time.

Checkout freezes the reservation, attempt, date and payment deadline before entering Finance. The existing Finance intent stores that immutable deadline and refuses to start a new provider request after it. A missing intent is not proof of cancellation: recovery submits the original frozen command, allowing Finance to durably cancel a still-unstarted attempt while racing delayed creation safely. Already-started requests use the existing provider idempotency and reconciliation path.

After the deadline, payment responses withhold checkout URLs/client secrets. A pending or unknown attempt remains locked with its stock and delivery place. Finance must report capture or confirmed unpaid closure before Commerce commits or releases the exact reservation. Capture uses the existing paid-order completion transaction. Recovery preserves the same pending order and requires a new active date selection before another attempt. Invoice-backed recovery still requires staff assistance.

`Quartz:ScheduledJobs:DeliveryReservationSweep` runs each minute by default. It pages due holds in batches of 50, skips Commerce-disabled tenants, and uses a fresh tenant scope per cart. It expires unpaid holds without refreshing user activity, and reconciles payment deadlines through the same recovery code as the customer endpoint. Uncertain provider results remain reserved and are retried on a later sweep. Failures log reservation/tenant references and error type, without provider payloads or customer details. The worker must run alongside the API and existing payment-event workers.

## Bank holidays

`GET /commerce/admin/fulfilment-calendar/bank-holidays/{region}` previews the official fixed GOV.UK feed for `england-and-wales`, `scotland` or `northern-ireland`. The business chooses its applicable region and saves chosen closures through the existing fulfilment-calendar `blackoutDates` field. Preview never overwrites manual closures. The request is bounded and unavailable data is reported as unavailable; checkout does not depend on a live holiday HTTP request.

Before launch, author actual capacity, verify its box definition, configure approved calendar/closures and coverage, run the sweep, and exercise the last-slot and payment-timeout flows with the configured Stripe sandbox. Code tests do not establish real kitchen capacity or courier availability.
