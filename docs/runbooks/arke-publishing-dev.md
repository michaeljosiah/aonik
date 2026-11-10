# Arke publishing dev rehearsal — #403

This record separates deployment from end-to-end qualification. No production
catalogue migration or public release is authorised by the dev rehearsal.

## Delivery exhaustion and isolated restore — 10 November 2026

The final-attempt regression found that the publication handler previously relied
on another outbox dispatch to record exhaustion. If the outbox stopped at the
same attempt bound, the operation could remain `uncertain`. PR #421 now records
`dead-letter` / `attempt-limit` on the final transient failure itself, releases
its claim and returns the receipt to approved state in that transaction. The
handled terminal outcome completes the original outbox message; a separate,
audited manual retry is needed to resume delivery. No schema changed.

Local verification passed all 139 publishing tests and the solution build.
GitHub CI's backend rerun passed 5,385 tests with five existing skips; the Admin
UI suite passed 690 tests. The first CI attempt hit an unrelated API host-startup
serializer error, tracked in #422; record both attempts rather than implying an
uninterrupted first-pass result. The image build and security scan passed.

The Worker-only image rollout uses merged commit
`8014cd3370a82583abfeac2d02da953443fef7bf`, scanned source
`30370ffb968d7f5712b30a35086bc4eba4fdcd91`, and healthy revision `0000375`.
All 46 Worker environment entries are unchanged. API `0000385`, Admin UI `0000370`,
the six-attempt publishing limit, ordinary exponential backoff and disabled mirror
remain unchanged. The retiring Worker replica was confirmed absent before the fault.

For monitoring, prefer the deployed publication activity screen and normal
service reads. Repeated Container Apps diagnostic WebSocket connections can return
429 with `Retry-After: 600`; stop that probe and honor the cooldown. The live
rehearsal encountered this diagnostic limit, switched to the deployed activity
screen, and kept the separate restore watchdog running. Do not alter retry timing
or bypass cloud throttling merely to speed up evidence collection.

### Live exhaustion, recovery and withdrawal

The original synthetic Review Garden archive was submitted through Studio's real
freeze/upload/receipt services under new receipt
`ccec17c2-c65d-4a94-a3bc-a4661f362923`, revision 1. The deployed reviewer inspected
the private download and recorded the exact independent test-account decision
under the owner's autonomous dev authority. Publication, retry and withdrawal
used the real deployed UI confirmations.

The bounded fault denied only the fixed Worker's observed `100.100.0.59/32`
source. Its readback returned 403 while normal clients returned 200. A separate
30-minute restore watchdog was armed before the rule was created. Normal
backoff ran without changing limits: the sixth failed attempt recorded
`dead-letter` / `attempt-limit` at 11:16:39 UTC. The operation claim was cleared,
the receipt remained approved, and empty catalogue serial **20** was unchanged.
The original outbox event completed this handled terminal result after five
retryable failures; it did not require a seventh delivery dispatch.

After explicit rule removal and a successful Worker 200 readback, the deployed
**Retry same operation** confirmation recovered operation
`154e4327-cec6-4fe6-9b56-faa03666c442` on its first attempt. Retry audit
`920e1ed1-55c3-4382-a64d-766b9b98375c` retains the reason and original operation
reference. Both artifact IDs, hashes and URLs are unchanged; the existing archive's
ready timestamp is preserved. Exactly one edition committed at serial **21**.
Independent downloads match the 99,725-byte approved ZIP and 2,052-byte artwork.
The ten-cycle manual retry boundary and exact-request replay pass service tests;
this live batch exercises one manual retry, not ten live cycles.

The ordinary Android emulator fetched serial 20 during the fault, discovered the
recovered edition at serial 21, and fetched empty serial **22** after the UI
withdrawal. All four saved worlds, library/reading records, 31 retained media files
and preferences are unchanged. No APK replacement or data clear occurred.
Studio's final authenticated receipt refresh reports **withdrawn / complete**.
There are no active deliveries, the ingress baseline is restored and the watchdog
has exited. Production and the GitHub mirror remain unchanged.

The live retry audit exposed a wording defect: `retry-queued` fell back to the
generic incomplete-check warning. The shared runtime/design presentation now
explicitly describes the queued reconciliation. All 690 Admin UI tests pass;
all 14 editable scenes and 28 light/dark exports verify against the regenerated
shared bundle. This is a presentation change; retry permissions and execution
remain server-enforced.

See [sanitized exact evidence](evidence/arke-delivery-recovery-2026-11-10.json)
for operation/artifact IDs, before/after Player checkpoints, approval provenance,
database/file comparisons, runtime hashes and cleanup records. Raw downloads,
database rows and browser screenshots remain private local evidence.

### Isolated backup restore and private handoff

The rehearsal restored Azure SQL `aonik` to a new, isolated S0 database at
**2026-11-10T10:10:00Z**. Azure accepted the request at 10:14:58 and the restored
database became readable at approximately 10:46 UTC. The running API and Worker
kept their original connection strings throughout. No application was switched
to the restored database and no production recovery-time objective is inferred.

Both publishing Azure Files shares were snapshotted at 10:14:53–55 and restored
into separate private shares. Every restored file was downloaded again and
compared byte for byte: **41 private files / 29,484,564 bytes** and **21 delivery
files / 14,648,840 bytes**. These immutable-file snapshots follow the SQL restore
point; additional unreferenced bytes do not become published records.

A SELECT-only probe used the existing dev API network and database credential,
without opening a SQL firewall rule. All **200 publishing rows across 13 tables**
match the saved source checkpoint in every field, including row versions. The
restored catalogue head is empty serial **20**. All 20 public artifact records
and all 14 completed private upload packages resolve to the restored files with
the expected length and SHA-256. Both `.store-id` markers and their derived
storage location identities are preserved at the canonical logical mount paths.

The restored private Review Garden archive also passes Studio's real
`checkWorldPackageZip` verifier. The private handoff records its receipt,
revision, exact editorial decision/digest, package hash and current withdrawn
status. This proves recovery of a verifiable intake package and its review
evidence; it does not authorize republishing a withdrawn receipt or qualify a
manual production mirror upload.

For a real recovery:

1. Hold new publishing admission and delivery through the normal audited
   configuration process. Record the canonical catalogue serial/head, database
   restore point, source share snapshots, storage identity markers and image
   versions. Retain the existing environment for investigation.
2. Restore SQL into a new database and files into new private shares. Compare
   publishing records, operation claims, approval digests, catalogue snapshots,
   package/artifact hashes and all referenced paths. Account for objects created
   between the database restore point and file snapshot; never infer a release
   from the presence of files alone.
3. Preserve `.store-id` and the logical mount paths
   `/mnt/aonik-publishing-private` and `/mnt/aonik-publishing-public`. A different
   path/marker deliberately changes the location identity and blocks delivery;
   do not rewrite database identities to bypass that guard.
4. Reconcile the highest serial already served to consumers before any runtime
   cutover. Never serve a restored lower serial as a public rollback. Perform a
   separately authorized cutover and recovery by a new canonical serial, with
   exact approval, namespace and policy checks still enforced.
5. If cloud publication remains unavailable, retain the verified private ZIP,
   manifest, hash, receipt/revision and rights/review evidence for an operator
   handoff. Current withdrawn/held/rejected state remains authoritative. Do not
   bypass approval, copy arbitrary Studio workspaces, or publish through the
   legacy mirror as an improvised fallback.
6. After verification and retention of sanitized evidence, remove only the
   rehearsal's isolated databases/shares/snapshots and probe files. Verify the
   original resources, ingress rules, mirror setting and application connections.

Azure SQL's configured seven-day point-in-time retention and Azure Files share
snapshots are separate mechanisms. This rehearsal verifies their recovered data
together; it does not establish cross-resource atomic backups, a full runtime
disaster-recovery cutover, regional failover, or production capacity/cost readiness.

## Acceptance standard and reviewer UI — 10 November 2026

The owner accepts autonomous end-to-end testing with the development test accounts
and waived separate human checks on 10 November. Earlier requirements in this record
for a nontechnical walkthrough or consenting-human dev pilot are superseded for dev
acceptance. Preserve separate creator/reviewer identities, exact-revision approval
and release confirmations, and evidence of the actual deployed screens. This does
not authorise production distribution or claim physical-device/OEM coverage.

The first deployed reviewer UI attempt exposed two independent prerequisites:
the tenant still selected the generic `base` profile, and `/admin/manifest` omitted
all publishing policies even when the server granted the review permissions.
The manifest fix evaluates the existing publishing policies without weakening any
permission or endpoint check. Its 15 manifest API regressions pass.

At 06:50 UTC, a guarded operator tool applied the installed `arke-worlds` v2 pack
to the exact Arke Kidz dev tenant using the existing additive `IConfigPackApplier`.
It selected the Arke Worlds profile and recorded `ArkeDevBusinessPackApplied` with
correlation `arke-403-ui-20261110`. Existing settings and user-role assignments were
read back unchanged; the module reapply wrote no disabling rows and preserved
existing defaults. No schema or financial data changed. The temporary single-client
SQL firewall rule was removed in `finally`. This is configuration preparation;
the deployed UI journey was subsequently verified as recorded below.

### Deployed reviewer and Player acceptance

[PR #418](https://github.com/michaeljosiah/aonik/pull/418) is merged as
`a8b6919c8372593d5fcea540819d9c9369e9684c`. Its CI and image scan passed;
API revision `aonik-dev-api--0000385` is healthy. The image-only rollout preserved
all 85 environment entries. The reviewer can now reach the actual submission queue
and catalogue through the Arke Worlds menu.

The original synthetic **Review Garden Dev 403** passed the deployed Edge UI journey:
private package download and verified artwork/inventory; rights and audience review;
request changes; a fresh Studio revision under the same receipt; exact-revision
approval; hold and hold removal; separate publication; and catalogue withdrawal.
The private UI download and public artifact both match Studio's frozen archive
`7bae38aad3244ea813ea287ab0aae336e9cd6f08aa8beba74b6fef852b8b61df` (99,725 bytes).
Revision 2 retains those bytes and adds explicit dev-only scope to the summary,
producing a different review digest. Studio receives the creator-facing message,
while the reviewer history retains the internal assessment. Editorial, hold,
release and withdrawal writes used the actual deployed UI, with no intercepted
responses or API harness for those actions. Creator freeze/upload/receipt checks
used Studio's actual services through the existing isolated harness.

Approval and hold/removal left empty serial 18 unchanged. Publication committed
serial **19**; withdrawal committed **empty serial 20**. Studio's revised receipt
`9bede8c3-8141-4e1c-89b0-962dd39cfd27` now reports **withdrawn / complete**.
The Android 16/API 36 emulator discovered and installed the actual HTTPS edition,
played its synthetic chapter, fetched the withdrawal, and retained its downloaded
copy. With Wi-Fi and mobile data disabled and no active default network, a cold
process restart reached the Offline home and opened the local chapter for native
playback. Networking is restored and Player is paused; no APK replacement, uninstall
or data clear occurred in this batch.

All three prior library positions and all three prior world records are unchanged.
The previous story/comic files and preferences are byte-identical; 53 prior private
files are unchanged and none were removed. One prior audiobook refreshed only its
save timestamp. Full before/after private-directory backups and local screen captures
are recorded by hash in the [sanitized evidence](evidence/arke-reviewer-ui-2026-11-10.json).
Raw browser captures remain local because they include unrelated tab chrome.

The live journey also exposed successful hold outcomes using a generic incomplete-check
warning. [PR #419](https://github.com/michaeljosiah/aonik/pull/419), merged as
`8f7136b46acbd437ecdd6e73e7ec3c9a4c1a0cc7`, corrects both labels. Its CI/image scan
passed, dev Admin UI revision `aonik-dev-adminui--0000370` is healthy with its
configuration unchanged, and the actual recorded operations now show the correct
success messages. The shared editable-artboard bundle is regenerated; all 14 scenes
and 28 light/dark exports pass verification.

Validation: solution build has zero errors; final local .NET project runs total
5,374 passed / 12 existing skips. The initial parallel solution run hit API request
timeouts; the complete 791-test API rerun passed with two xUnit threads. All 15
manifest tests and 690 Admin UI tests pass, as do UI production build, design and
contrast checks. Both fix PRs passed independent GitHub CI.

This completes the owner-approved automated deployed-UI acceptance for #402.
#403 remains open for the remaining technical/operational matrix: supported older
installed applications and applicable interactive-movie/export-UI checks; deliberate
mirror migration and production baseline/rights/ownership/capacity/cost qualification.
The later recovery section above qualifies live attempt exhaustion, one audited
retry, isolated database/files restoration and a private verified intake handoff.
It does not qualify a full runtime disaster-recovery cutover or manual production
publication. Physical-device/OEM coverage is not claimed. A separate human dev
walkthrough is no longer an outstanding gate. Production and the mirror are unchanged.

## Deployed application

The latest configuration release `a6dc2e1d50db8e6dfc689c6fd409dfed78029b62`
passed [preview](https://github.com/michaeljosiah/aonik/actions/runs/42289347838)
and [deployment](https://github.com/michaeljosiah/aonik/actions/runs/42289777170)
on 6 November 2026. It retains the tested `78be974c` application images below.
API revision `0000379` was restarted after its Key Vault secret references were
updated; the replacement replica became ready before new verification requests.
Both synthetic creators completed actual mailbox OTP verification at 20:46–20:47
UTC. Their required onboarding gates pass with no next actions. API health is
200 and the closed dev catalogue remains 404. See the
[sanitized account evidence](evidence/arke-creator-email-2026-11-06.json).

The follow-up release `78be974cb7bc8e3f268641289bb598ef657d0682` passed
[CI](https://github.com/michaeljosiah/aonik/actions/runs/42182358963),
[image build and scan](https://github.com/michaeljosiah/aonik/actions/runs/42182368248)
and [deployment](https://github.com/michaeljosiah/aonik/actions/runs/42183901780).
API `0000378`, Worker `0000369` and Admin UI `0000369` are Running; health is
200 and the closed catalogue remains 404. The actual reviewer session now leaves
the optional company wizard through **Skip for now** and reaches the Arke Kidz
dashboard. No industry/company details or setup-complete flag were invented.

| Application | Immutable digest |
| --- | --- |
| API | `sha256:ab433e5b7538a13a5662c1b916ba610d08bf41797664e6a03e271bf6a5f2a4b6` |
| Worker | `sha256:4f5e9af453d4a9ac52b100f72b694d7388d81e72340373eeb30f55948e982b4b` |
| Admin UI | `sha256:86e4a31ee124e010a84f6bc85c19c72b6552fbd79397e060e53c27d0bbc5c2a0` |

Recovery release `484c87782b397b636dda2bb98216041fca85ac58` from PR #406 passed
[CI](https://github.com/michaeljosiah/aonik/actions/runs/42179322706),
[image build](https://github.com/michaeljosiah/aonik/actions/runs/42179351344),
[preview](https://github.com/michaeljosiah/aonik/actions/runs/42180760111) and
[deployment](https://github.com/michaeljosiah/aonik/actions/runs/42181160485).
API `0000377`, Worker `0000368` and Admin UI `0000368` are Running; health is
200 and the disabled catalogue is 404. The actual reviewer browser now reaches
the two-organization picker and selects Arke Kidz without a false session expiry.
The live body-only registration check returns 403 before identity creation;
Auth0 confirmed no second identity was created by that rejected request. At that
point, the first identity was verified but still needed an AONIK invitation/link.

The previously unused tenant's optional company wizard then exposed a separate
skip loop: its completion callback reloaded into the unfinished wizard. The
follow-up changes that callback to leave setup in the current session, preserving
the incomplete flag and progress. No industry/company details are invented.
Native Studio consent was left unaccepted; tenant invitations use the normal
authenticated administration path once setup can be postponed.

On **6 November 2026**, the owner approved the protected dev build/deployment
gates for implementation `2848f9658fff95c45b43f19a2c5063f2b741cfd0` (PR #405,
merged as `03544392428273583672aad0652e17ea52c8a07f`). The normal workflows passed:

- [Image build](https://github.com/michaeljosiah/aonik/actions/runs/42166936001)
- [Deployment preview](https://github.com/michaeljosiah/aonik/actions/runs/42177133886)
- [Deployment](https://github.com/michaeljosiah/aonik/actions/runs/42177516628)

| Application | Ready revision | Immutable digest |
| --- | --- | --- |
| API | `aonik-dev-api--0000376` | `sha256:a67535ccdc3cebaf4040bfe7ed69ae093880e11c078edbeb7a4f302b4ca93727` |
| Worker | `aonik-dev-worker--0000367` | `sha256:e76ca6b6062f92a7ac4e4fb980af4756dbae3b7ef22c3737bedcdf45da1233e7` |
| Admin UI | `aonik-dev-adminui--0000367` | `sha256:e9c69a68205a64d718eb3770a1bcaddb149d16e9ff460669296644fbe383da65` |

API and Worker are Running with minimum replicas 1. Both select
`AzureDynamicSessions` and the qualified immutable verifier image/pool described
below. `/health` returned 200; the still-disabled dev catalogue returned 404.
This proves rollout, not an actual Worker-managed verification job.

The owner requested two synthetic Mailinator creators and nominated an existing,
email-verified account as independent reviewer. Registration exposed two defects:
body-only tenant routing was rejected before the endpoint, and an uninvited
registration created an Auth0 identity before tenant admission failed. The fix
checks active tenant/invitation before IdP creation and returns 403. Recover the
first verified identity through an administrator invitation followed by normal
sign-in; invite the second address before registration. Never mark email verified
directly, reset the reviewer's password, or grant access by editing SQL.

Reviewer Auth0 sign-in succeeded, but a stale Admin UI organization selection
caused `/v1/me` to run against an unrelated tenant before membership discovery.
The startup-order fix was subsequently deployed and verified as recorded above.
Registration/session recovery is not end-to-end publishing acceptance.

### Synthetic creator account proof

The nominated reviewer signed in through the normal Admin UI and created both
invitations in Arke Kidz without assigning elevated roles. Invitation records
were saved, but their emails were not delivered because dev email configuration
was incomplete. The first existing Auth0 identity linked on normal sign-in; the
second used the normal registration endpoint only after its invitation existed.

| Test creator | AONIK user | Party |
| --- | --- | --- |
| Synthetic Creator 1 | `a86d992b-872d-4fcd-b88d-0156ee122a5f` | `fcb1f81f-c207-4e34-942a-7183da17ff83` |
| Synthetic Creator 2 | `194096bc-eb58-40df-885a-bc37ecc04dbf` | `c712335b-ec09-499e-afce-40a9c8d92b6e` |

Both actual Mailinator verification links were followed; Auth0 reports both
addresses verified. Both then completed code + PKCE sign-in using Studio's real
`AonikAccount` implementation in an isolated rehearsal harness. `/v1/me` returned
200 and `Active` for each in tenant `1087cc03-46cc-45fa-9023-f0ce4f7bbdb8`.
The first `/v1/onboarding/me` snapshots passed `ProfileComplete` but required the
separate AONIK email OTP. After the corrected email configuration was deployed,
both `/v1/verifications/email/start` requests returned 200 and both fresh codes
arrived in the intended Mailinator inboxes from the Azure managed sender.
`/v1/verifications/email/confirm` returned 200 with `isVerified: true` for each.
Subsequent `/v1/onboarding/me` snapshots passed both required `EmailVerified`
and `ProfileComplete` gates, with `nextActions: []`. Phone verification remains
optional and unsatisfied. Auth0 claims were not used to bypass the application
OTP gate. The evidence records confirmation and onboarding timestamps without
credentials or verification codes.

Generated passwords remain DPAPI-encrypted in ignored local evidence. Harness
tokens are encrypted with process-only keys and were never printed. The harness
proves the Studio account adapter against dev, not the full desktop publishing
journey. Synthetic accounts do not establish consent from two real pilot creators.
Reviewer allowlisting, namespace admission, upload verification, editorial review
and isolated Player delivery remain separate unqualified steps.

### Dev invitation email prerequisite

The live invitation form reported no sender address. The existing dev ACS secret
was not a usable ACS connection, and neither accessible subscription contained a
Communication Services resource. A standalone, incremental deployment from
`iac/azure/environments/dev/email.bicep` provisioned `aonik-dev-email`, its
`AzureManagedDomain`, and linked `aonik-dev-acs` in `rg-aonik-dev-uksouth`.
What-if showed exactly these three creates; deployment `aonik-dev-email-403`
succeeded. Data location is `UK`, engagement tracking is disabled, and Azure
reports domain, SPF, DKIM/DKIM2 and DMARC verification as `Verified`.

The managed sender is
`DoNotReply@35565aeb-e344-40ed-ac8f-23e16de2f279.azurecomm.net`. The dev runtime
parameter records this non-secret fallback; other environments default to no
change. Stored tenant/global sender settings keep precedence.

On 6 November 2026 the owner explicitly approved the credential destination and
deployment. The connection was stored in GitHub's protected `dev` environment
secret `ACS_CONNECTION_STRING` at 20:22 UTC without printing it. The normal
[deployment preview](https://github.com/michaeljosiah/aonik/actions/runs/42287819287)
passed. Its `Delete` legend was checked against the actual resource changes:
zero deletions; four unsupported entries were existing roles in the unchanged
CodeAct session module. The preview does not expand publishing verifier access.
The [sender rollout](https://github.com/michaeljosiah/aonik/actions/runs/42288403086)
uses configuration `f7d0b093` and the already-tested `78be974c` application images.
Actual mailbox arrival and application verification must be recorded separately
from provisioning and the Auth0 verification links.

The initial sender rollout reached API revision `0000379`, but the live send
attempts returned 500: `enableOptionalSecrets` was still false, so the API used
the template's literal placeholder rather than the configured ACS secret. The
dev profile now enables the existing Key Vault references. No OTP hashing key
existed in GitHub or Key Vault; a new 256-bit dev-only key was generated directly
into protected `VERIFICATION_HASH_KEY` storage without printing or replacing an
existing key. The earlier undelivered challenges were discarded.

The corrected profile `a6dc2e1d` passed
[preview](https://github.com/michaeljosiah/aonik/actions/runs/42289347838) and
[deployment](https://github.com/michaeljosiah/aonik/actions/runs/42289777170).
Both `acs-connection-string` and `verification-hash-key` now reference the intended
dev Key Vault secrets through the API's system identity. The API revision was
restarted and its old replica retired before requesting fresh codes. Mailbox
arrival was observed at 20:43:46 and 20:43:50 UTC; the two creator confirmations
succeeded at 20:46:37 and 20:47:07 UTC. All required onboarding gates then passed.
This proves fresh verification-email delivery and consumption, not retrospective
delivery of the earlier invitation messages. The temporary sign-in harness
sessions were stopped after collecting the sanitized evidence.

### Previous baseline

On 5 November 2026, release `d05793b5bf1a1c754d4f94e68fb554e54eaba5cb`
(merged PR #404) passed CI and was deployed with the normal runtime workflow:

- [CI](https://github.com/michaeljosiah/aonik/actions/runs/41925344200)
- [Deployment preview](https://github.com/michaeljosiah/aonik/actions/runs/42072157634)
- [Application deployment](https://github.com/michaeljosiah/aonik/actions/runs/42072948960)
- API: `https://aonik-dev-api.delightfulisland-9fd7c1e7.uksouth.azurecontainerapps.io`
- Admin: `https://aonik-dev-adminui.delightfulisland-9fd7c1e7.uksouth.azurecontainerapps.io`
- Resource group: `rg-aonik-dev-uksouth`; subscription:
  `336d38e2-bb96-4510-a4c5-5c313139eef9`.

API health and Admin both returned HTTP 200. A read-only SQL inspection confirmed
the canonical migration stream through `20261104080759_AddPublishingPublicDelivery`.
The temporary single-client SQL firewall rule used for inspection was removed.

| Image | Immutable digest |
| --- | --- |
| `aonik-api` | `sha256:1cabdf1ff6cc1bc37308f2b66a453e004689b732324412bbe0c59785b8c3b752` |
| `aonik-worker` | `sha256:d6a1438db8cb1b065aa724c58be6d69a38854c98daa04e5094c8455c33dc2cad` |
| `aonik-adminui` | `sha256:2ce8e938e4e9fe242a7dfd149df5c002b59328ef3a7846fb094b57bdc06e43a7` |

The API's optional AI warm-up reported exhausted provider credits. It did not
prevent startup and is separate from deterministic package verification.

## Isolated channel

The existing **Arke Kidz** dev tenant is
`1087cc03-46cc-45fa-9023-f0ce4f7bbdb8`. Its persisted business type was `base`, pack
version 1, at inspection. Its name alone does not prove that the Arke Worlds pack
or Publishing module is configured. Apply the current pack/modules using the
normal authorised tenant administration APIs before admitting creators.

`iac/azure/environments/dev/aca.parameters.json` defines **arke-worlds-dev**.
Its API and Worker receive identical non-secret publishing settings through the
dedicated `publishingAppSettings` parameter. The GitHub runtime-setting collector
cannot overwrite this independent parameter. Intake, namespace admission, review,
delivery, public catalogue and legacy mirror remain explicitly **disabled** until
their prerequisites below are proven. No reviewer or publisher is auto-granted.

This is a **new synthetic channel**, with empty v2 baseline at serial 0. It is not
an import or replacement of the existing production channel. The SHA-256 binds
the exact baseline string. Do not initialise it using a production Player build
whose bundled/cached higher serial would mask this feed.

The dedicated storage module provisions two authenticated SMB shares (10 GiB
quota each), both private at the storage layer. Only AONIK's authorised public
artifact endpoint can expose delivery objects after approval. Mounts are:

- `/mnt/aonik-publishing-private`: session parts, frozen archives and store marker.
- `/mnt/aonik-publishing-public`: immutable delivery objects and store marker.

API and Worker use the same shares, paths and UID 1654. Neither is beneath the
web root or generic blob store. A running Worker replica is required: the SQL
outbox has no event scaler to wake a zero-replica worker. Storage is billed for
usage; retaining API/Worker replicas adds ongoing compute cost. A later API
scale-from-zero health probe exceeded 30 seconds, so rehearsal configuration
also keeps one API replica warm for Studio's bounded requests.

Storage deployment `publishing-storage-dev` succeeded with account
`ankpubxkhim4xqiopna`. The configuration was applied with
`scripts/Configure-ArkeDevPublishing.ps1 -Apply` after its read-only preview.
API revision `aonik-dev-api--0000374` and Worker revision
`aonik-dev-worker--0000366` became ready. Keeping the API warm produced revision
`aonik-dev-api--0000375`, verified Healthy/Running. All 25 publishing settings
matched the 5 November profile on both apps; API and Worker minimum replicas
are 1. The final warm API health check returned HTTP 200 and
`/publishing/channels/arke-worlds-dev/catalogue.json` returned 404.

An actual UID-1654 Worker shell wrote a synthetic probe to both shares. The API
read the same SHA-256 (`55c6dee596462bd625eb3b3d502bbdd51d7ac08fa7f23e1a0b0f9a2381d29f4a`),
renamed the private probe and removed both probes. This environment rejects custom
SMB mount options (`ContainerAppVolumeMountOptionsNotSupported`); its mounted root
permissions are `0777`, owned by root. Isolation therefore relies on private
storage bindings, trusted API/Worker containers and domain access checks, not
per-tenant POSIX users. No untrusted parser may share these mounts. This smoke
check does not qualify crash durability, backup restoration or multi-instance races.

Retained quotas are 512 MiB/creator and 2 GiB/tenant; the initial archive cap was 128 MiB,
reduced in the staged 6 November remote-verifier profile to 32 MiB (16 MiB per asset); at most
20 creator packages, 200 tenant packages, 10 namespaces/creator and 20 sessions
per creator/day. Parts plus assembly require at least three times the retained
quota plus temporary-write headroom. Share quotas are capacity ceilings, not
evidence of filesystem atomicity, backup or restoration.

## Creator sign-in

Confirmed issuer: `https://aonik.uk.auth0.com/`; API audience:
`https://api.aonik.com`. The existing deployed management application has only
`read:users create:users update:users`; it cannot create or modify applications.
After owner approval, **Arke Studio Dev / Native** was created in the `aonik`
development tenant on 5 November 2026. Its public client ID is
`9SfLUjB57WtWEQUNm1twTO7UQRnsUzvY`. The existing API management application's
permissions were not expanded.

Saved Auth0 configuration:

- Callback: `http://127.0.0.1:37619/aonik/callback`, exact match.
- Authorization Code and Refresh Token grants; token endpoint authentication `none`.
- The existing username/password connection is enabled. Refresh-token rotation
  is enabled, with an idle lifetime of **86,400 seconds**, maximum lifetime of
  **604,800 seconds**, and zero overlap. The AONIK API's **Allow Offline Access**
  setting is enabled. This API-level prerequisite also permits other authorised
  clients with refresh grants to request offline access; their policies were not
  changed. Studio's implementation supplies S256 PKCE, state and nonce.
- Implicit grant disabled; password, client-credentials and device-code grants
  remain disabled for this native client.
- Scopes `openid profile email offline_access`, API audience above.
- No client secret in Studio or its configuration. Do not reuse the Admin SPA ID.

The non-secret host profile is
[`iac/azure/environments/dev/arke-studio.json`](../../iac/azure/environments/dev/arke-studio.json).
Set `ARKE_AONIK_CONFIG` to its absolute path before launching the Studio branch.
It includes the API URL, issuer, native client ID, audience, scopes, tenant ID,
channel key, redirect port, 32 MiB qualification cap and `dev-synthetic-v1` declaration version.
The profile passed the actual Studio configuration parser. Its proposed synthetic
distribution statement still requires the submitting human's explicit confirmation
in Studio; creating this profile does not accept rights or authorise production.

A separate local Electron profile/workspace was opened for the initial sign-in
check, but computer-use access to that window timed out. The later isolated
harness proved both creators' live code + PKCE sign-in using Studio's real account
adapter, authorized profile reads and AONIK email verification. Desktop UI
sign-in, refresh rotation, persistent credentials and publishing receipt reads
still need their own acceptance checks. The installed Studio workspace was not
modified.

[Studio PR #2578](https://github.com/michaeljosiah/ArkeStudio/pull/2578) contains
the sign-in, frozen edition, resumable upload and receipt implementation. Its
`docs/development/aonik-publishing.md` describes configuration and recovery.

Two consenting creator identities and an independent reviewer/publisher must
have active AONIK user/party links. Create/onboard through domain APIs. Record
IDs privately; do not add passwords, tokens or private rights evidence here.
Use role management for `Publishing.Review`, `Publishing.Publish` and
`Publishing.Withdraw`, plus explicit channel reviewer/publisher IDs. TenantAdmin
alone grants no publishing authority. A creator cannot approve or publish their
own submission, including through another linked party.

On **7 November 2026**, the designated reviewer completed Studio's actual
Authorization Code + PKCE sign-in. A subsequent `/v1/me` request returned 200
and confirmed the expected tenant, reviewer identity and active party. The
normal tenant administration APIs created `ArkeDevPublishingReviewer` with
exactly `Publishing.Review`, `Publishing.Publish` and `Publishing.Withdraw`,
then assigned it to the reviewer using the additive user-role endpoint.
Read-back confirmed the assignment, preserved both existing reviewer roles,
and confirmed that both synthetic creators retain only `PersonalUser`.
See [sanitized reviewer evidence](evidence/arke-reviewer-authority-2026-11-07.json).
Actor and role identifiers remain in the private rehearsal evidence.

The first manual login handoff failed because its temporary terminal receiver
was no longer running. A separate, bounded background receiver resolved that
failure. After a successful callback the Studio adapter closes its loopback
listener by design; refreshing that callback page can therefore show connection
refused even though the account is signed in. Confirm the adapter's account
state and an authenticated API response before asking the reviewer to sign in
again. This establishes adapter authentication and role assignment, not desktop
UI qualification. Channel allowlists/enablement, editorial decisions and the
complete publishing rehearsal remain outstanding.

## Verifier runtime

ACR run `db3` successfully built and tested the pinned Linux verifier:

- Image: `aonikdevacr.azurecr.io/aonik-arke-verifier@sha256:dc81e03d6c13a3281986b9e0e0343add47c5fdcb31a7291597284a551c8ae164`
- Reader: `27df122566bcdef18126932c15a74112e18b8573`
- Official Dart SDK base: `dart@sha256:e3ea62cbe54470e5cc4c31cbc85693aa9b0b01723521baf0be634ea3597e1f5d`
- Build context: pinned reader packages, wrapper and synthetic tests only.

The CLI entry point was excluded by the global .NET `bin/` ignore rule; the
explicit source exception now makes a clean checkout reproducible.

**The original deployed Worker cannot execute this Docker verifier.** Its adapter launches
Docker with a networkless, read-only, non-root container and bounded CPU, memory,
PIDs, scratch and timeout. The ACA Worker image has neither Docker CLI nor daemon;
[ACA does not support privileged host access](https://learn.microsoft.com/en-us/azure/container-apps/containers).
An image build is not evidence that these runtime controls work in dev.

The 6 November implementation adds a dedicated
[custom-container Dynamic Sessions](https://learn.microsoft.com/en-us/azure/container-apps/sessions-custom-container)
adapter. It does not borrow the network-enabled AI pool or parse packages in
API/Worker. The repeatable module is `iac/azure/modules/publishing-verifier.bicep`.

- Environment: `aonik-dev-publishing-acae`, workload profiles enabled, no storage mounts.
- Pool: `aonikdevpublishing`; endpoint
  `https://aonikdevpublishing.icybay-1d1112c3.uksouth.azurecontainerapps.io`.
- Image: `aonikdevacr.azurecr.io/aonik-arke-verifier-sessions@sha256:edcf30cccff2a89f69523e2dabba590118999778cc2e623471c511217ab46dfb`
  (ACR run `db7`, 22 Dart and 9 Linux transport/limit tests).
- Python base: `python@sha256:09a17cad02668ae61ece136b4fd36ac3849e14f670ebedb637ea3e15580525bc`;
  reader and Dart base pins remain as above.
- Outbound networking disabled; no identity or credentials inside sessions.
  A separate pull identity has only ACR pull access. API/Worker system identities
  have Session Executor and Reader on this pool, not Contributor.
- Fixed qualification profile: 1 CPU / 2 GiB, 64 MiB scratch, 32 MiB archives,
  16 MiB assets; one ready session and a two-session cap. Azure rejected zero
  ready sessions and a lifetime below 300 seconds. One warm sandbox and its
  supporting infrastructure incur ongoing cost; this is not scale-to-zero.
- Each job uses sealed memfd input, a fresh nonce/session, 45-second reader
  deadline, a non-renewable 150-second watchdog and a 300-second Azure lifetime.
  The reader also has kernel address-space, CPU, process, descriptor and file
  limits. See the [provider contract](../features/arke-publishing-intake.md#azure-dynamic-sessions-provider).

The earlier session image (`4ece9f…`, run `db5`) passed all seven synthetic types
through the actual .NET adapter: world, listening, guide, music, story, comic and
part. A separate live probe confirmed UID 65532, blocked public/metadata TCP
connections, non-writable runtime directories and exactly 64 MiB scratch.
Azure hides the cgroup mount in this sandbox; cgroup introspection is not
evidence of its CPU/memory enforcement. The final image adds an independent
kernel address-space allocation-denial probe.

**Final-image qualification passed on 6 November at 05:18 UTC.** All seven valid
types passed through the real adapter; changed archive identity was held with
`verifier-upload-invalid`, and non-ZIP content was refused by the pinned reader.
The runtime probe confirmed the same network/UID/scratch restrictions and kernel
limits (2 GiB address space, 45 CPU seconds, 32 processes, 16 MiB file size), with
an oversized allocation actually denied. A deliberately abandoned job was
reclaimed after 160 seconds without caller cleanup; the subsequent active
session list was empty. The structured [evidence record](evidence/arke-verifier-2026-11-06.json)
contains image, reader, archive and fingerprint provenance without credentials
or session identifiers. The temporary operator Session Executor grant was
removed and its removal confirmed. Only the intended app grants remain.

Run `scripts/Test-ArkeDevVerifier.ps1 -CheckAbandonedJob` to inspect the fixed
runtime probes and observe reclamation without caller cleanup. Run
`dotnet run --project tools/ArkeVerifier.Qualification -- iac/azure/environments/dev/aca.parameters.json tests/Aonik.TestSupport/Publishing/Fixtures`
for seven positive packages, changed archive identity and an invalid archive.
The CLI runner uses the signed-in dev operator, while deployed applications
explicitly use their system-assigned managed identity. Temporary operator
Session Executor access must be removed after the rehearsal.

The application adapter from `2848f965` passed the protected
[dev image workflow](https://github.com/michaeljosiah/aonik/actions/runs/42166936001)
and normal deployment recorded above. Current API/Worker images include it and
select the closed remote-verifier profile. The actual deployed system-assigned
identity path still needs a live intake job before opening intake. API health
is 200 and the dev catalogue remains 404.

These tools do not submit creator material, accept distribution rights, approve
an edition or expose a catalogue. Keep admission closed until the final runtime,
deployed caller identity and remaining creator/Player checks are recorded.

Implementation validation on 6 November: full .NET regression **5,325 passed /
12 existing skips**, then **21 focused verifier tests** after the final transport
and active-image checks. Solution builds have zero errors; restoring current
NuGet audit data also surfaced existing transitive package advisories (Pkcs and
ImageSharp), separate from this change. Bicep compilation and PowerShell parsing
passed. Full live creator/reviewer and Player acceptance is still outstanding.

## Player and catalogue audit

Read-only audit on 5 November 2026:

| Source | Serial | Worlds |
| --- | --- | --- |
| ArkeStudio `arke-player-worlds` hosted release | 49 | 5 |
| ArkePlayer `origin/main` bundled catalogue | 49 | 5 |
| Existing local Player checkout | 45 | 5 |

Hosted catalogue SHA-256:
`6f05f91401e4752930b38c0936bc0da2ad08ad3ac6b2ed0552c9083dcda6ceca`.
Evidence is in ignored `output/research/403-catalogue-audit/`. Supported release
baselines and ownership/rights evidence still require qualification; this audit
does not establish a production import baseline or permission to republish.

[Player #50](https://github.com/michaeljosiah/ArkePlayer/issues/50) is still open.
Foreground discovery and background notices currently share a hard-coded
production GitHub URL. A rehearsal build must select the HTTPS dev channel in
both paths and isolate its bundled baseline, cache and app data from production.
Keep normal installed libraries/offline playback intact. Do not point production
phones at the empty test channel or replace the production GitHub catalogue.

## Live acceptance sequence

Record UTC, application/image versions, actor IDs, receipt/revision/operation IDs,
review digest, archive hash, validation provenance and catalogue serial for every
case. Screenshots must be from the actual signed-in applications.

1. Native creator sign-in, refresh rotation, cancellation, expiry and sign-out.
2. No-party denial; second-creator and foreign-tenant access denial.
3. Freeze a synthetic edition through Studio's existing exporter. Submit its
   declaration with rights confirmation; interrupt upload; resume only missing
   parts with the same receipt and operation IDs. Replay identical commands and
   prove changed hashes/arguments conflict without new public state.
4. Exact pinned verification to `in-review`; unavailable, timeout, malformed and
   tampered packages remain held. Prove private paths have no anonymous mapping.
5. Independent editor requests changes; creator submits a new frozen revision.
   Prove stale snapshots are rejected. Exercise rejection separately.
6. Editor inspects actual synthetic rights/material and explicitly approves the
   exact revision. Publisher separately confirms release. Approval alone must
   leave the catalogue unchanged.
7. Force a delivery readback fault, recover with the same durable operation,
   verify exact public SHA/length/MIME and a single higher-serial catalogue commit.
8. Dev Player foreground/background/forced refresh, install, part continuity,
   update and offline reopening. Include story/comic-aware and supported older
   consumers. Unavailable/invalid/equal/older feeds retain the appropriate cache.
9. Hold future delivery, then withdraw with a new serial. Retained immutable
   bytes and previously installed offline copies are not remote-deleted.

## Operations and recovery

### Synthetic rehearsal, 7 November 2026

`Set-ArkeDevPublishingStage.ps1 -Stage PrivateReview -ReviewerEvidencePath <private-receipt> -Apply`
activates only the fixed dev tenant/channel after checking the independently assigned
reviewer role and unchanged creator roles. `Delivery` additionally opens dev delivery
and discovery; `Closed` closes all channel switches. Each command defaults to a
read-only preview. The checked-in infrastructure profile remains closed, and a normal
deployment reapplies that closed profile. Reapply the intended rehearsal stage only
after the deployment is healthy. The GitHub production mirror stays disabled.

API revision `0000381` and Worker revision `0000371` were healthy with private review
enabled. The first native Studio submission created receipt
`fd5b2a73-3cdb-40e3-98e8-509ba1606880` for the owner-confirmed synthetic edition
`10047792c721bdb60da54aedf10c281265d360151e760e3a6415c323e49a362c`.
Upload then paused on an unsupported `chmod` against Azure Files. The creator resumed
that same saved receipt after the storage adapter fix; no new request identity was made.

At 13:35 UTC the fix from `5553bb20` was deployed by CLI to healthy API revision
`0000382` (100% traffic) and Worker `0000372`. Both images passed the critical
vulnerability gate with no secret findings (6 API / 10 Worker high findings remain).
GitHub had no assigned runner, and ACR source builds exhausted compiler memory;
the fallback cross-published committed sources for Linux locally and packaged the
unchanged non-root runtime stages in ACR. Only publish output entered those build
contexts. Keep ignored `output/` and `.tools/` out of future workspace build contexts.
The [deployment evidence](evidence/arke-storage-deployment-2026-11-07.json) pins both
digests. At that deployment checkpoint health was 200 and public discovery was 404.
The later live results below are separate from that deployment evidence.

The dev profile explicitly selects `Publishing:Storage:AccessControl=AzureFilesSmb`
and the dedicated account `ankpubxkhim4xqiopna`. Before writes, Linux mount metadata
must identify the exact root, CIFS filesystem, writable mount and superblock, and the
expected `intake` or `delivery` share. Missing mounts, nested binds, other accounts,
read-only shares and unknown modes refuse. Local mode remains the default and keeps
Unix owner-only roots. No permission error is silently ignored. Azure Files access
is enforced by the authenticated ACA storage binding; these shares are not static
file mounts. See [Microsoft's SMB mounting guidance](https://learn.microsoft.com/en-us/azure/storage/files/storage-how-to-use-files-linux).

The rehearsal stage uses 1 MiB parts within the existing 32 MiB package cap, so the
2,354,240-byte real Studio export exercises multipart recovery without increasing
the accepted archive size. This is a synthetic test configuration, not a capacity
qualification. Recovery resumed the original receipt before any successful part;
this checkpoint did not prove interruption after an accepted chunk or a process
restart. The subsequent private failure rehearsal below proves both separately.

At 14:36 UTC the original receipt completed all 2,354,240 bytes and the deployed
Worker's managed-identity job passed the pinned Azure Dynamic Sessions verifier.
The independent reviewer downloaded the private package and checked its exact
SHA/length, story and geometric artwork. Anonymous private-package, upload-state
and reviewer-package requests returned 401. Following the owner's explicit exact
revision approval, decision `2254df5d-143b-466b-9421-83eda49cff06` committed;
an identical replay returned the same decision and changed arguments returned 409.
Approval alone left public discovery at 404.

The owner separately authorised dev publication and withdrawal. `Delivery` stage
produced healthy API `0000383` and Worker `0000373`, retaining the scanned image
digests. The empty dev baseline committed serial 0; publication operation
`b51122d2-7923-45e3-9d65-2aa767f5a244` committed serial 1 on its first attempt.
Its replay returned the same operation. Public ZIP/artwork readback matched every
declared SHA, byte count and MIME type. Studio's original receipt displayed
**Published** and the independent review message after refresh. The production
catalogue remained byte-identical at serial 49 with five worlds; the mirror is disabled.

The first dev Player install exposed a missing refresh on P9a when the bundled
catalogue was empty. First open now starts the existing shared catalogue refresh,
retaining the designed **Install a world** and **Add a file** actions. Targeted tests
passed 31/31, the full app suite passed 246 tests with 13 existing skips, strict Dart
analysis and formatting passed, and the design gate passed 21 turns / 166 rules.
The fix is Player commit `1b80ab7`, with SPEC-058 clarification `044729b`.
The APK update preserved emulator
app data; Android reclaimed disposable caches after an insufficient-storage refusal.
The real Player core downloaded, verified and installed the public edition, then
reopened its persisted library and read the matching local EPUB. On the Pixel 9
emulator, Discover showed the hosted edition, Install completed, and the reader
rendered both the cover and complete **A Small Beginning** chapter.

Desktop capture intermittently returned `FrameArrived timed out`. A cold boot of
the same AVD with `-gpu swiftshader -no-snapshot-load` restored reliable capture;
no app data was cleared. Before relaunching Player, Android airplane mode was
enabled and the Internet tile showed **Airplane mode**. The retained world and
complete chapter then rendered offline. Networking was restored after the check.

The authorised hold committed and identical replay returned the same operation.
The release preview reported `publication-held`, while the catalogue remained at
serial 1. Withdrawal operation `d2fcf8af-bdd6-41d3-8b2e-344977c576d3` committed
serial 2 on its first attempt; replay after completion returned the same operation.
The public feed became empty, while the immutable ZIP remained HTTP 200 with its
original SHA and length. Player's forced refresh displayed **Checked** and removed
the world from Discover. Its persisted cache contained serial 2 with no worlds,
while the installed library, EPUB hash and Read action remained intact.
Studio refreshed the original receipt to **Withdrawn** at 15:19:49 UTC. The temporary
reviewer harness was signed out and stopped; Creator 1's Studio session was preserved.
See the [live evidence](evidence/arke-live-rehearsal-2026-11-07.json) for exact identities
and remaining acceptance cases. This synthetic base-book run does not qualify all of #403.

## Private upload failure rehearsal — 7 November 2026

The owner requested merge and continuation. AONIK [#407](https://github.com/michaeljosiah/aonik/pull/407),
Studio [#2752](https://github.com/michaeljosiah/ArkeStudio/pull/2752), Player
[#54](https://github.com/michaeljosiah/ArkePlayer/pull/54), and specification PR #134
were squash-merged at 15:32 UTC. AONIK used the owner's prior force-merge authority
because required GitHub checks remained queued without an assigned runner.
Queued checks are not passing CI. The local validation limits in the validation section below
still apply; merging did not replace the deployed runtime images.

At 15:41 UTC the real Studio `CreatorPublishingService`, `AonikPublishingApi` and
normal Creator 1 account adapter submitted another **private** receipt using the
same owner-confirmed synthetic archive and declaration. An isolated Electron
process exited after the server accepted the first 1 MiB part but before Studio
recorded its acknowledgement. A fresh process loaded the durable receipt as
**Paused**, with zero locally acknowledged bytes. Resume reconciled the server
state and sent only parts 2 and 3, retaining receipt
`acf682c0-3141-4fb5-835d-89c566eb3165`, revision
`e768da08-740d-4ad1-b451-16fe4522c324` and upload
`8d189b7c-bc0a-4803-9464-4f609649eeec`. The deployed Worker verified the archive
at 15:41:51 UTC and the receipt reached **In review**. No editorial approval or
publication was requested for this new receipt.

Narrow authenticated API probes then established these results:

| Case | Observed result |
| --- | --- |
| Identical create and submit replay | Same receipt/revision and validation report |
| Changed archive declaration with reused request ID | 409 `request-id-reused` |
| Wrong submit revision / stale replacement version | 409 `revision-changed` / `review-version-changed` |
| Creator reading reviewer or channel-management routes | 403 |
| Invalid bearer / unresolved tenant header | 401; these do not prove expired auth or configured cross-tenant isolation |
| Complete before all parts / submit before complete | 409 `parts-missing` / `package-not-uploaded` |
| Malformed digest / short or out-of-range part | 422 `invalid-part-digest` / `invalid-part-length` |
| Correct declared part hash with corrupted transport bytes | 422 `upload-bytes-mismatch`; zero bytes counted |
| Retry the correct bytes after corruption | Accepted; identical retry did not increase received bytes |
| Changed identity for an accepted part number | 409 `part-identity-conflict` |
| Cancel and repeat cancellation | Both 204; upload remains cancelled |
| Write to the cancelled upload | 409 `upload-superseded` |
| Start replacement, then repeat the old cancellation | New session remains open |
| Individually valid parts whose assembled archive differs from the declaration | 422 `upload-bytes-mismatch`; session retired as failed and submission stayed awaiting upload |

The malformed-byte checks used a separate private receipt
`617d6c5e-5bb6-4781-a8cf-051f0ecdc784`; its altered bytes never entered verification
or editorial review. Failed/cancelled bytes remain subject to normal retention
cleanup. No immediate quota reclamation or physical-deletion claim is made.
The public dev catalogue remained empty at serial 2. The original released edition
remains withdrawn, and this run made no production requests that mutate state.

The [sanitized evidence](evidence/arke-negative-rehearsal-2026-11-07.json) records
timestamps, exact identities, HTTP outcomes, merge commits and remaining cases.
The service interruption is a real process restart using Studio's implementation;
the additional HTTP probes are not new UI coverage. At this checkpoint, Creator 2
cross-party checks awaited a fresh sign-in; the later results below complete them.
Independent request-changes/rejection was also pending then; the editorial
results below complete that sequence. Real expired-auth/no-party and configured
cross-tenant checks, delivery failure recovery, supported Player variants,
capacity and restore qualification remain open.

To repeat recovery in an isolated dev workspace, keep the frozen export and saved
request identity, stop the uploader after a successful part acknowledgement, and
restart Studio with that workspace. Resume the existing **Paused** receipt, then
verify that only server-reported missing parts are sent. For corruption tests use
a separate private receipt; never alter the original approved edition or release
malformed bytes. Retain IDs for cancellation/replay checks, and check catalogue
serial and contents after each test. Do not reset receipt files or database state
to make a failed run look successful.

## Cross-account qualification — 7 November 2026

Creator 2 completed fresh Studio code + PKCE sign-in at 21:21–21:25 UTC. The
expired afternoon callback was correctly refused; the new receiver accepted only
its matching request. No callback state validation was weakened.

At 21:25–21:29 UTC, **21 live denial probes passed** using Creator 2's real identity
in the same tenant as Creator 1. Creator 1 first opened an empty private upload
on the existing failure-test receipt, so write checks targeted a session that was
actually writable by its owner. Both accounts' authenticated access was verified.

| Cross-account case | Observed result |
| --- | --- |
| Creator 1 receipt, uploaded/open sessions, private ZIP, validation preview and decision history | 404 |
| Withdrawn receipt and its retained private ZIP | 404 |
| Supplied owner user/party headers | Still 404 |
| Begin upload, send a correctly hashed 1 MiB part, complete or cancel upload | 404 `submission-unavailable` |
| Submit the owner's validated package or replace its revision with a valid declaration/current version | 404 `submission-unavailable` |
| Reviewer detail/list, reviewer ZIP/artwork, release preview and channel management | 403 |
| Creator 2's own list, including a supplied owner-party query filter | 200 with zero receipts; no Creator 1 records |

Creator 1's positive controls returned 200 for the receipt, upload, preview,
decisions and private package. Before/after snapshots matched exactly, including
rowversions, revision IDs, validation preview, decision history and upload state.
The writable upload remained open with zero received bytes. The downloaded archive
still matched all 2,354,240 bytes and SHA-256
`10047792c721bdb60da54aedf10c281265d360151e760e3a6415c323e49a362c`.
After this comparison, Creator 1 cancelled the empty fixture successfully (204).
The temporary Creator 2 harness signed out and stopped; native Creator 1 Studio
remained signed in.

The public catalogue remained empty at serial 2. No application/schema fix,
additional release or production write was needed. These results qualify
**same-tenant cross-party isolation** for the exercised routes; they do not prove
a configured second-tenant boundary or the remaining identity/delivery
matrix. The [sanitized evidence](evidence/arke-cross-account-rehearsal-2026-11-07.json)
contains each HTTP outcome and matching owner snapshot hashes. #403 remains open.

## Editorial revision and rejection rehearsal — 7 November 2026

The independently authenticated reviewer resumed with the same private test
receipt `acf682c0-3141-4fb5-835d-89c566eb3165`. At 21:33 UTC, private download
readback matched the original archive hash and length. Revision 1's review digest
was `fbf59c3362c6e35a06780bc914dea8bf442b00abac856b95f97daa50f84aed08`.
The reviewer recorded **Request changes**, explicitly asking for a clearer
dev-only submission summary as a workflow rehearsal. This was not an approval
or a substantive adverse finding about the synthetic story.

Studio's real publishing service refreshed Creator 1's receipt and received that
message. It froze a new local candidate from the same owner-confirmed export and
submitted changed immutable metadata as revision 2 of the same receipt:
`669d306a-99f7-493a-ae00-96a52a20a04f`. The archive itself was unchanged;
the summary now explicitly names the isolated dev channel. Studio uploaded all
three parts to a new session, completed and submitted it. The deployed verifier
passed report `e8943be4-8493-407c-8f3b-71d0532281e1` at 21:34:36 UTC.
The new revision received a different review digest and retained revision 1.

The reviewer then recorded **Reject** for revision 2 to complete the private
failure workflow. Both decisions replayed to the same IDs. Changed replay
arguments returned 409 `decision-operation-conflict`; stale versions returned
409 `review-version-changed`. The old digest against the current revision returned
409 `review-material-changed`, and the old revision against the current version
returned 409 `review-revision-changed`.

Creator 1 refreshed through Studio's service and received **Rejected** plus both
revision-specific messages. Its earlier local receipt became **Superseded**.
The creator history exposed no internal reasons, reviewer user/party IDs or
approval-check fields. The reviewer history retained the first decision as
inactive and the new rejection as active. Public discovery remained empty at
serial 2; no new approval or release was requested. The temporary reviewer
harness signed out and stopped, preserving native Creator 1 Studio.

The [editorial evidence](evidence/arke-editorial-rehearsal-2026-11-07.json) records
the exact decision IDs, validation provenance, revision identities, request
outcomes and final creator feedback. This is live service/API proof, not additional
UI coverage. No runtime or schema fix was required. Remaining #403 work includes
configured cross-tenant/no-party/expired-auth cases, delivery fault/retry,
supported consumer variants, production rights, capacity and isolated restoration.

## Controlled delivery readback fault — dev only

`scripts/Set-ArkeDevDeliveryReadbackFault.ps1` can temporarily deny the single
observed Worker source IPv4 at the **dev API ingress**. All other clients remain
allowed. This uses Azure Container Apps [IP restrictions](https://learn.microsoft.com/en-us/azure/container-apps/ip-restrictions);
it does not change approval, delivery policy, artifact bytes or database state.
The observed source must be requalified for the current Worker replica. Do not
reuse an old pod IP after a restart or assume the outbound public IP is used for
traffic within the Container Apps environment.

1. Confirm there is one Worker replica, no other active delivery operation and
   no existing dev ingress restriction. In a temporary Worker debug console,
   probe a retained dev artifact with `curl --write-out` and record HTTP status,
   `local_ip` and `remote_ip`. Normal clients must also receive HTTP 200.
2. Run `-Action Preview -WorkerSourceIp <observed-ip>` with a private `-StatePath`.
   `Block -Apply` records the empty baseline and arms a detached restore watchdog
   before adding one named `/32` deny rule. It temporarily denies all API requests
   from that source, so keep other Worker-to-API work out of the rehearsal window.
   The default deadline is 600 seconds; an automatic-exhaustion test may explicitly
   select up to 3,600 seconds to cover the existing exponential backoff without
   changing Worker retry settings. `-AutoRestoreSeconds 60` is available for watchdog
   recovery qualification. The deadline
   starts the restore; Azure's control-plane update can take additional time.
3. Qualify the fault against the retained artifact before publishing: Worker
   readback must return 403, while operator health/catalogue reads remain 200.
   Record the catalogue serial and confirm it did not change. If these checks
   fail, restore immediately and do not claim a delivery fault was exercised.
4. For an independently approved exact revision and explicitly authorised dev
   release, retain the original publication operation through failure and retry.
   Expect `uncertain` / `public-transport-uncertain`, an unchanged catalogue head
   and no current edition. Use the existing `/publishing/publications/{id}/retry`
   API with the current operation version and a durable retry request ID.
5. Run `-Action Restore -StatePath <same-file> -Apply`. Confirm the state records
   `originalBaselineRestored: true`, Worker readback returns 200, and the original
   operation commits once. Replays must not create another catalogue promotion.
   Withdraw the synthetic edition using a new higher serial after verification.
6. Exit the temporary debug console, inspect the watchdog's output/error logs
   beside its state file, and verify no rehearsal restriction remains. The
   watchdog also exits promptly after an explicit successful restore.

Recovery removes only `arke-dev-403-readback-rehearsal` when its name, description,
action and source match the saved record. It preserves unrelated operator rules
and reports if the original empty baseline is no longer restored. If the helper
cannot run, inspect the rule and remove that exact owned rule with
`az containerapp ingress access-restriction remove --subscription 336d38e2-bb96-4510-a4c5-5c313139eef9 --resource-group rg-aonik-dev-uksouth --name aonik-dev-api --rule-name arke-dev-403-readback-rehearsal`.
Never clear all ingress rules as a recovery shortcut.

On 7 November, the preflight observed Worker source `100.100.0.212` and API
internal address `100.100.242.87`. Both manual restoration and the 60-second
watchdog qualification passed (200 → 403 → 200). Normal health and catalogue
reads stayed available, with the empty dev catalogue at serial 2. This preflight
alone is not proof of a failed publication or successful retry.

### Live result — 7 November 2026

Studio froze and uploaded revision 3 of receipt
`acf682c0-3141-4fb5-835d-89c566eb3165`, preserving the original synthetic archive
hash and adding an explicit delivery-rehearsal summary. The deployed verifier
passed report `6fc86383-c1b2-4a34-a8ad-9af882a9f3d4`. The owner separately confirmed
the exact independent editorial decision and dev release/retry/withdrawal.
Approval `5f6afa10-52f7-435f-9331-d4ff7dade8c6` is bound to revision
`5516467b-7dfa-43ee-94a7-4093700bcc05` and review digest
`62988eabc500b5142a15ecad45680132fdb8c77876ad511cdd17415c5518abcf`.

The live fault was applied at 22:17 UTC. The deployed Worker copied the archive
but could not complete public readback. Publication operation
`f5f85e15-c520-47df-9f36-5ebd12127231` became **uncertain** with
`public-transport-uncertain`; its claim was released and the receipt returned to
Approved. The catalogue stayed empty at serial 2 with no new published edition.
The operator could still download the retained archive at its original hash and
length while the Worker-network probe received 403.

After restoring ingress, the normal retry API accepted that **same operation**
and recorded one retry audit, `3a61b0cf-55b5-497a-8b46-47a522cd02bd`. It committed
one edition at serial 3. The ZIP retained artifact ID
`3f5a20c5-0cb4-4b4c-88f9-1ba46e914f78`; retry completed the artwork at
`cb9001b7-c002-4c4d-be85-6557e45c6bc8`. Both public URLs returned 200 with exact
MIME type, declared length and SHA-256. Replaying publish and retry returned the
original operation without another promotion. Changed retry arguments returned
409 `publication-operation-conflict`; a fresh retry of the committed operation
returned 409 `publication-not-retryable`.

Withdrawal operation `50b4d3f6-249b-4b6a-a584-149d5c55d980` then committed serial 4
with zero discoverable worlds. Both immutable URLs remained readable and exact.
Studio's real receipt refresh showed Approved → Published → Withdrawn for the
same frozen candidate. Production remained serial 49/five worlds with its
original feed hash; the GitHub mirror remained disabled. The ingress baseline
was restored and the temporary debug console exited.

The operator harness initially looked up the idempotency request ID as if it were
the server operation ID. It stopped before any retry write; using the saved
publish response corrected that lookup. No product-code, schema or deployment
change was needed. The solution build passed with zero errors and 60 existing
dependency/analyzer warnings. The full solution suite passed **5,352 tests with
12 existing skips**, including all 787 API tests and 269 database tests. The
initial sandboxed run could not connect to its test host; it was stopped before
tests ran, and the complete rerun outside the sandbox passed. PowerShell parsing,
JSON evidence assertions and diff whitespace checks also passed.

The [sanitized evidence](evidence/arke-delivery-rehearsal-2026-11-07.json) includes
the failed/accepted/committed operation snapshots, retry audit, artifact identities,
replay refusals, creator states and restoration timestamps. This qualifies a
transient public-readback fault and recovery through the deployed pipeline. It
does not qualify attempt exhaustion, mirror failure, the remaining identity
matrix, further Player variants, production rights, capacity or backup restore.
#403 remains open.

## Configured identity boundaries — 8 November 2026

The [sanitized identity evidence](evidence/arke-identity-rehearsal-2026-11-08.json)
records **41 successful denial probes**: 16 across configured tenants, seven
with a missing party relationship, and 18 with missing or malformed bearer
credentials. These use the deployed API and the existing Studio creator session.
They do not substitute malformed credentials for a genuinely expired token.

The nominated reviewer's existing PlatformAdmin membership in the Aonik Dev
Tenant created **Arke 403 Identity Isolation 2026-11-08** through the normal
admin API. Tenant `44241bd4-14af-4221-9a85-007c11c0c33e` uses the Arke Worlds
pack with publishing enabled. The normal invitation API admitted Creator 1 as
PersonalUser. No elevated creator role, new content submission, or second-channel
distribution declaration was introduced. The empty fixture tenant is retained
for further identity checks.

The same real Auth0 identity resolves to distinct AONIK user/party IDs in the two
tenants. Both `/v1/me` calls return 200, and the new tenant's creator list returns
200 with zero receipts. Requests for the original tenant's receipt, package,
preview, decisions, upload reads/writes, submission and revision replacement
return 404. Spoofed owner headers and owner/tenant query parameters do not escape
the boundary. Reviewer/publication/management routes return 403. Owner-positive
receipt, preview, decision, package and writable-upload snapshots are identical
before and after the denials. The owner then cancelled the empty upload fixture.

For the no-party case, an operator-only EF harness temporarily soft-deleted the
single newly created fixture UserParty relationship. It pinned the dev database,
tenant, user, party and reviewer, refused a tenant containing publishing content,
and wrote apply/restore audit records. It did not alter Creator 1's original
tenant relationship. The authenticated fixture identity remained Active with
`partyId: null`; all seven creator routes returned **403 `no-party`**. A `finally`
block restored the same relationship ID. A fresh API check confirmed the original
party ID and a successful empty creator list. The temporary single-client-IP SQL
rule was removed, with an independent timed cleanup also scheduled.

The initial restoration-evidence query timed out after the relationship had
already been restored. Separate API checks verified restoration. Read-only
diagnostics found approximately 720,000 audit rows and only the primary-key
index. Three tenant-scoped audit indexes were generated through the canonical
EF migration stream and applied to dev; no schema operation was authored by hand. Admin tenant,
user and role pagination also returned positional arguments in the wrong order.
Those three constructors now use named arguments; regression checks cover
filtered middle pages and empty results.

The solution build passed with zero errors and 59 existing warnings. The final
full solution run passed **5,355 tests with 12 existing skips**, including all
787 API and 269 database tests. It used `DOTNET_PROCESSOR_COUNT=4` to bound test
concurrency. The initial broad run was interrupted after an API timeout while
the model was rebuilding; the complete final run passed. All migrations,
including the three new audit indexes, applied cleanly to a fresh LocalDB
database. The first dev index build exceeded the five-minute DDL limit and
rolled back, leaving only the original primary-key index. After confirming no
blocking session, the same migration completed at **07:49:05 UTC**, using a
bounded 20-minute command timeout for the one-off canonical context runner.
The three commands took approximately 392, 300 and 49 seconds on the existing
10-DTU database. Its service tier was not changed. For a similarly sized audit
table, pre-apply this canonical migration with an adequate maintenance timeout;
the normal Migrator host's five-minute default was insufficient in this rehearsal.

The first API lookup then hit synchronous SQL automatic-statistics generation
(`SELECT (STATMAN)`), interrupted by the API's 30-second deadline. Executing the
same parameterized **read-only** count with a bounded longer timeout let those
statistics finish in 60.5 seconds. The normal audit endpoint subsequently returned
200 with both expected records in **78 ms**. Apply audit
`401ea88e-b402-4a4a-92a0-13c62c09b800` and restore audit
`70d3c4d2-37e8-4323-9cbc-49d8d14fe464` prove the party relationship was absent
only from 07:12:53 to 07:12:54 UTC; the later delay was retrieval, not restoration.
The migration and query warm-up both removed their temporary SQL firewall rules.

[CI 42459529234](https://github.com/michaeljosiah/aonik/actions/runs/42459529234)
and [image build/scan 42459535358](https://github.com/michaeljosiah/aonik/actions/runs/42459535358)
passed for source `5f69585208a3070acabd00b5c17fea665a2e1779`. API, Worker and
Admin UI were updated through the CLI to immutable image digests; all three
`arke403-5f695852` revisions were Running by 08:01:22 UTC. Environment, volume,
mount, resource, scale and ingress fingerprints match the previous revisions.
The active publishing configuration was preserved rather than reapplying the
checked-in closed-channel defaults.

Live tenant, user and role lists now return correct counts (5, 2 and 5 in the
selected test contexts), page 1 and page size 100. The empty tenant search
returns zero items/count/pages. On the new API, both creator tenant identities
remain accessible, the owner receipt returns 200, the other-tenant receipt
returns 404, and all 18 missing/malformed bearer probes still return 401.
Final documentation/evidence updates do not alter the tested runtime source.

**Genuine expiry passed at 14:42:10 UTC on 8 November.** The actual Creator 1
token was accepted for its owner receipt before expiry and its signature verified
with Auth0's JWKS. After its original **14:36:10 UTC** expiry, the watcher allowed
the API's five-minute clock tolerance plus one minute. All five routes—identity,
submission list, receipt, package read and upload creation—returned **401 with an
expired-token challenge**. No refresh token was used, and the protected access-token
fixture was deleted after the result was recorded. Auth0 lifetimes, JWT claims,
server time and shared authentication settings were unchanged. The sanitized
[identity evidence](evidence/arke-identity-rehearsal-2026-11-08.json) includes the
positive control and each expired response; no credential belongs in Git history.

The identity batch ended with empty dev discovery at serial 4; the later approved
Player release/withdrawal cycle below ended empty at serial 6. Production retained
the same catalogue asset digest, previously parsed as serial 49 with five
worlds and SHA-256
`6f05f91401e4752930b38c0936bc0da2ad08ad3ac6b2ed0552c9083dcda6ceca`.
The broader supported-consumer, production-rights, capacity, backup and pilot
acceptance work remains open under #403.

## Operational safeguards and recovery

- Keep intake closed if verifier capacity, identity setup or storage readiness is
  unknown. Keep review and delivery separate; an outage must not grant approval.
- Inspect receipt validation codes and outbox attempts; unavailable verification
  is held. Resolve the infrastructure cause and use a fresh validated revision
  where required. Never patch approval, validation or outbox success in SQL.
- For uncertain delivery, retain the original operation, reviewed revision and
  expected lineage. Use authorised retry; do not fabricate another public URL.
  Dead-letter and mirror states are separate from canonical publication.
- Monitor real private/public free space, pending age, verifier concurrency,
  failures, outbox dead letters and Worker replicas. Agree measured capacity and
  cost limits before pilot expansion; no throughput qualification exists yet.
- Back up SQL and both shares with `.store-id` together. Seven-day share deletion
  retention is not a backup. Prove a restore in isolation before enabling a pilot.
- Roll back application images only after schema compatibility review. Roll back
  public discovery by a **new higher serial**, never by restoring an old feed over
  the current head. Preserve existing hashes/URLs and publication history.
- Private manual intake and verified operator publication handoff remain the
  fallback. No production manual or automated upload is part of this run.

## Player consumer compatibility — 8 November 2026

The consumer matrix uses actual Player source, rather than an independent parser:

| Consumer | Exact source | Scope qualified |
| --- | --- | --- |
| Current worlds-first Player | `17dc9a23c8bd550dc7116ef61d37a53319e15081` | Eight scenarios across seven AONIK-generated catalogue types, including coverless parts, exact pins, installation, repeated installation, part continuity and network-free library reopening; future story/comic versions are not downloaded. |
| Last published Android preview, `v0.1.0-preview.20261026.update` | `03a261bda82bc6ec07d66f980506143ecba430a8` | The same catalogue matrix; supported world/listening/guide/music/part content remains usable. Story/comic entries are safely ignored and never fetched. |
| Earlier dev APK from the first rehearsal | `1b80ab72067bdb91642adaf57cf1bff4c6735f8d` | Previously qualified base EPUB discovery, install, reading, withdrawal refresh and offline cold launch. Its retained library is the input to the current APK upgrade check. |

The published preview is version `0.1.0`, Android version code `2001`, ARM64,
Android 7+. It is the historical compatibility floor exercised here, **not** proof
that its released APK has passed on a physical phone. Current dev is Android x64,
`studio.arke.player.dev`, version `0.1.0+1`. No Google Play candidate or iOS build
is qualified by these results. Older unlisted builds remain unqualified.

`qualify_consumers.dart` consumes the eight complete catalogue scenarios emitted by the
real .NET delivery tests. It checks every retained pin's length and SHA, then uses
the selected checkout's real installer. Only the test HTTP transport maps the
synthetic `public.example.test` authority to loopback; certificate checks and
production configuration are unchanged. Standalone productions use a local base
fixture; the guide uses the real file-import path. All libraries reopen after the
local artifact server closes. This proves format/installer compatibility, not an
editorial approval or a deployed publication of these fixtures.

`qualify_dev_player.dart` separately installs the already-approved synthetic EPUB
from its retained immutable dev HTTPS URL with each reader. Forced refresh advances
the saved serial-1 feed to the live, empty serial 4, repeated install is stable,
and the installed EPUB reopens without network access with its original SHA.
Withdrawal removes discovery; a previously known immutable URL remains downloadable,
as intended by the retention policy. This is not artifact revocation.

The checks found that the AONIK projection discarded the schedule declared inside
a verified part. The projection now updates only the affected production's schedule,
preserving an existing declaration when the next part omits it. Regression tests
cover the schedule, unrelated production and unchanged base fingerprint.

A further check found that Studio parts intentionally omit the base cover, while
the submission/approval path required another image. Parts may now use an empty
artwork asset ID to inherit the approved base cover. The existing public cover pin
is preserved, and all included assets still require rights evidence. A real,
image-free synthetic part exercises approval and delivery; whole editions still
fail approval without their own verified artwork. Studio has the matching frozen
candidate/schema change. These fixes require deployment before live qualification.

The current APK was installed over the earlier dev APK without clearing data; its
library was byte-identical immediately after upgrade. After an emulator reboot,
airplane mode was enabled before the first Player launch. The retained cover and
complete chapter rendered, and the EPUB hash remained unchanged. Network access
was restored. **You → Check for updates** displayed **Checked** and saved the empty
serial-4 catalogue; Worlds no longer listed the edition while Library retained it
and its reading position. Android System UI became unresponsive under the initial
host load, then recovered after reducing test concurrency and waiting; this is an
emulator observation, not a physical-device reliability result.

To reproduce the local matrix, set `AONIK_PLAYER_QUALIFICATION_DIRECTORY` to a
temporary output directory and run `PublicationDeliveryTests`. Resolve each exact
Player checkout's dependencies, then invoke its Dart SDK with its package config:

```text
dart --packages=<player>/.dart_tool/package_config.json tools/arke-package-verifier/test/qualify_consumers.dart <projection-output> <player> current <result.json>
```

Use `legacy` for the published preview. The live probe takes the retained,
approved serial-1 dev snapshot, exact reader commit and evidence output path.
Do not substitute an unreviewed package or publish these local fixtures.

The current pure-Dart world suite passed **376** tests; the historical suite passed
**227**. The current Flutter suite passed **408**, with **13** existing skips, and
the explicit development-channel/refresh suite passed **13**. The native PDFium
download failed during first setup; the rebuild reused the same version's binaries
already qualified by the previous dev build. The application source was unchanged.

Both readers also passed a local whole-edition update using two actual Studio
exports of Lantern Garden (7 and 8 November). The library stayed singular, retained
the EPUB reading position and reopened offline. The chapter bytes are identical;
the regenerated EPUB publication metadata has a new asset hash, which the installer
matches exactly. `qualify_edition_update.dart` reproduces this file-import check;
it is not a live approval/publication or an Android update result.

Final focused AONIK publishing regression passed **129** tests; Studio publishing
passed **32**, workspace type-checks passed, and lint reported zero errors with eight
existing warnings. The solution build passed with zero errors. Final functional commit
`03ba5abfcfbf0c8a0b88b9fafc5248b65597eec3` passed [GitHub CI](https://github.com/michaeljosiah/aonik/actions/runs/42473620611):
**5,370 .NET tests**, five existing skips, and **690 Admin UI tests** across 70 files.
Local full-suite attempts hit unrelated API HTTP timeouts,
and a restricted test-host attempt could not establish its runner connection; these
are not recorded as passing runs. The final host-connected publishing rerun passed,
and the 13 affected API tests also passed in isolation.

The immutable API, Worker and Admin UI images for that functional commit were deployed
to dev through the CLI at 11:32 UTC on 8 November. [Image build and scan](https://github.com/michaeljosiah/aonik/actions/runs/42473695138)
passed on attempt 2; attempt 1 failed downloading Trivy before scanning and caused
no rollout. Environment, volume, mount, scale, resources and ingress fingerprints
match the pre-deployment baseline. Health returned 200, anonymous private receipt
and review requests returned 401, and both the native creator and independent reviewer
could still read the verified, private receipt. Both actual Player readers repeated
the retained-HTTPS install, empty serial-4 refresh and offline reopen after deployment.

Studio's broader CI exposed audio fixture failures already present on its main branch
after direct PCM duration measurement landed. The paired Studio PR repairs the actual
PCM durations and constructs legacy sidecar metadata through owned commits. The broad
local run passed 167 tests with two skips and one restoration failure; the final
restoration and two legacy-metadata cases then passed after repair, with typecheck
and lint passing. [Final Studio CI](https://github.com/michaeljosiah/ArkeStudio/actions/runs/42476402823)
passed all 21 Linux/Windows and build jobs for `128db8572488c9820eea516822290876e264c170`.
Studio PR #2838 and the paired private specification PR #141 are merged. The earlier
broad local run is retained honestly as a failed intermediate run.

Withdrawal is terminal for the current review workflow. The operator's authorised
base re-release therefore produced a new private receipt of the identical original
archive, which passed server verification. The owner separately approved receipt
`fbbb1317-8c13-4062-ba00-90b9c3cab0d6`, revision 1, digest
`3e6ca4cb6eda9db8067c58a0f9d84ee6ee2470c907d3e4c78173cc81e6b3f8ad`.
That decision was recorded at 16:10:21 UTC, publication committed serial **5** at
16:10:50, and the authorised withdrawal committed empty serial **6** at 16:17:55.
Each operation completed in one attempt. The old withdrawn approval was not reused.

Both actual Player readers discovered the live serial-5 edition, downloaded its
exact approved archive, repeated installation without changing their library, and
reopened the EPUB without network access. The same libraries then refreshed to
empty serial 6 and reopened unchanged. On the current Android emulator, the UI
showed the discovery card, cover and complete chapter. After withdrawal, its
persisted feed and discovery UI were empty, while its library JSON—including
reading position—was byte-for-byte unchanged. This new cycle does not claim a new
Android offline cold launch; that separate earlier observation is recorded above.
Published immutable ZIP/artwork URLs still returned their exact hashes after
withdrawal, as the retention contract requires; the private receipt package route
still returned 401 anonymously. Production release-asset metadata retained the
same digest and update time. See the [release-cycle evidence](evidence/arke-player-release-cycle-2026-11-08.json).

`qualify_release_cycle.dart` reproduces the two phases against a live dev base:

```text
dart --packages=<player>/.dart_tool/package_config.json tools/arke-package-verifier/test/qualify_release_cycle.dart published <fresh-scratch-directory> <reader-commit> <published-serial>
dart --packages=<player>/.dart_tool/package_config.json tools/arke-package-verifier/test/qualify_release_cycle.dart withdrawn <same-scratch-directory> <reader-commit> <withdrawn-serial>
```

Run the first phase while the exact synthetic base is approved and published,
and the second only after its authorised withdrawal. The tool does not publish,
withdraw, or delete its evidence. Keep separate scratch libraries for each reader.

Remaining acceptance is explicit: actual scheduled Android notification delivery
for a newly approved part, live AONIK publication of part/update/story/comic editions,
physical and Play-installed device tests, and production baseline/rights qualification.
Old hard-coded clients still use GitHub. Their eventual updates require an explicitly
approved catalogue mirror; this rehearsal does not enable that mirror or change the
production feed. See the [consumer evidence](evidence/arke-player-compatibility-2026-11-08.json)
for the exact distinction between local, live transport and device observations.

## Newer-edition checkpoint and reader fix (8 November)

The `2026-11-08` whole edition is frozen as local candidate
`91f61711-6da5-41ed-952e-9f1107351750`, archive SHA-256
`c6253edce1162fa70c1ac01f5b99d9c125c5f9c266c2375aacb2d6fc2653850b`.
The story XHTML and artwork are byte-identical to the approved synthetic base;
only the EPUB publication metadata changed. **It is not yet uploaded, approved or
published.** Creator 1's native session expired and Studio's receipt refresh requests
a new sign-in. Dev remains empty at serial 6. The earlier exact base approval is
not an approval of this newer edition.

Preparing the Android checkpoint exposed a real reader defect: the short EPUB's
coarse CFI percentage reached 100% on page 1 of 2. Player marked it finished and
reopened at the cover. [Player #66](https://github.com/michaeljosiah/ArkePlayer/pull/66)
and [the paired specification](https://github.com/michaeljosiah/arke-studio-specs/pull/142)
are merged. Completion also requires the last reported chapter page; absent or invalid
pagination keeps the book resumable. All CI jobs, 22 library tests, 420 app tests
(13 existing skips), and static analysis passed.

The fixed Android app shows **Continue** and resumes **A Small Beginning**, page
1 of 2. The dev app upgrade retained byte-identical library data. Oversized debug
APKs were refused for storage; a 63,679,315-byte x86-64 profile APK with the same
app ID and signing key installed successfully. No app data was cleared. Its exact
APK hash and saved CFI are in the [edition-update evidence](evidence/arke-player-edition-update-2026-11-08.json).
This proves the reader fix, not the pending live content update or offline-after-update gate.

`qualify_live_edition_update.dart` uses copies of the retained base libraries and
the real Player `HostedCatalogue`, `hasUpdate` and `WorldInstaller.update` paths:

```text
dart --packages=<player>/.dart_tool/package_config.json tools/arke-package-verifier/test/qualify_live_edition_update.dart prepare <copied-base-scratch> <reader-commit> <empty-serial>
dart --packages=<player>/.dart_tool/package_config.json tools/arke-package-verifier/test/qualify_live_edition_update.dart updated <same-scratch> <reader-commit> <published-serial>
dart --packages=<player>/.dart_tool/package_config.json tools/arke-package-verifier/test/qualify_live_edition_update.dart withdrawn <same-scratch> <reader-commit> <withdrawn-serial>
```

Both current and historical readers completed **prepare**, retaining a deterministic
halfway checkpoint in separate libraries. The later phases remain pending. They check
live detection, old content remaining available until verification, exact position
retention, one library entry, clearing Update, idempotent repeated update, and offline
reopening. The tool does not submit, approve, publish or withdraw anything. Record
Android UI observations separately from these reader-library checks.

## Completed live newer-edition rehearsal (9 November)

Creator 1 completed fresh normal Auth0 code + PKCE sign-in using the generated
test credential. A bounded local receiver used Studio's real account adapter and
saved the session through Electron's protected storage; no token or password is
included in the evidence. The independent reviewer also completed normal sign-in.

The existing frozen candidate `91f61711-6da5-41ed-952e-9f1107351750` was submitted
without rebuilding it. Receipt `6b52ca0c-c8b8-404b-8d11-144c40f3fb7c`, revision 1
(`b8dafad9-381e-4999-8d38-49f9bd4b7a8e`), uploaded all 2,354,240 bytes and passed
the deployed Worker verifier at 08:09:18 UTC. It entered **In review**.
The reviewer downloaded the exact server copy and confirmed archive SHA-256
`c6253edce1162fa70c1ac01f5b99d9c125c5f9c266c2375aacb2d6fc2653850b`.
Its review digest is
`874b3f058a9346bd298b5293ee36703ddd85de5f8705da6a8543904c7f53540c`.

The original synthetic story and geometric artwork remain unchanged; the EPUB
publication metadata is the only book change. Before release, the current and historical
Player scratch libraries retained their original book hash and halfway reading checkpoints.
The public dev catalogue was still empty at serial 6 at 08:19 UTC, and anonymous
access to the new private package returned 401.

The owner then explicitly authorised autonomous test-account sign-in and all dev
rehearsal actions. Using the separate reviewer identity, the normal server gate
recorded exact-revision decision `32a5df4a-73ae-4e4b-9ef8-998ca3d56122`.
Approval replay returned the same decision. This is an owner-authorised automated
synthetic dev rehearsal with separate identities; it is not a manual editorial
walkthrough or a consenting-human production pilot.

Publication `20451375-edb9-4e90-bb63-8229773ba0e3` committed in one attempt at
15:57:48 UTC, advancing the catalogue from empty serial 6 to serial 7 with one
world. Replaying the command returned the same operation. Both the current
Player consumer (`30cb419e`) and historical consumer (`03a261bd`) then passed
the live HTTPS **updated** phase: detection, old bytes available before the verified
swap, exact updated book hash, unchanged saved CFI/progress/finished state,
one world and book, cleared update state, idempotent repeat and offline reopening.
The earlier current-reader preparation used `17dc9a23`; the update used the merged
`30cb419e` reader-position fix. No scratch checkpoint was reset.

The existing Android dev app detected **Update · 2.4 MB** after **You → Check for
updates**, installed edition `2026-11-08`, and returned to **Continue**. Its exact
saved place (including timestamp) and unfinished state survived installation.
The installed EPUB matched SHA-256
`a47f988bdf1602d1bf173c12036189161279529c0abfb3bd0ffef3964517dc5e`.
Continue opened **A Small Beginning**, page **1 of 2**, online and again with
airplane mode enabled. Normal reading subsequently advanced only the place's
timestamp. Networking was restored; the app data was never cleared.

Withdrawal `13588317-1ff2-4367-be19-941e1f840f84` committed in one attempt at
16:06:38 UTC and replay returned the same operation. Serial **8** is empty. Both
reader harnesses passed **withdrawn**, retaining the updated content and progress
offline. Android's forced check cached serial 8, discovery became empty, and the
installed library still held the same edition/CFI and unfinished state. Studio's
real creator service refreshed the same receipt through approved, published and
withdrawn. The temporary reviewer helper was signed out after the batch.

See the [completed 9 November evidence](evidence/arke-player-edition-update-2026-11-09.json).
This supersedes the pending-update checkpoints above. The later live part/story/comic
batch is recorded below; supported device/background-scheduler cases and the
remaining operational/pilot gates in #403 remain open. No production mutation or
mirror enablement was made.

## Live part, story and comic rehearsal (9 November)

Player [#51 / PR #68](https://github.com/michaeljosiah/ArkePlayer/pull/68) is merged.
The catalogue-pinning CLI now emits the dedicated comic pin and version while
preserving story and other format branches. All 393 world-package tests and the
three CI jobs passed. The opt-in Android reader rehearsal is merged in
[Player PR #69](https://github.com/michaeljosiah/ArkePlayer/pull/69), also with all
three CI jobs green.

The original synthetic **Paper Garden Dev 403** fixtures were built with Studio's
real package writer/readback and export records, then frozen and submitted through
its actual upload/receipt services using Creator 1. They contain gentle Mina text,
geometric artwork and quiet AAC test tones, with no third-party assets or human
voice. This qualifies the submission path, not interactive Studio authoring or the
production export compiler. Separate reviewer sign-in and exact-revision decisions
were automated under the owner's explicit synthetic-dev-test authority; they are
not a nontechnical human editorial walkthrough or production rights clearance.

The deployed verifier checked all four private archives, and independent downloads
matched their exact SHA-256 and byte lengths. The base, artwork-free audio part,
visual novel and graphic novel published in order at serials **9, 10, 11 and 12**.
Each decision/publication replay returned the same operation. The part inherited
the base cover and added chapter 2 plus the production schedule.

Current Player libraries installed the real HTTPS artifacts, preserved the audio
position, ignored future story/comic versions, and did not fetch duplicates on
repeat installation. The historical `03a261bd` reader installed base/part and
safely ignored story/comic without fetching their archives. This is execution of
the older libraries, not deployment of an older application. The normal Android
dev app discovered the part, kept chapter 1's Resume position and played chapter 2.

The real Android readers followed the visual novel's friend choice and saved
`sc_friend` / `ch_friend`; the comic saved page 2. Continue reopened both with Dart
HTTP client creation denied, and the file-backed library reload retained both
saves, the audio position and the unrelated Lantern Garden world. These checks
passed at live serial 12 and again after withdrawal at empty serial **15**.
They do not establish physical-radio, OS background scheduling or physical-device
compatibility. Pure-reader withdrawal checks also verified all 17 retained local
files and saved positions with no artifact downloads.

**Device interruption:** the first debug test APK exceeded emulator storage.
Flutter's installer automatically uninstalled the dev app before it could be
stopped. The dev library was explicitly restored from the pre-run checkpoint and
verified media (10 on-device file hashes); this is not uninterrupted native
retention across that incident. Subsequent runs backed up all private files and
preferences, used smaller profile APKs and replace-only `adb install -r`, and
stopped on installation failure. After both passing phases, the ordinary dev APK
was restored and its full library exactly matched the post-test backup. The
documented test procedure prevents the uninstall fallback from recurring.

Comic withdrawal committed at serial 13, story at 14, and part at 15. Withdrawing
the part correctly retired its base sequence as well, leaving an empty catalogue.
The older Worker nevertheless left the base receipt reporting Published. AONIK
[PR #415](https://github.com/michaeljosiah/aonik/pull/415) updates every affected
current-revision receipt and permits an exact authorised replay to reconcile older
stale receipts without a new operation or catalogue serial. The fix is merged as
`6984034312d9e7ba8be15b712621bb90730c3fe6`. Its exact source tree passed
[CI attempt 2](https://github.com/michaeljosiah/aonik/actions/runs/42686674558/attempts/2):
**5,377 .NET tests passed / 5 existing skips**, canonical-model validation and
**690 Admin UI tests**. The first CI attempt never acquired a runner and was
cancelled. Local focused tests, all 270 SQL tests and all 19 publishing endpoints
passed; the broad local API retries retained unrelated HTTP timeout failures and
are not represented as a clean full-suite pass. The initial SQL disk-exhaustion
failure was resolved by clearing disposable generated builds.

The [image build and vulnerability scan](https://github.com/michaeljosiah/aonik/actions/runs/42686715788)
passed. Image-only CLI deployment produced Worker `aonik-dev-worker--0000374` and
API `aonik-dev-api--0000384`, retaining all 46 Worker and 85 API environment
settings. The comparison was corrected to normalize null/empty secret-reference
placeholders and compare authoritative readback, rather than the CLI update
response projection. No actual setting changed. Both revisions became ready and
API health returned 200.

At **20:25:35 UTC**, an exact replay of part withdrawal
`0ea8ea16-f77f-4cae-adfd-2db1e5576397` repaired the base receipt; the repeated replay
returned the same operation. The operation set and empty catalogue serial **15**
did not change. All four real Studio creator receipts now report **Withdrawn /
complete**. The temporary reviewer helper was signed out after this batch.

The [sanitized format evidence](evidence/arke-player-live-formats-2026-11-09.json)
records hashes, receipts, decisions, operations, reader checks and limitations.
This batch supersedes earlier live part/story/comic-pending notes. #403 remains
open for supported deployed devices/OS scheduling, interactive movie/production
export walkthroughs as applicable, remaining delivery faults/mirror migration,
manual fallback and backup restoration, nontechnical review/human pilot, and
production ownership/rights/baseline/capacity/cost approval. Production and the
GitHub mirror remain unchanged.

## Android background notification rehearsal — 9 November 2026

The owner selected the emulator for this batch. Original synthetic **Notification
Garden Dev 403** base and chapter-2 part passed real Studio freeze/upload/receipt
services, deployed private verification and exact package readback, separate test
reviewer decisions and dev publication at serials **16 and 17**. This is automated
synthetic testing, not a human editorial walkthrough or production rights approval.

The ordinary Android 16 / API 36 x86_64 dev app passed permission denial/grant,
one daily task with the Wi-Fi-only constraint, native background delivery, exact
inbox ZIP verification and an unchanged library until foreground startup. Cold
and same-process warm notification taps opened **Garden chapters**, imported the
part once, consumed the inbox and did not autoplay. A repeated successful worker
kept the same notification creation/update times and inbox. Notify-off cancelled
the job. Existing Paper Garden story/comic/audio saves and Lantern Garden progress
remained unchanged. After a forced withdrawal refresh, the retained new chapter
played with both emulator Wi-Fi and mobile data disabled and no default network.
Native media controls paused it; networking was restored.

The rehearsal found that audio-service registration could warm the main Flutter
engine during a worker, starting Home and prematurely importing the part. The
first fix waited after media initialization, which exposed a stalled handshake
after worker cancellation. [Player PR #71](https://github.com/michaeljosiah/ArkePlayer/pull/71)
waits for the first resumed lifecycle **before media/UI initialization**. It is
merged as `368600aad909abd8bf8c18276858ea61d2ae9bd7`. All three
[CI jobs](https://github.com/michaeljosiah/ArkePlayer/actions/runs/42706988452)
passed; local app tests passed **434 / 13 existing skips**, with focused lifecycle
tests, static analysis and formatting also passing.

Each replacement used matching dev identity/signature, full private-data backups
and `adb install -r`; no uninstall or clear-data occurred in this batch. Only the
new synthetic world was reset to its backed-up base to repeat the same approved
part. The daily check was accelerated by changing only backed-up dev WorkSpec
`last_enqueue_time` and cache `fetchedAt`, preserving interval, task inputs and
constraints. This is not a natural 24-hour or OEM scheduling test. The long-lived,
resource-constrained emulator incurred cold-start ANRs and execution-quota/network
deferral; persisted work recovered after reboot. A temporary System UI ANR cleared
after waiting. Host storage exhaustion was resolved by removing inactive generated
test builds and retaking the affected evidence. These interruptions are retained
in the record, not counted as passing attempts.

Withdrawal operation `33668aff-1e58-415d-b531-75c316f28744` committed at serial
**18**, retiring the base/part sequence. Both real Studio receipts report
**Withdrawn / complete**, and Player fetched the empty feed while retaining all
three installed worlds. The temporary reviewer session is signed out, Notify is
off and no daily task remains. Production and the GitHub mirror are unchanged.

The [sanitized notification evidence](evidence/arke-player-notifications-2026-11-09.json)
records hashes, operations, actual failure/recovery paths and backup identities.
This supersedes the earlier OS-background-notification-pending note for this
emulator only. Physical Android/iOS, supported older installed applications,
natural/OEM scheduling, applicable interactive-movie/export UI, remaining delivery
faults/mirror migration, human walkthrough/pilot, isolated backup restoration and
production readiness remain open. Keep #403 and Player #50 open.

## Validation of this preparation

For the 7 November storage fix, the solution build passed with zero errors and
37 existing warnings. The completed non-API suites passed; the broad API run and
isolated retry hit unrelated HTTP timeouts and were stopped. The focused publishing
API suite passed all 18 tests. Do not interpret the earlier preparation totals below
as a clean full-suite result for this later commit. The new rehearsal evidence and
Docker-context exclusions do not change application code or database schema.

`dotnet build Aonik.sln --no-restore -m:1 -nr:false` passed with zero errors and
two existing query-filter analyzer warnings. `dotnet test Aonik.sln --no-build
-m:1 -nr:false` passed **5,306** tests with **12** existing skips. Bicep compilation
passed (two existing warnings outside the new storage module). The registry
image build passed Dart analysis and **22** verifier tests. These are distinct
from the live acceptance sequence above.

**Readiness:** dev admission/review/delivery are enabled, the production mirror is
disabled, and the synthetic edition has completed Studio submission, deployed Worker
verification, independent owner-approved review and dev publication. Both synthetic
creators have active tenant links and completed email/profile gates. Core Player
install, device discovery/reading, cold-launch offline access, hold, withdrawal and
forced refresh passed for this one synthetic base EPUB. The transient Worker
readback fault, same-operation retry and serial-4 withdrawal are also qualified;
dev discovery is now empty at serial 18 after the later notification rehearsal.
Configured cross-tenant, no-party,
missing/malformed authentication and genuine token-expiry boundaries are qualified.
Consenting human pilot actors, remaining delivery failure cases, the remaining live/device consumer checks, audited
production baseline rights, capacity and backup restoration are still
unqualified. #403 stays open.
