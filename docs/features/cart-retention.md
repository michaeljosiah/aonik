# Box draft retention

The existing `BoxCartAbandonSweepJob` abandons inactive, open box drafts. A box with no retained dishes expires after 24 hours of inactivity; a box containing dishes expires after 7 days. Extras alone do not count as dishes. Unavailable dishes still count while they remain in the box.

The global settings are:

| Setting | Default | Accepted values |
| --- | --- | --- |
| `Commerce.Carts.EmptyAbandonAfterHours` | `24` | Whole hours, 1–8760 |
| `Commerce.Carts.AbandonAfterDays` | `7` | Whole days, 1–365 |

Existing explicit `AbandonAfterDays` overrides remain effective. Missing, malformed or out-of-range values use the defaults. Settings resolve through the existing global settings/configuration/default chain; tenant overrides are not used by this background policy. These server settings are not public storefront configuration.

The default schedule is hourly at minute 10 (`Quartz:ScheduledJobs:BoxCartAbandonSweep:CronExpression`). A configured schedule takes precedence. Expiry is housekeeping, so the actual transition occurs on the first successful sweep after the inactivity threshold, rather than a customer-facing countdown.

`Cart.LastActivityAtUtc` records meaningful successful customer changes. Reads, quote refreshes, catalogue repairs and background maintenance do not renew it. Legacy rows fall back to `UpdatedAt`, then `CreatedAt`; a server-only edit preserves that fallback before changing the audit timestamp. Removing the final dish preserves the same active box and starts its empty-box inactivity window from that edit.

The worker retains its Commerce module gate and tenant isolation. It excludes deleted carts, generic carts, closed carts and every cart already linked to an order, including pending or uncertain payments. If a customer edit or checkout wins the native cart-version race, the sweep skips that cart without retrying abandonment against its newer state. Other eligible carts can still be processed.

Abandonment changes active eligibility; it does not release payment or delivery holds, delete order snapshots, or define a separate personal-data purge policy. Reservation expiry and payment-provider finality remain separate concerns. The draft must survive an expired delivery reservation, and an uncertain submitted payment must be resolved before offering another payment.

This supersedes Spec 068's original single 14-day draft window.
