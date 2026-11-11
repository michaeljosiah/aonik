# Arke publication delivery and staff workspace

Issues [#401](https://github.com/michaeljosiah/aonik/issues/401) and [#402](https://github.com/michaeljosiah/aonik/issues/402), implementing [Spec 100](../specifications/100.arke-player-publishing.html). This follows [private intake](arke-publishing-intake.md) and [editorial approval](arke-publishing-review.md).

An editorial approval does not publish anything. An independent, assigned publisher confirms the exact approved revision. A durable operation copies the reviewed archive and selected artwork into immutable public storage, verifies each actual public URL, then commits a complete Player v2 catalogue snapshot. The catalogue, edition provenance, serial, operation result and optional mirror event commit together. Failed delivery leaves the previous complete catalogue available.

## Staff journey

The deployed dev journey passed on 10 November 2026 using separate test accounts,
under the owner's acceptance of autonomous testing in place of a human walkthrough.
The actual UI exercised private inspection, request-changes/revision, approval,
hold/removal, release and withdrawal; Player installed the release and played its
retained copy after an offline cold start. Dev is back to empty serial 20.
The [runbook and exact evidence](../runbooks/arke-publishing-dev.md#deployed-reviewer-and-player-acceptance)
record the navigation/configuration fixes, rollout, preserved reading positions
and remaining #403 operational/production qualification. Production approval policy
is unchanged.

1. Open **Arke Worlds → Creator submissions**. Search the server-side queue by title, creator or world, or filter its status. The queue contains only submissions the assigned reviewer may inspect.
2. Open the current revision. **Overview** identifies the creator, audience, edition and immutable digests. **Package & preview** shows verified file identities and a privately fetched PNG/JPEG cover. Download the complete private package to inspect its contents; arbitrary EPUB, HTML and media documents are not executed inside the admin application. Package and image access are audited.
3. Inspect **Validation**, **Rights & audience** and **History**. The history separates internal editorial reasons from creator-facing messages. Upload activity and verifier results come from the service.
4. **Approve revision** requires an assessment and explicit distribution-rights, attribution and audience checks. Changes/rejection require creator feedback. The confirmation captures revision ID, review digest and server version. A stale confirmation stops until the current review is reloaded. Approval enters a hold for separate release.
5. An assigned publisher opens **Publication → Publish approved edition**, or **Player catalogue → Approved for release**. Publish-only staff can release from the catalogue without gaining access to private rights evidence or editorial assessments. The confirmation identifies the exact revision, approval and channel head, requires a reason, and separately confirms replacement of an existing base/part sequence.
6. Queue admission is displayed as **queued**, never as successful publication. While work is active, the view refreshes the operation state. **Needs reconciliation** retains the same durable command and assets. A staff retry records its reason and identity. Uncertain HTTP responses keep confirmation arguments frozen and reuse the same request UUID; changing intent requires a new confirmation.
7. The catalogue shows current and historical entries, immutable pins, source approval/revision IDs and published serials. Authorised staff may hold future releases, clear a hold, withdraw an entry or reconcile the optional legacy mirror. These are distinct confirmations with reasons. Canonical catalogue state and legacy-mirror state are displayed separately.

Native dialogs use browser focus trapping, an explicit first focus target, Escape/cancel and focus restoration. Review tabs support arrow/Home/End keys. Private state and in-flight results are discarded on user/tenant changes. There are no runtime fallback fixtures.

## Permissions and channel assignment

| Capability | Named permission | Additional assignment |
| --- | --- | --- |
| Private queue, package, image and editorial decisions | `Publishing.Review` | `ReviewEnabled` and `ReviewerUserIds` |
| Catalogue management, initialise, publish, publication retry, mirror retry | `Publishing.Publish` | `PublisherUserIds` |
| Future-publication hold/removal, withdrawal and withdrawal retry | `Publishing.Withdraw` | `PublisherUserIds` |

The Publishing module must be enabled for staff operations. Named permissions are seeded without automatic grants. An actor needs an active user/party relationship in the current tenant. Review and release reject creator-user and all linked-creator-party self-approval. Worker delivery rechecks the persisted requesting actor and the approving editor's current authority, rather than trusting the queueing request. Staff policies are `PublishingReviewPolicy`, `PublishingManagementPolicy`, `PublishingPublishPolicy` and `PublishingWithdrawPolicy`.

Public committed feeds and retained objects survive a pause of the authoring module. Disabling new delivery does not disable separately authorised withdrawals. Disabling the public channel itself stops its anonymous routes; treat that as an operational decision affecting existing Player installations.

## Qualification and configuration

Everything defaults closed. No production channel, credential, baseline or public directory is installed by this change. Before enabling delivery, the deployment owner must qualify:

- A dedicated, persistent **absolute** `Publishing:Delivery:PublicRoot`, outside `wwwroot`, private intake and generic blob/static mounts. All API/Worker instances serving the channel must share this durable volume. Back up the objects, database and `.store-id` together. Replacing the root marker changes its identity and fails closed.
- A permanent HTTPS `Channels:<key>:PublicBaseUrl`, without credentials, query or fragment. The archive/image URL is `<origin>/publishing/channels/<key>/assets/<artifact-guid>`. A proxy path prefix may be part of the qualified origin. Worker readback must reach that actual public origin with the same exact bytes, `Content-Length`, MIME type and HTTP 200. Redirects and cookie-based readback are rejected; the transport does not retain or send cookies.
- `DeliveryEnabled`, `PublicCatalogueEnabled`, a bounded `DeliveryPolicyVersion`, and the authorised publisher IDs. Delivery defaults: ten-minute claim, six attempts per cycle, at most 128 selected public pictures. Supported bounds are enforced; picture files must be PNG/JPEG and at most 4 MiB each. The complete feed is at most 1 MiB.
- The **complete authoritative current Player v2 catalogue**, byte-for-byte, in `BaselineCatalogueJson`; its lower-case SHA-256 in `BaselineSha256`; and a durable qualification reference in `BaselineEvidenceReference`. Do not initialise with an empty list when a real catalogue already exists. Initialisation preserves its serial, catalogue metadata, bundle-path picture strings and existing pins.
- For every existing world, `BaselineOwnership:<world-id>:CreatorPartyId` must identify a real party in this tenant, with `EvidenceReference`. Existing conflicting namespaces are refused. Legacy world/production identities must be qualified before import. Unidentified standalone productions cannot be silently assigned new IDs.
- To append listening parts to a legacy base, qualify `BaselineOwnership:<world-id>:ProductionChapters:<production-id>` as a list of `{ "id": "chapter-slug", "number": 1 }` covering that production's existing base chapters. An explicitly empty list asserts no base listening chapters. The ownership evidence must substantiate this projection; a missing projection refuses part release. Newly published bases derive this information from pinned verification evidence.
- The isolated verifier image/policy, private persistent store, consented identities and Worker/outbox infrastructure described in the intake guide. Native reader tests do not establish container isolation or production TLS/storage readiness.

Initialise once through the staff API after qualification. Baseline, owning tenant/channel and public origin cannot be rewritten in place. A later authoritative-baseline change is an operator reconciliation, not a reset-to-zero action. Preserve the public serial across migrations and recovery. These deployment rehearsals remain [#403](https://github.com/michaeljosiah/aonik/issues/403).

## HTTP contracts

All staff responses are private/no-store. Mutation bodies are bounded to 16 KiB and reject unknown members. UUIDs are stable client operation identities. Reusing one with different arguments or actor returns a conflict. Versions are opaque server values, not counters to construct locally.

| Method and route | Purpose |
| --- | --- |
| `GET /publishing/management/channels` | Assigned channels, capabilities, readiness and head/mirror state |
| `GET /publishing/review/submissions?channelKey=...&status=...&query=...&page=1&pageSize=25` | Private queue, total and bounded server search |
| `GET /publishing/review/submissions/{id}` | Exact review, snapshot, validation, decisions and upload activity |
| `GET /publishing/review/submissions/{id}/revisions/{revisionId}/images/{assetId}` | Authenticated, hash-checked verified image preview |
| `GET /publishing/management/channels/{key}/catalogue?page=1&pageSize=25` | Edition history, pins, serials, operations and publishable summaries |
| `GET /publishing/submissions/{id}/publication` | Exact approval, release blockers, expected lineage and operations |
| `POST /publishing/management/channels/{key}/initialise` | `{ operationId, baselineSha256, reason }` |
| `POST /publishing/submissions/{id}/publication` | `{ operationId, revisionId, decisionId, reviewDigest, expectedVersion, expectedSerial, expectedBaseFingerprint, expectedSlotFingerprint, confirmBaseReplacement, reason }` |
| `POST /publishing/publications/{id}/retry` | `{ operationId, expectedVersion, reason }` |
| `POST /publishing/submissions/{id}/publication/hold` | `{ operationId, expectedVersion, reason, hold }` |
| `POST /publishing/editions/{id}/withdraw` | `{ operationId, expectedVersion, reason }` |
| `POST /publishing/management/channels/{key}/mirror/retry` | `{ operationId, expectedVersion, reason }` |

The earlier decision, replacement and private package-download contracts remain in the review guide. The API never accepts a publisher-supplied arbitrary public URL, filesystem path, tenant or catalogue JSON in a release command.

Anonymous routes are exactly `GET /publishing/channels/{key}/catalogue.json` and `GET /publishing/channels/{key}/assets/{artifact-guid}`. Middleware resolves the tenant from the **server-configured enabled public channel**, overriding caller tenant headers/claims for these exact route shapes. No other Publishing prefix is tenant-validation exempt. Queries additionally constrain tenant and channel. Unknown/private channels and unknown assets return 404. Private intake, rights and upload paths have no public mapping. Feed responses cache for 60 seconds; immutable artifacts cache for a year with digest ETags and `nosniff`. ZIPs are attachments. A fully copied approved artifact may be fetched by its unguessable ID before catalogue commit to support public readback; it is unlisted until promotion.

## Recovery, lineage and retention

`PublicationOperationRequestedEvent` carries a tenant and durable operation identity. Worker outbox handling restores tenant context. Serializable claim and commit transactions use expiring random tokens. Every write/readback progress update is fenced by the current token. Replaced workers cannot commit or acknowledge a newer claim. A committed command is idempotent even after response loss.

Artifacts are reserved in SQL before copying. Paths use tenant/operation/artifact UUIDs. Writes stream exact reviewed length/SHA into a temporary file, flush and atomically move without overwrite. Repeated copies must match existing content. A process crash may leave a temporary upload file; reconcile storage under an operator retention procedure rather than deleting reviewed or referenced objects automatically. Normal failed writes remove their owned temporary file. Public descriptors and committed snapshots/provenance are immutable through canonical and module persistence.

At final commit, the worker reloads the active revision/decision, policy, rights, holds, actor memberships/permissions, target slot and base fingerprint. It checks all public objects have passed readback. Unrelated world changes can merge onto the latest complete snapshot at a higher serial; changes to the expected base or slot stop the command. New parts require the exact base fingerprint/edition and next global part number, a real base production, and no reused listening chapter IDs or numbers. Competing next-part commands cannot both advance the sequence. Restoring a retired base fingerprint with historical parts requires a new edition, preventing retired parts from silently rejoining a sequence.

Whole-world, guide, part, standalone music, story and comic pins retain the Player capability shape. Story/comic use their dedicated versioned fields, never a generic production package masquerading as one. Catalogue display picture pins supplement existing bundle-path strings. The server's projection is tested against the pinned Player parser, not an invented substitute schema.

Transient transport/commit failures become `uncertain` and remain eligible for outbox reconciliation. Domain or approval changes become `held`. The final allowed transient failure records `dead-letter` / `attempt-limit` immediately and completes that event's handling, without requiring an additional outbox dispatch. The publication is still unsuccessful and its catalogue remains unchanged. An explicit authorised retry records a separate audit command and opens another bounded cycle (at most ten manual cycles). Retrying cannot change the original revision, approval, origin or policy. If intent has changed, resolve the hold/review and confirm a new command. A publication hold does not erase an already public entry.

Withdrawal commits an omission at a **higher** serial while retaining bytes and history. Withdrawing a base or any part removes that base/entire dependent part sequence from discovery, avoiding a gap; independently pinned productions and guides remain. Withdrawing a standalone production or guide removes its own slot. Previously downloaded editions, cached feeds and known immutable URLs may remain usable. This is not remote deletion or DRM.

## Optional GitHub compatibility mirror

The canonical database snapshot is authoritative. `LegacyMirrorEnabled` defaults false. Qualify `LegacyMirrorRepository`, `LegacyMirrorPath`, `LegacyMirrorBranch` and a least-privilege secret `Publishing:LegacyMirrorToken` externally. The token is not returned in DTOs, stored in operation policy or emitted in public URLs.

Each channel has a separate expiring mirror claim. Every event/retry loads the **latest canonical head**, compares the remote serial/content, and uses the remote blob SHA to condition the Contents update. The adapter uses the [GitHub Contents API's required replacement SHA](https://docs.github.com/en/rest/repos/contents#create-or-update-file-contents); CAS conflicts reconcile again. A higher remote serial or different content at the same serial holds the mirror for operator reconciliation. A delayed older worker cannot overwrite a newer mirror through a stale SHA. Unknown write outcomes are resolved by reading the remote again. Mirror failures do not roll back a committed canonical release. No GitHub mirror write is performed by local verification.

That `contents` mode does not update the release asset used by installed Players.
For that endpoint, `LegacyMirrorMode=release` makes AONIK a credential-free observer;
the separately serialized Actions bridge writes the release using its own
short-lived repository token. Missing/lower bytes remain `mirror-release-pending`.
Only exact anonymous readback confirms sync. Promotion preserves immutable serial
fences and previous assets but has a brief non-atomic filename gap. See the
[release cutover and handoff runbook](../runbooks/arke-publishing-handoff.md) for
the sole-writer requirement, recovery, configuration and production gates.

## Editable designs and reproducible checks

The admin manifest must include the server-evaluated `PublishingReviewPolicy`,
`PublishingManagementPolicy`, `PublishingPublishPolicy` and
`PublishingWithdrawPolicy`. The Arke Worlds profile also requires the publishing
module and the appropriate stored permission. An assigned API reviewer can otherwise
be blocked by navigation even when the publishing endpoints authorise them. The
manifest integration tests cover each publishing permission and a tenant admin
without publishing access; no client-side role inference or endpoint bypass is used.

- [Dedicated editable canvas](../../Templates/aonik-admin-starterkit/publishing.html), also included in the full canvas; [review gallery](../../Templates/aonik-admin-starterkit/marketing/publishing.html).
- Fourteen native React artboards: queue, exact review, package, rights, history, approval confirmation, stale review, forbidden review, publication readiness, publication confirmation, catalogue, uncertain retry, withdrawal and mirror hold. Twenty-eight light/dark exports plus real Admin UI review captures are in the marketing screenshots directory. All records/rights references there are fictional.
- `src/Aonik.AdminUi/src/pages/arke/publishing/PublishingViews.tsx` is the shared presentational source. `build-publishing-design.mjs` bundles it and the actual application icon renderer for the kit and copies the same CSS. Edit fixtures/layouts through the canvas; regenerate the shared bundle after changing application views. No application API adapter is included in the artboard bundle. The shared view deliberately retains semantic native selects, tables and checkboxes so the React-only canvas renders the exact same controls. Localised guardrail exceptions record this boundary; semantic-token contrast, keyboard navigation, confirmation gates and browser focus checks still apply.
- Regenerate with `node Templates/aonik-admin-starterkit/scripts/build-publishing-design.mjs`, `build-publishing-review.mjs`, then `verify-publishing.mjs`. The design check renders all themes and exercises local confirmation/failure controls without API calls.
- Start Admin UI with `VITE_AUTH_PROVIDER=mock` on port 4176, then run `node src/Aonik.AdminUi/scripts/verify-publishing.mjs`. It intercepts API responses and checks actual routed components, exact confirmation payloads, stable unknown-outcome retry IDs, stale refusal, read-only actions, accessible button names, focus and themes. This is not a deployed-auth acceptance claim.
- Backend tests cover actual SQL rollback after writes/before commit, competing heads, expired workers, response loss, public tenant isolation, public byte/MIME/digest checks, approval/hold/permission changes and mirror CAS. Fixtures come from `tools/arke-package-verifier/test/export_fixtures.dart` against the pinned reader. Set `AONIK_PLAYER_QUALIFICATION_DIRECTORY` for `PublicationDeliveryTests`, then run `test/qualify_catalogues.dart` in the pinned staged verifier workspace to parse the seven emitted .NET catalogues and check every public pin against its saved bytes.

The CLI-generated canonical migration is `20261104080759_AddPublishingPublicDelivery`; it adds five delivery/catalogue tables, submission pointers/hold reason, preview-access metadata and scoped constraints. It was applied with the full canonical stream to a fresh disposable LocalDB database. No module-context migrations, manual migration operations or snapshot edits were used.
