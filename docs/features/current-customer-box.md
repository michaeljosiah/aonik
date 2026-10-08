# Current customer box

Issue #348 adds account recovery to the existing box cart and explicit choices when signing in with a guest box. It reuses `Cart`, its native row version, party resolution, box pricing and catalogue drift handling. There is no new session table or migration.

## Read and activate

`GET /commerce/carts/box/current` requires a signed-in customer. The server resolves the party from the principal; a request cannot select another customer. It returns the existing `BoxCartDto`, including `cartVersion`, quote and any drift notices. It never returns a guest token. No linked party or current box returns `404`; the read creates neither a party nor a cart.

A current box belongs to the tenant and party, has a box bundle, is `Open`, has no `OrderId`, and is not deleted. A committed empty box remains current, including after removing its last dish. Pending-payment carts are frozen and excluded. A read does not count as customer activity, although existing catalogue drift repairs can update cart metadata.

Creating a box is an explicit commitment through the existing `POST /commerce/carts/box` route. Browsing Step 1 must not call create. A carried first dish uses the existing atomic `FirstLine` field. Creating another account box returns `409 commerce.active_box_exists`; the client should resume the saved box rather than silently add the carried dish again.

All `/commerce/carts` responses carry `Cache-Control: no-store`, including authentication, validation and conflict responses.

## Choose a whole box at sign-in

The existing `POST /commerce/carts/{guestCartId}/adopt` still accepts an empty body when there is no competing account box. It requires both the authenticated account and possession of the guest cart token. Since #347, a new empty-body adoption also requires the observed `X-Cart-Version`; explicit choices below retain their two body versions. Unknown carts, wrong tokens, foreign owners and other tenants remain indistinguishable `404` responses.

When a different account box exists, a current-version empty-body request returns `409 commerce.box_choice_required` without changing either cart. The response includes `guest` and `savedCandidates` summaries with `cartId`, `cartVersion`, `boxSize`, `lineCount` and `lastActivityAt`. Since #347 the timestamp is meaningful user activity, falling back to the update/creation timestamp for legacy carts.

Resubmit the same route with the guest token and JSON:

```json
{
  "decision": "KeepGuest",
  "expectedSavedCartId": "<saved cart ID shown in the conflict>",
  "expectedSavedCartVersion": "<saved cartVersion>",
  "expectedGuestCartVersion": "<guest cartVersion>"
}
```

`KeepGuest` adopts the guest box and archives the saved box. `UseSaved` preserves the saved box and archives the guest box. Both choices retire the guest capability and associate the archived guest record with the account. Lines, quantities, extras and selections are never merged. The response is the existing `CartDto` for the selected cart; the client can use its ID with the existing box read route.

The expected ID and both versions bind the choice to the state displayed. A stale choice returns `409 commerce.box_choice_stale`; refresh the choice before resubmitting. Checked-out carts cannot be adopted or archived. A retry of a completed decision may read its exact already-owned result without repeating the archive. It never substitutes a newer saved cart.

## Concurrency and existing duplicates

Account box creation and box adoption use a serializable SQL transaction before reading the tenant/party range, under the existing EF execution strategy. This coordinates the known writers even when the range is empty. Native cart row versions protect concurrent edits and checkout. Creation keeps a stable ID across execution retries, including an uncertain commit acknowledgement; response construction runs outside the write retry.

Older code could leave several active account boxes. Current reads and creates return `409 commerce.multiple_active_boxes` instead of choosing or archiving customer work. Adoption also refuses when more than one other saved candidate exists. Conflict responses contain at most 20 owned summaries and `hasMore`; the existing authorized cart read can inspect a known candidate. This issue does not add bulk duplicate recovery or silently archive an unshown third box.

The guarantee covers the existing box create/adopt service paths, not arbitrary direct database writes. [Checkout drafts](checkout-drafts.md) add shared form state, conditional writes and activity-based retention in #347; authoritative payment recovery remains #344. Existing plan-authoring currency checks are preserved; this change does not redesign concurrent plan authoring.

## Verification

Service and API tests cover account/tenant isolation, empty-box recovery, drift, no read-time activation, both explicit choices, stale versions, token retirement and replay. SQL Server tests exercise create/create, create/adopt, independent guest adoptions, competing owners, opposite decisions, intervening edits/checkout and an uncertain commit acknowledgement.
