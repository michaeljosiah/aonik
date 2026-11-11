# Arke publishing authority handoff

This addresses the engineering gaps found after #403 and #428 under epic #397 and
follow-up #431. Production activation remains a distinct owner decision. The
current Azure inventory contains `rg-aonik-dev-uksouth` only; there is no qualified
AONIK production deployment to point Studio or Player at.

## Legacy feed: use the endpoint installed Players actually read

Installed production Players read:
`https://github.com/michaeljosiah/ArkeStudio/releases/download/arke-player-worlds/catalogue.json`.
The original `contents` mirror updates a Git branch file, not that release asset.
It retains its CAS implementation for explicitly qualified branch consumers.
Do not enable it and claim that installed Players are receiving updates.

The release bridge is `tools/arke-release-mirror/mirror.py`, also exposed as a
composite action. It fetches the latest **committed public AONIK feed**, never a
workflow payload or local edited catalogue. Only a repository-scoped, short-lived
Actions `GITHUB_TOKEN` writes the release. AONIK holds no GitHub write credential.

The workflow must live in the repository owning the release. All writers share
concurrency group `arke-release-catalogue-writer`, with `cancel-in-progress: false`.
The existing direct/manual release writers must stop before activation. A
concurrency group cannot fence an unrelated person, workflow or credential.
GitHub's release API has no Contents-style conditional asset replacement; this
single-writer rule is an operational precondition, not a database lock claim.

1. Require the configured baseline serial and SHA; refuse a lower source/current
   feed or conflicting bytes at the same serial.
2. Read the highest retained `aonik-catalogue-<serial>-<sha>.json` snapshot and
   verify its bytes. It is a durable serial fence even if the live filename is
   missing after interruption.
3. Upload and read back the new immutable snapshot and separate pending asset.
   Re-read the canonical source and current release asset identity before promotion.
4. Rename the old live asset to a retained name; rename the pending copy to
   `catalogue.json`. Never delete the last complete catalogue or overwrite an
   immutable snapshot. Never change release metadata or the application's Latest.
5. Confirm the actual anonymous public URL returns the exact canonical bytes.
   Like Player's explicit **Check for updates**, readback uses a fresh query and
   `Cache-Control: no-cache` to avoid GitHub's cached previous filename redirect.
   Background consumers may still see their cached redirect until it expires.
   Unknown responses and failed readback stay failed until a later run observes
   success. A replay performs no duplicate promotion.

There is a short availability gap between the two renames. A killed run may leave
that gap until recovery. There is **no atomic swap guarantee**. Player keeps its
last accepted catalogue and installed library on fetch failure; fresh discovery
can be temporarily unavailable. Scheduled runs target five-minute intervals but
GitHub scheduling and CDN caches do not provide a five-minute delivery SLA.
The retained history has a 2,000-asset bound; crossing it requires an operator
retention review, not automatic deletion of evidence.

## Configuration and accurate status

The disabled-by-default workflow is `.github/workflows/arke-release-mirror.yml`.
Use a protected `arke-release-mirror` environment with restricted deployment
branches. Its variables are:

| Variable | Required value |
|---|---|
| Repository `ARKE_RELEASE_MIRROR_ENABLED` | `true` only after cutover approval |
| Environment `ARKE_RELEASE_MIRROR_TAG` | Exact existing release tag |
| Environment `ARKE_CANONICAL_CATALOGUE_URL` | Permanent AONIK HTTPS channel URL |
| Environment `ARKE_CATALOGUE_BASELINE_SERIAL` | Qualified serial floor |
| Environment `ARKE_CATALOGUE_BASELINE_SHA256` | Exact baseline hash |

For ArkeStudio, pin the composite action to a reviewed full AONIK commit SHA;
do not copy an unpinned remote script. The job needs `contents: write`, Python 3.12,
`GH_TOKEN: ${{ github.token }}` and `ARKE_MIRROR_SINGLE_WRITER: 'true'`. Repository
and tag are fixed by protected configuration, not free-form dispatch arguments.
The bridge refuses cross-repository writes and does not create a production release.

Configure the matching AONIK channel with `LegacyMirrorEnabled=true`,
`LegacyMirrorMode=release`, `LegacyMirrorRepository=michaeljosiah/ArkeStudio`,
`LegacyMirrorReleaseTag=arke-player-worlds`, `LegacyMirrorPath=catalogue.json`.
The Worker follows only HTTPS GitHub release-storage redirects without cookies or
credentials and caps the response at 1 MiB. A lower/missing release remains
`retryable / mirror-release-pending`; a higher or conflicting head is held.
Only an exact public readback yields `synced / latest-head-confirmed`.

Publication stays canonical even if the compatibility feed is delayed. In Admin,
open **Arke Worlds → Player catalogue**, inspect the separate mirror state, then
**Reconcile latest mirror** after fixing the bridge. This observes the external
release; it does not bypass the serialized writer or editorial approval.

## Baseline and rights inventory

The original audited source was serial **53**, SHA-256
`3fdc4558650817334e99f68d3364c9a3a00a2519930eaed016a30dff4d1229b4`:
five worlds, thirteen package pins, eighteen artwork pins and 1,812 embedded assets.
The source bytes and integrity evidence are under
`docs/runbooks/evidence/arke-catalogue-audit-2026-11-10` and the adjacent audit JSON.
Public availability establishes neither ownership nor a new rights declaration.

The live head advanced to **54** during the 11 November handoff checks through
already-merged Player #78. The [follow-up audit](evidence/arke-catalogue-baseline-2026-11-11.json)
verifies its exact SHA-256
`716f3457dd831d9402b862c76ce750fd7f997f3c48440fce5166b52ba6017957`,
31 pins and 1,833 embedded assets with both current and previous Player readers.
Only Na Love or Juju's world and guide pins changed. Hosted and bundled bytes match.
Use the [serial-54 ownership projection](evidence/arke-baseline-ownership-serial54-template-2026-11-11.json)
for the latest preparation, preserving the original audit and template as history.
Production Party and private rights-reference fields remain null. A cutover must
qualify the current highest serial again; serial 53 is no longer the live head.

The [unapproved ownership template](evidence/arke-baseline-ownership-template-2026-11-11.json)
contains the base production/chapter projection read from hash-checked audited
archives. Michael Josiah was named by the owner for all five namespaces and the
reviewer/support contact on 11 November 2026. Production Party IDs and private
rights evidence remain unresolved, so those template fields stay null. This
designation does not establish an independent backup or approve distribution.
Reproduce the template
with `tools/arke-release-mirror/prepare_baseline.py` against the frozen catalogue
and audit cache; fill private ownership records only after an explicit decision.

| World namespace | Existing work | Production owner/evidence |
|---|---|---|
| `the-undersong` | The Undersong | Michael Josiah; Party/evidence mapping pending |
| `legend-of-dumah` | Legend of Dumah | Michael Josiah; Party/evidence mapping pending |
| `the-red-quiet` | The Red Quiet | Michael Josiah; Party/evidence mapping pending |
| `embers-of-the-fallen` | Embers of the Fallen | Michael Josiah; Party/evidence mapping pending |
| `na-love-or-juju` | Na Love or Juju | Michael Josiah; Party/evidence mapping pending |

Before import, resolve each owner to a real tenant Party and retain the private
rights/evidence reference. Qualify the base listening chapter projection from the
audited packages before adding parts. Do not invent production IDs for legacy
display-only entries. The importer preserves original JSON bytes, pins, IDs,
metadata, serial, and legacy provenance. New editions still require fresh exact
server verification, rights evidence and independent approval.

Re-fetch the hosted baseline immediately before cutover. If it differs from the
audited hash, stop and qualify the new highest serial; never overwrite it with 53.
Initialise the new production channel exactly once through the authorised API.
Read back its canonical bytes and compare to both hosted and bundled baselines
before enabling new intake/delivery or switching any consumer URL.

## Cutover sequence and recovery

1. Record the approved production tenant, channel, permanent HTTPS origin,
   identity clients/callbacks, owner mappings, reviewer/publisher IDs, private
   rights reference, support owners, retention, budget and change window.
2. Deploy immutable qualified API/Worker/Admin and verifier images, canonical
   migrations, persistent private/public volumes and least-privilege identities.
   Use the #428 capacity profile only with its matching storage/lease limits.
3. Back up database and private/public stores together, including `.store-id`;
   validate restoration using the recorded #403 procedure. Capture existing
   application Latest release and the hosted catalogue identity.
4. Stop all old catalogue writers. Freeze and re-audit the latest baseline. Import
   with its real namespace owners. Compare anonymous AONIK readback byte-for-byte.
5. Install the pinned release bridge in ArkeStudio, with production variables
   disabled until approved. Activate one run against the unchanged baseline,
   confirm exact legacy public readback and unchanged application Latest, then
   enable scheduled reconciliation and AONIK's read-only release observer.
6. Configure new Studio/Player builds to the permanent production channel. Verify
   the existing supported Player at its unchanged release URL. Release one
   explicitly approved edition and verify discovery, install/update, progress,
   offline retention and withdrawal with exact publication/serial evidence.
7. Record the handoff and monitor canonical/mirror lag, failed Actions, queue age,
   dead letters, validation time, review age, storage and cost. Assign the alert
   destination and its primary/backup; do not claim a route is staffed implicitly.

On failure, pause new intake/delivery as appropriate; preserve anonymous committed
feeds and retained artifacts. Restore the matching app/config version, not a lower
catalogue snapshot. Resume the same operation/reconciliation after repair. A
release gap is recovered from the latest canonical head and serial fence; never
rename an older backup into live discovery to make a red status green. Conflicting
same-serial or higher legacy content needs explicit reconciliation before writing.

## Private manual intake during an outage

Manual fallback means continuing private intake and communication, not bypassing
AONIK approval with a hand-edited public feed.

1. Use the staffed `submissions@arkeworlds.com` route once its mailbox/owners are
   verified. Issue a unique receipt in the restricted register; do not ask for
   large email attachments or send private content through a public issue.
2. Give each creator a restricted upload location. Test access from a different
   account. Record creator, timestamp, declared rights/version, archive length
   and SHA-256; keep the file immutable and unexecuted pending verification.
3. Record an explicit `awaiting-system-intake` status, assigned owner, next review
   time and creator-facing update. The three-working-day target requires staffing.
4. On restoration, submit the frozen bytes through normal AONIK intake, correlate
   the manual receipt, verify server length/hash and obtain the ordinary independent
   exact-revision approval. Changed bytes get a new revision. Never mark email
   acknowledgement as editorial approval or public delivery.
5. For urgent removal while automated delivery is impaired, use the authorised
   withdrawal operation and serialized mirror retry. If authority is unavailable,
   escalate to the named incident owner and preserve evidence; do not improvise a
   lower-serial public rollback. Already installed offline copies cannot be recalled.

## Owner decisions for production

The engineering evidence cannot supply these business decisions:

- Resolve Michael Josiah's production Party and the private permission reference
  covering retention/distribution of the existing exact public editions and artwork.
- Which production tenant/channel/origin and accountable deployment/budget owner
  should be used? No qualified production Azure resource group was found in the
  accessible subscription inventory.
- Michael Josiah is the designated reviewer/support contact. Confirm a production
  contact address, independent editorial coverage where required, backup coverage
  and whether the three-working-day response target is staffed. The existing
  Mailinator reviewer identity is a dev fixture, not a production contact.
- Approve the concrete infrastructure cost and cutover window. The #428 estimate
  is about **£1,232.24 per 730-hour month for one E16 verifier node including one
  management charge**, before API/Worker/SQL/storage/networking and discounts/tax.
  Both retained dev pools currently have one node; their combined compute estimate
  is about £2,354.69 per 730 hours before management. Retiring the old rollback
  pool or redesigning idle capacity is a separate cost decision.

These decisions are not evidence of two external creators or a Play-installed
production pilot. Launch tickets AONIK #395, Studio #2213 and Player #24 keep that
scope. The owner accepted autonomous test-account/emulator evidence for #403.

The [production proposal](arke-production-proposal.md) makes the target deployment,
configuration prerequisites and cost assumptions reviewable. Issue #434 tracks its
remaining owner decisions and actual cutover; the epic stays open until those
production gates are resolved.

## Verification record

The [private outage/recovery evidence](evidence/arke-private-intake-recovery-2026-11-11.json)
records a real Studio service run: a 1 MiB / eight-asset frozen synthetic package
remained paused in its private journal before any network admission, then resumed
after process restart to receipt `235d8559-0f3a-403e-962d-405e5a77afeb` and passed
server verification into review. The same candidate/hash/bytes were retained;
the journal had inheritance disabled and only the current OS user allowed. No
approval/publication occurred and dev remained empty serial 26. This rehearses
the technical fallback/re-entry path; it does not claim mailbox or staffing readiness.

`python -m unittest discover -s tools/arke-release-mirror -v` covers stale/lower and
same-serial conflicts, changed canonical input, both interrupted rename steps,
missing-live serial fencing, lost replies, corrupt staging and failed readback.
The CI dispatch input `qualify_release_mirror=true` performs the real GitHub API
rehearsal using a run-scoped, non-Latest prerelease with empty synthetic catalogues,
then removes only that exact release/tag and retains sanitized evidence.
It never touches ArkeStudio's production release.

The [live release recovery evidence](evidence/arke-release-mirror-2026-11-11.json)
records all twelve checks passing in run `42947720150`, including lost responses
at both rename steps and recovery without duplicate promotion. The temporary
release and tag were removed and application Latest was unchanged. PR #432 was
merged as `7f2f7928f3fad96964d32e19d0aef9fab44af6c2` after final-head CI passed.
The qualified image update reached ready revisions API `0000390`, Worker `0000380`
and Admin UI `0000373`. The isolated release observer reached ready API `0000391`
and Worker `0000381`; authenticated retry with a missing release catalogue correctly
reported `retryable / mirror-release-pending`, with no mirrored serial. Dev stayed
at the same empty serial 26 and its health endpoint returned 200.

The first actual-source bridge run (`43053870065`) exposed an HTTP header bug before
uploading any assets: its bodyless catalogue GET declared a JSON content type, so
FastEndpoints returned 400 while trying to deserialize an empty body. The bridge
now emits content type only for requests carrying bytes and limits GitHub version
headers to GitHub API hosts. Eighteen recovery/transport tests pass, including this
regression, and the corrected transport retrieves the exact dev catalogue. Scheduled
mirroring was disabled while correcting it. PR #433 merged the fix after green CI.

The [actual-source dev mirror evidence](evidence/arke-dev-mirror-2026-11-11.json)
records successful bridge run `43056912292`: exact canonical serial 26, SHA-256
`3ff0014a08aa98781a94cc6f571a17c2ef804566e47e8a0b64e022e841e0025f`.
Authenticated AONIK reconciliation then reached `synced / latest-head-confirmed`
with mirrored serial 26. Repeat run `43057800631` reported `promoted: false` and
retained both asset IDs. Application Latest stayed `v0.1.1` / release `320999757`.
API `0000392` and Worker `0000382` are ready with their exact pre-rehearsal mirror
settings and images restored. Health is 200; the public feed remains 37 bytes,
empty serial 26. The temporary reviewer session was signed out and its encrypted
credential removed. The last synced observer record is historical, not an indication
that mirroring remains enabled.

The repository bridge switch is disabled. The isolated prerelease
`michaeljosiah/aonik:aonik-publishing-dev-mirror` is retained with only the two empty
test-catalogue assets: automatic approval review rejected deleting the remote release
and tag without explicit deletion authority. No production release was modified.
The separate twelve-case fault rehearsal above covers interrupted promotion and
serial conflicts; this empty-feed run establishes actual-source/observer integration,
not a new claim of an approved edition being published.

`PublicationBaselineTests` exercises the actual frozen serial-53 input through
the service with synthetic test-only owner mappings. These tests are not a
production import or rights attestation. `GitHubReleaseMirrorTests` verifies
anonymous bounded readback, redirect restrictions and pending/no-write behavior.
