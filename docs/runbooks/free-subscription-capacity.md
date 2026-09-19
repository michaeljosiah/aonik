# Refreshing free subscription capacity

POST `/subscriptions/admin/{subscriptionId}/refresh-free-capacity` with `expectedVersionId` and `targetVersionId`.

The caller needs AdminWritePolicy and billing management access to the subscriber. The target must be an effective, published version of the same zero-price, non-renewing plan and currency. Existing entitlements cannot be removed, ceiling limits cannot decrease, counter and flag allowances cannot change, and only ceilings may be added. Pending changes or cancellation prevent the refresh.

This updates the subscription's pinned version only. It does not materialise grants or create a billing period. Replaying an already-applied target is safe. A stale expected version is refused. The relational operation uses a serializable transaction and reloads persisted subscription state on execution-strategy retries.

Kidz development uses this to add one workspace and 100,000,000 bytes to Peek while preserving its single lifetime story. These are development capacities, not public pricing commitments.
