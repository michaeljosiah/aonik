# Content safety over HTTP

The safety half of what a child-facing product host needs from the platform (aonik#323 for
ArkeKidz#10; Spec 096): screen what a child typed before a model sees it, screen what a model
produced before the child does, hold the youngest bands' output for the guardian's own review, and
record every decision with the hash of exactly what was judged — so a product can prove, at the
moment of delivery, that the bytes in hand are the bytes that passed.

All routes require the `UserPolicy` and act as the caller's **party**. The subject is a child the
caller holds guardian authority over, or the caller themselves; anyone else's child answers
`404 ward-not-found`, never `403`. Refusals share one shape,
`{ "status": 403, "code": "consent-required", "message": "…" }`.

| Status | Code | When |
|---|---|---|
| 403 | `no-party` | The signed-in user has no party. |
| 403 | `consent-required` | The subject's `safety-classification` consent does not stand (Spec 095 §12.3: classification is egress). |
| 404 | `ward-not-found` | The subject is not the caller's ward, or does not exist — the same answer. |
| 404 | `decision-not-found` | No such decision, or one about somebody else's child. |
| 422 | `invalid-request` | Not text, no layer, no content. |

## The one supported route

Classification goes through `ISafetyClassificationProvider`, and this slice ships one: OpenAI's
moderation endpoint (`omni-moderation-latest`), registered when `ContentSafety:OpenAI:ApiKey` is
configured and **not otherwise** — a gate with no classifier refuses, it does not pass through. The
route a subject's classification takes must be one their consent terms name
(`ConsentTermsVersions.NamedProviders`); a route the terms do not name is refused as
`check-unavailable`, never substituted. Its taxonomy lands on Spec 096's categories — sexual;
sexual/minors → csam; violence and violence/graphic → graphic-violence; self-harm/* → self-harm;
hate/* and harassment/* → hate — taking the highest score of the provider categories mapped onto
each. **`frightening` is not scored by this route**: no provider taxonomy carries it, and the spec
says so. What stands for it is the structural constraint of the youngest bands (L1) and the
guardian's review (L5), which holds output for them by default under 10.

The catalogue decides the model: an `AiRoutePolicy` for use case `safety-classify-text` whose
primary model belongs to the `openai` provider. Without one, the gate answers `check-unavailable`.

## Calls

### `POST /safety/screen`

```json
{ "subjectPartyId": "7c1e…", "modality": "text", "layer": "output",
  "content": "Pip crept along the harbour wall…", "generationRunId": null, "usageReservationId": null }
```

`layer` is `input` — what the child typed, screened before a model sees it (L2) — or `output` —
what a model produced, screened before the child does (L4). `200`:

```json
{ "decisionId": "…", "allowed": false, "outcome": "held-for-review", "categories": [],
  "contentHash": "3f2a…", "safetyBand": "under-6", "policyVersion": "builtin-1", "pendingReviewId": "…" }
```

`outcome` is one of `allowed`, `blocked` (with `categories`), `held-for-review` (an output the
child's band holds for the guardian; `pendingReviewId` names the review), `check-unavailable` (the
classifier could not run, or the route is not one the terms name — a refusal, on the record) and
`modality-disabled`. `contentHash` is the SHA-256 of exactly the content judged, lowercase hex: the
one thing a product should keep beside its copy of the content. An input judged reportable is
preserved in the platform's file store before the refusal is answered (Spec 096 §12).

### `GET /safety/decisions/{decisionId}`

The decision as recorded: subject, band, modality, layer, outcome, categories, policy version,
`contentHash`, when, and the review it is held for — `{ pendingReviewId, state, heldAt, expiresAt,
decidedByPartyId, decidedAt }` with `state` one of `pending`, `approved`, `declined`, `expired`.
What a delivery check reads back before trusting a product's own copy.

### `POST /safety/decisions/{decisionId}/review`

```json
{ "decision": "approve" }
```

`200` `{ "decisionId": "…", "outcome": "approved", "contentHash": "3f2a…" }`. The guardian's own
review of content held for them (L5). `approve` is what lets the product deliver; `decline` leaves
it undeliverable. Already decided, expired, or not this caller's ward: `"not-available"`.

## What a host does with these

Before asking a model for a child, screen the instruction as `input`; blocked, refuse the request
and say why in the child's own terms. When the model answers, screen the body as `output` before
staging it anywhere the child could read it; keep `decisionId` and `contentHash` with the content.
`held-for-review` means the guardian decides — in a product where a parent accepts every draft,
their accept is that review, and the product calls `…/review` with `approve` as them. At delivery
to the child, hash the exact bytes and compare with the decision's `contentHash`: equal and the
decision `allowed` or `approved`, deliver; anything else, do not.

## Not in this slice

Images and video are not screened over this route (`modality` must be `text`), though the
moderation adapter can score an image URL and the gate has an image classifier; the delivery
permit for generated images is the next call, alongside the `frightening` category, which needs
either a route that scores it or a curated model of its own.
