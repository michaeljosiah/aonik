# Arke editorial review — issue #400

Delivery: [#400](https://github.com/michaeljosiah/aonik/issues/400), under [Spec 100](../specifications/100.arke-player-publishing.html) and [epic #397](https://github.com/michaeljosiah/aonik/issues/397). This follows the [private intake](arke-publishing-intake.md).

An assigned independent editor reviews a verified immutable revision and records **approve**, **request-changes** or **reject**. Approval enters **approved**, retained for later publication. The separate [public delivery and staff workspace](arke-publishing-delivery.md) implements #401 and #402, including matching editable artboards. Approval itself still never changes the public catalogue.

## Authority and channel setup

Every review endpoint requires the dedicated `PublishingReviewPolicy`, which checks authentication and `Publishing.Review`. The service independently checks authenticated user/tenant, the enabled Publishing module, explicit permission, active primary party, **all active party links**, channel assignment and review policy configuration. Generic agent proposal approval and generic admin/read-only roles confer no editorial authority.

The editor must differ from the originating submission user and the current revision's author. None of their linked parties may be the creator party. Switching primary parties does not evade this rule. Review detail, decisions and private package downloads apply this rule; queues exclude that editor's own submissions. Foreign tenants and inaccessible channels/submissions return 404. Permission/identity failures return 401/403.

`Publishing.Review` is in both normal permission catalogues. It is deliberately absent from default TenantAdmin and Operations grants. Assign it through role management and configure named editor user IDs in the intended publisher channel:

```json
{
  "Publishing": {
    "Channels": {
      "arke-worlds": {
        "TenantId": "<publisher-tenant-guid>",
        "Enabled": false,
        "NamespaceAdmissionEnabled": false,
        "MaxPackageBytes": 134217728,
        "RightsDeclarationVersion": "<current-distribution-statement>",
        "ReviewEnabled": false,
        "ReviewPolicyVersion": "<owner-qualified-editorial-policy>",
        "ReviewerUserIds": ["<independent-editor-user-guid>"]
      }
    }
  }
}
```

Review defaults closed. `ReviewEnabled` is separate from new receipt/upload admission (`Enabled`): an operator can pause new intake while allowing qualified editors to finish an existing queue. Current size, rights, namespace and verifier compatibility remain mandatory. Removing permission, channel assignment or an active party link revokes subsequent review access. Public delivery needs its own authority in #401.

## Frozen editorial material and evidence

Creator declarations now accept optional `editorialMaterial`. Existing declarations without it retain their exact v1 replay digest. Declarations with it use request digest v2; every admitted field is bound. Nothing mutates an existing declaration in place.

```json
{
  "editorialMaterial": {
    "audience": "children",
    "ageRating": "Ages 7+",
    "releaseKind": "world",
    "artworkAssetId": "cover",
    "attribution": "Original text and illustrations by the creator",
    "rightsEvidence": [{
      "rightsBasis": "Original work owned by the creator",
      "evidenceReference": "<private evidence reference available to the editor>",
      "assetIds": ["cover", "book", "narration"]
    }]
  }
}
```

Audience is `general`, `children` or `adults`; release kind must match the verified world/guide/part/production/story/comic kind. Attribution and age rating are bounded non-empty text. Approval requires selected artwork to be a verified PNG/JPEG inventory asset. Rights evidence contains bounded bases/references and must cover **exactly every inventory asset**, including documents and media. Multiple evidence records can apply to one asset. Unknown or uncovered assets block approval.

References identify private evidence the editor must actually inspect through its authorised source. A reference or creator grant is not an automated finding that the creator owns the work. This service does not fetch arbitrary URLs or expose those references publicly. The mandatory human checks record the editor's assessment of distribution rights, attribution and audience safety. If the underlying evidence is unavailable or insufficient, request changes or reject.

The verifier now retains the owning reader's manifest projection, schema version and SHA-256 of exact `world.json` bytes, alongside complete asset verification. This covers production IDs/titles/attribution, guide/part information and all embedded artwork choices. Older validation reports without that presentation evidence cannot receive a review snapshot. Submit a replacement revision and run fresh validation with the updated qualified worker.

## Review journey and HTTP contracts

1. `GET /publishing/review/submissions?channelKey=arke-worlds&status=in-review&page=1&pageSize=25` lists the editor's independent authorised queue. Channel is required; supported filters are in-review, held, validating, changes-requested, rejected and approved. Pagination is bounded.
2. `GET /publishing/review/submissions/{id}` returns the current receipt/version, validation findings, private review snapshot, blocking codes and decision history. Missing/failed/incompatible verification blocks decisions. Missing rights/artwork material blocks approval while still allowing a verified revision to be returned for changes or rejected.
3. `GET /publishing/review/submissions/{id}/revisions/{revisionId}/package` downloads the exact retained private revision. It requires the same independent editor authority, rechecks archive identity and records an append-only access entry. Responses use attachment/no-store/nosniff/sandbox headers. Historical retained revisions remain scoped; uploaded active content is not rendered by this API.
4. `POST /publishing/review/submissions/{id}/decisions` records a decision using the exact snapshot digest and current version.

```json
{
  "operationId": "<stable-decision-uuid>",
  "revisionId": "<reviewed-revision-uuid>",
  "reviewDigest": "<64-lowercase-hex-snapshot-digest>",
  "expectedVersion": "<base64-SQL-rowversion-from-review-detail>",
  "decision": "approve",
  "internalReason": "Editorial assessment retained for authorised staff",
  "creatorMessage": "Your edition has been approved for the next step",
  "checks": {
    "distributionRightsReviewed": true,
    "attributionReviewed": true,
    "audienceSafetyReviewed": true
  }
}
```

`decision` is `approve`, `request-changes` or `reject`. Internal reason is always required. A creator message is mandatory for changes/rejection; approval may omit its text. All three checks must be explicitly true for approval. Unknown body fields, including invented actor, approved-state or publication flags, are rejected. Actor and publisher authority are resolved on the server.

Creation/replacement request bodies are limited to 128 KiB, normalized editorial material to 100 KiB and decision bodies to 16 KiB. Evidence permits up to 20 records with bounded text and asset references. These limits supplement the separate upload quotas.

Keep the operation UUID **and all arguments** unchanged when retrying a lost response. Identical authenticated retries return the stored outcome. Reusing the UUID with different arguments or another actor gives 409. Replay after a later replacement returns historical `isActive: false`; it never reapplies the old decision. `isActive` describes the current revision/decision pointer, not permission to bypass later publication-policy checks.

The service recomputes current evidence and policy rather than trusting a browser-supplied digest. A decision also requires the snapshot to have been opened, a current exact passing report, compatible namespace/part lineage, an unchanged rowversion and status `in-review`. Stale revisions/digests/versions give 409; missing required evidence/checks give 422. No partial decision or state advance is saved on refusal.

## Snapshot, transaction and audit invariants

The immutable review snapshot binds publisher/channel/creator and authorship, declaration digest/version and all public metadata, audience/rating/kind/attribution, rights evidence and grant version, selected artwork and **all** inventory hashes, exact ZIP hash/length, parsed manifest/production identities and manifest hash, Player edition fingerprint, base/part identity, validation report/completion/provenance and current review/verifier policy. The ZIP hash, Player fingerprint and review digest remain distinct.

Snapshots are deterministic and uniquely identified by tenant/revision/digest. A policy change produces a different digest; it does not rewrite the old snapshot. The current snapshot, exact revision and SQL rowversion are rechecked under a serializable transaction. Decision identity, actor user/party, expected version, checks, scoped reasons, policy version and decision time are appended together with the submission status and active decision pointer.

Normal writes through both module and canonical contexts reject edits/deletes to decisions, snapshots and package access history, edits to completed validation evidence, and edits/deletes to frozen declarations. Only operational upload/validation fields may advance on a revision. SQL foreign/unique keys enforce tenant/revision/snapshot identity and operation uniqueness. Native SQL rowversion arbitrates concurrent editors and replacement requests; the InMemory provider generates equivalent test tokens but does not substitute for SQL concurrency evidence.

Approval currently records an **approved hold**. No public storage work or publication command is offered by this decision contract. #401 must admit the exact active approval and publication operation/outbox atomically and recheck current holds/policy before public catalogue commit. It must not infer publication authority from a status string alone.

## Creator feedback and replacement revisions

`GET /publishing/submissions/{id}/decisions?page=1&pageSize=25` returns only the owning creator's decision/revision IDs, kind, creator message and time. Internal reasons, reviewer user/party and private staff history are excluded. Other creators and foreign tenants receive 404.

`POST /publishing/submissions/{id}/revisions` accepts:

```json
{
  "operationId": "<stable-replacement-uuid>",
  "expectedRevisionId": "<current-revision-uuid>",
  "expectedVersion": "<version-from-own-current-receipt>",
  "declaration": {
    "clientRequestId": "<same-stable-replacement-uuid>",
    "channelKey": "arke-worlds",
    "...": "all normal immutable declaration fields and optional editorialMaterial"
  }
}
```

The abbreviated declaration above illustrates nesting; send the complete normal declaration without an ellipsis property. The same channel, current rights/size policy, ownership and explicit creator write authority are required. The service appends an increasing revision, resets the active decision, returns to `awaiting-upload` and copies **no** uploaded/validated/approved authority. Even metadata-only edits require this fresh upload and validation. Replacement during validation or a future publication operation is refused. Retained revisions are bounded to 100 per submission.

Identical replacement retries return the current receipt; conflicting operation arguments fail. Original receipt creation still compares the first declaration digest and returns the current revision on replay. Old snapshots, decisions, reports and retained uploaded packages remain historical evidence; existing incomplete uploads retain their bounded expiry/cleanup ownership. A concurrent replacement and approval can have only one winner for the expected version.

## Validation and rollout

Service and HTTP coverage includes permission/channel/tenant isolation, originating-user and non-primary-party self-review, exact versions/digests/policies, missing validation/presentation/rights/artwork/checks, immutable history, creator message privacy and fresh replacement/replay. SQL tests cover duplicate/conflicting/competing decisions, replacement-versus-approval, real insert-failure rollback and canonical audit immutability. The pinned native worker fixtures verify manifest evidence as well as complete assets.

Exact build/test/migration results are recorded in the [delivery record](arke-publishing.md). Production channel/editor policy qualification, real container isolation and consenting-identity acceptance remain #403. Existing manual/private review remains available while deployments and the staff UI are qualified.

Local evidence: a complete solution run with **5,263 passed / 12 existing skips**, **124 final focused Publishing/permission tests**, **22 pinned-worker fixtures**, a final build with zero errors and a clean canonical migration application/model check. The complete run used four test workers after a default-concurrency run encountered HTTP timeouts. The final focused run covers the subsequent save-overload audit hardening.
