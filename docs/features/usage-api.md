# Metered usage over HTTP

The allowance half of what a child-facing product host needs from the platform (aonik#326 for
ArkeKidz#9; Spec 087 §7): hold part of a family's allowance before paid work starts, commit what
the work actually used when it finishes, give the hold back when it does not — and never charge
the family twice for a request whose response was lost. First consumer: the Arke Kidz host, which
reserves one `chapter-drafts` before it asks a text model for a chapter and commits it only once
the draft is staged.

All routes require the `UserPolicy` (a signed-in user of the tenant). Authorisation is the
**meter's**: every call names or implies a subscriber, and the meter's authorizer for that kind
decides whether the caller may act for it — a party for itself, a group's accepted members for the
group. Refusals share one shape, `{ "status": 402, "code": "allowance-exceeded", "message": "…" }`.

| Status | Code | When |
|---|---|---|
| 402 | `allowance-exceeded` | The subscriber's open grants cannot cover the quantity. |
| 403 | `forbidden` | The caller may not act for the subscriber (or no such subscriber). |
| 404 | `reservation-not-found` | No such reservation, or one the caller may not act on — the same answer. |
| 404 | `no-subscription` | The subscriber has no active subscription. |
| 409 | `invalid-state` | Committing a released or lapsed reservation, or more than was held. |
| 422 | `invalid-request` | A malformed request. |

## The lifecycle

```
POST /usage/reservations ─▶ held ─┬─▶ POST …/{id}/commit ─▶ committed  (consumed; the rest of the hold returned)
                                  ├─▶ POST …/{id}/release ─▶ released  (nothing consumed)
                                  └─▶ (the hold period passes) ─▶ expired
```

A meter of kind `counter` is what this lifecycle draws on (Spec 087 §6). A `ceiling` is claimed
and released whole — the workspace routes do that — and never reserved.

### `POST /usage/reservations` — reserve

```json
{ "subscriber": { "kind": "group", "id": "…" }, "meterCode": "chapter-drafts",
  "quantity": 1, "idempotencyKey": "chapter-draft:2f4c…", "holdForSeconds": 900 }
```

`200` with the reservation **as it stands**:

```json
{ "reservationId": "…", "subscriber": { "kind": "group", "id": "…" }, "meterCode": "chapter-drafts",
  "quantity": 1, "status": "held", "expiresAt": "2026-09-18T12:45:00Z" }
```

The same `idempotencyKey` returns the same reservation whatever its state — held, committed or
released — so a product that lost the response asks again and holds nothing twice. The key is
the product's own name for the piece of work; the Kidz host uses the engine's operation key. The
hold lapses on its own after `holdForSeconds` (the meter's default when omitted), which is what
protects the family from a host that died between reserving and committing.

### `GET /usage/reservations/{id}`

The reservation as it stands. One the caller may not act on answers `404` whether or not it exists.

### `POST /usage/reservations/{id}/commit` — the work happened

```json
{ "actualQuantity": 1, "sourceType": "chapter-draft", "sourceId": "…",
  "providerCost": 0.0031, "providerCostCurrency": "USD" }
```

`200` `{ "reservationId": "…", "status": "committed", "usageRecordId": "…", "quantityCommitted": 1,
"replayed": false }`. Consumes `actualQuantity` (at most what was held) from the grants the hold was
taken against, in draw-down order, and returns the rest. **Committing a reservation already
committed is a replay**: `200` with `"replayed": true`, nothing consumed again — the answer for a
lost response. A released or lapsed reservation answers `409 invalid-state`.

### `POST /usage/reservations/{id}/release` — it did not

`200` `{ "reservationId": "…", "status": "released" }`. Returns a held quantity. Idempotent: a
reservation that is not held — committed, released, lapsed — changes nothing and answers with its
state.

### `GET /usage/allowance?subscriberKind=group&subscriberId=…`

What the subscriber's plan grants and what is left, meter by meter, for a product to show before
it asks:

```json
{ "subscriber": { "kind": "group", "id": "…" }, "planCode": "family-monthly", "planName": "Family",
  "status": "active", "currentPeriodStart": "…", "currentPeriodEnd": "…",
  "meters": [ { "meterCode": "chapter-drafts", "kind": "counter", "unit": "drafts", "allowance": 30,
                "consumed": 4, "held": 1, "remaining": 25, "resetPolicy": "period", "resetsAt": "…" } ] }
```

## What a host does with these

Before a paid operation, the host reserves against the family group with the operation's own key.
If the answer is `402`, it refuses the operation before anything is spent and tells the family.
When the operation completes, it commits — retrying the commit on a lost response is safe. When it
fails, it releases. A host that dies in between leaves a hold that lapses. Nothing here is a
substitute for the operation's own idempotency: the engine's operation record decides whether a
retried draft is a replay; the reservation decides whether it is paid for twice.

## Not in this slice

Periodic reset of counters is the catalogue's (`resetPolicy`); the routes do not expose the
ledger or purchases of extra allowance (Spec 087 §8), and there is no admin route to adjust a
family's grants.
