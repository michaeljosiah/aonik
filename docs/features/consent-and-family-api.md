# Guardian consent and family groups over HTTP

The identity half of what a child-facing product host needs from the platform (aonik#326, #328;
Spec 095, Spec 086): who the caller's children are and what consent stands for each, how a child
comes to exist at all, and the family group that holds them. First consumer: the Arke Kidz host
(ArkeKidz#8), which resolves a parent's session into a family scope and refuses anything a
child's consent does not cover.

All routes require the `UserPolicy` (a signed-in user of the tenant) and act as the caller's
**party**, resolved server-side. The operator routes require `AdminWritePolicy`. Refusals share
one shape, `{ "status": 404, "code": "ward-not-found", "message": "…" }`, with stable codes.

## The shape of it

```
operator ──attests──▶ guardian ──enrols──▶ child (party + guardian edge + service-core grant)
                                   │
                                   ├──grants / withdraws──▶ purposes (generation-disclosure, voice, …)
                                   │
                                   └──creates family group, adds child──▶ the family Group
```

Verification is never a flag the caller sends. Enrolment and every further grant resolve an
accepted route for the jurisdiction (payment mandate, signed form; government ID stays switched
off), run it, and record the attempt — success or failure — before anything else happens.

## Consent

### `GET /consent/wards` · `GET /consent/wards/{childPartyId}`

The children the caller holds active guardian authority over, with the consent that stands:

```json
{
  "childPartyId": "7c1e…", "displayName": "Ivy",
  "consentBand": "under-consent-age", "safetyBand": "early-years",
  "consentAgeOn": "2034-05-04T00:00:00Z", "majorityOn": "2039-05-04T00:00:00Z",
  "purposes": [
    { "purpose": "service-core", "termsVersion": "2026-09", "verificationMethod": "signed-form",
      "grantedByPartyId": "9a1e…", "grantedAt": "2026-09-18T09:12:00Z", "expiresAt": null }
  ]
}
```

`purposes` lists **active** grants only — unrevoked, unexpired. A product gates on this list:
`service-core` to touch the child's world at all; `generation-disclosure` before a child's words
leave the device for a model; `safety-classification` before they leave for a classifier
(Spec 095 §12.3). Any party who is not the caller's ward answers `404 ward-not-found`, the same
body as for a party that does not exist.

### `POST /consent/wards` — enrol a child

```json
{ "displayName": "Ivy", "dateOfBirth": "2021-05-04", "termsVersion": "2026-09",
  "jurisdiction": "GB", "purposes": ["generation-disclosure"] }
```

`201 Created`, `Location: /consent/wards/{childPartyId}`:

```json
{ "childPartyId": "7c1e…", "enrolmentAttemptId": "…", "verificationMethod": "signed-form",
  "consentAgeOn": "2034-05-04T00:00:00Z", "majorityOn": "2039-05-04T00:00:00Z", "safetyBand": "early-years" }
```

One transaction creates the child party, the caller's guardian edge and the `service-core` grant
plus any purposes named (nothing is pre-ticked). There is no other way to create a child. When no
accepted route can verify the caller, or the route refuses, the answer is `403 guardian-not-verified`
and **nothing is created** — only the verification attempt is recorded. The date of birth is
attested and exact; only the birth year is kept (Spec 095 §6). `jurisdiction` is a country code
and decides which routes are accepted; unmapped codes take the strict default.

### `POST /consent/wards/{childPartyId}/purposes` — grant a purpose

```json
{ "purpose": "generation-disclosure", "termsVersion": "2026-09", "jurisdiction": "GB" }
```

`200` `{ "purpose": "generation-disclosure", "verificationMethod": "signed-form" }`. The caller is
verified again through the same route resolution — a mandate that lapsed or an attestation that
expired stops supporting new grants. The same terms version re-granted is idempotent; a new
version supersedes the old grant atomically. Not the caller's ward: `404`. Route unavailable or
refusing: `403 guardian-not-verified`. Unknown purpose: `422`.

### `DELETE /consent/wards/{childPartyId}/purposes/{purpose}` — withdraw

`200` `{ "purpose": "…", "withdrawn": true }`. Any single active guardian may withdraw, and it takes
effect on the next operation regardless of other guardians (Spec 095 §7.1). Withdrawing
`service-core` withdraws the child's participation.

### `POST /consent/wards/{childPartyId}/guardians` — a second guardian

```json
{ "guardianPartyId": "b2d0…", "jurisdiction": "GB" }
```

`200` `{ "childPartyId": "…", "guardianPartyId": "…" }`. The caller — an existing active guardian —
authorises the addition; the **new** guardian is the one verified, through the same route resolution
and to the same standard as the first (Spec 095 §7), so a second parent added on a weaker basis
cannot dilute the first. From then on each guardian acts independently: grants, withdrawals, and the
child's workspaces (below). Not the caller's ward: `404`. The new guardian unverifiable: `403
guardian-not-verified`. The caller naming themselves: `422`. Already a guardian: `200`, nothing changes.

### Operator: `POST /admin/consent/attestations` · `DELETE /admin/consent/attestations/{id}`

The manual half of the signed-form route (Spec 095 §8): a named operator records that a returned
form was read and matched to the guardian party.

```json
{ "guardianPartyId": "9a1e…", "evidenceRef": "case-4411", "notes": "Matched to passport copy" }
```

`201` `{ "attestationId": "…", "guardianPartyId": "…" }`. The attestation is audited under the
operator's user, expires after `Consent:SignedFormAttestationDays`, and is what makes the
signed-form route *available* to that guardian. Revoking it (`DELETE` with `{ "reason": "…" }`,
`204`) stops it supporting new grants; grants already made stand, because lawfulness is judged at
the time of processing. A guardian cannot attest themselves: the route is admin-only.

### Operator: `POST /admin/consent/terms` · `GET /admin/consent/terms`

The terms a guardian's grant names (Spec 095 §10.2), and the processors those terms disclose
(Spec 096 §16). A grant carries a `termsVersion`; the classification route a child's content may
take is one the version names. So a tenant publishes a version before any family can consent to
anything, and this is where it is written:

```json
{ "version": "kidz-2026-09", "namedProviders": ["openai"], "affectedPurposes": [] }
```

`201` `{ "version": "kidz-2026-09", "namedProviders": ["openai"], "publishedAt": "…", "isCurrent": true,
"revokedGrants": 0 }`. The version becomes the tenant's current terms; whatever was current before is
not. `affectedPurposes` names the purposes the change is material to: every active grant under
another version is **revoked at publication** for each of them, and processing stops for those
families until they re-consent — that is the point, not a side effect. Publishing a version again
updates what it names and revokes nothing that already names it. `GET` lists the versions, newest
first, and is any signed-in user's: what a family consents to is not a secret from them.

## Family groups

### `POST /groups` · `GET /groups/mine` · `GET /groups/{groupId}`

```json
{ "kind": "family", "name": "The Harpers" }
```

`201` with the Spec 086 `GroupDto` — `id`, `kind`, `name`, `members[]` of `{ id, groupId,
partyId, userId, role, invitationStatus, … }` — the caller's party as `Owner`. `GET /groups/mine`
lists the groups the caller is an accepted member of; `GET /groups/{id}` answers `404` for any
group the caller is not a member of, foreign or absent alike.

### `POST /groups/{groupId}/members` — add a member without a login

```json
{ "partyId": "7c1e…", "role": "viewer" }
```

A child has no user, so they are **added** (`200`, the `GroupMemberDto`); a person with a login
must be invited through Spec 086's invitation routes instead (`409 invalid-state` here). Requires
the caller to be an owner or manager of the group. Enrolment does not add the child to a group
itself — Platform cannot write Groups' tables in the same transaction (see `ConsentService`) — so
this is the product's second step after `POST /consent/wards`.

## What a host does with these

A parent signs in; `GET /v1/me` gives their party. `GET /groups/mine` gives the family (the scope
every world lives in, and the subscriber that will pay). `GET /consent/wards` gives the children
and, per child, the purposes that stand — which is what the host's policy checks on every call,
and what it re-checks so a withdrawal takes effect on the next request rather than the next sign-in.

A child's world is then created **as the child's**: `POST /workspaces` with `ownerPartyId` naming
the ward. The Workspaces resolver accepts any active guardian of the owner (Spec 095 §12), so a
second parent added through `POST /consent/wards/{id}/guardians` opens and writes the same world
without a share grant, and a withdrawn `service-core` leaves every guardian read-only at the
platform, not only at the product. See `workspace-sync-api.md`.

## Not in this slice

The payment-instrument route is wired and now works on a real database (this change fixed the
Finance context's mapping of `AnkPaymentMandates`), but its *provenance* — proving how a trusted
mandate entered the system — is #328's launch decision, not something these endpoints assert.
The age-up transition (Spec 095 §7.3) has no route yet: a young person reaching `consentAgeOn`
still cannot take over their own grants over HTTP.
