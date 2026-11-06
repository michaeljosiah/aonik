# Arke publishing dev rehearsal — #403

This record separates deployment from end-to-end qualification. No production
catalogue migration or public release is authorised by the dev rehearsal.

## Deployed application

Recovery release `484c87782b397b636dda2bb98216041fca85ac58` from PR #406 passed
[CI](https://github.com/michaeljosiah/aonik/actions/runs/42179322706),
[image build](https://github.com/michaeljosiah/aonik/actions/runs/42179351344),
[preview](https://github.com/michaeljosiah/aonik/actions/runs/42180760111) and
[deployment](https://github.com/michaeljosiah/aonik/actions/runs/42181160485).
API `0000377`, Worker `0000368` and Admin UI `0000368` are Running; health is
200 and the disabled catalogue is 404. The actual reviewer browser now reaches
the two-organization picker and selects Arke Kidz without a false session expiry.
The live body-only registration check returns 403 before identity creation;
Auth0 confirms no second identity was created. The first identity is verified
but still needs an AONIK invitation/link.

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

A separate local Electron profile/workspace was opened for the sign-in check.
Computer-use access to that window timed out. No live creator sign-in, refresh,
encrypted credential persistence or API-authorised receipt read has been proven.
The installed Studio workspace was not modified.

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

The application adapter is pushed in `2848f965`. The normal
[dev image workflow](https://github.com/michaeljosiah/aonik/actions/runs/42166936001)
is waiting for the protected `dev` environment review. API/Worker still run
`d05793b5` and the old closed profile until that build and normal runtime rollout
complete. Do not work around the review with a direct application image swap.
After rollout, prove the deployed system-assigned identity path before opening
intake. The current API is healthy (200) and the dev catalogue remains 404.

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

## Validation of this preparation

`dotnet build Aonik.sln --no-restore -m:1 -nr:false` passed with zero errors and
two existing query-filter analyzer warnings. `dotnet test Aonik.sln --no-build
-m:1 -nr:false` passed **5,306** tests with **12** existing skips. Bicep compilation
passed (two existing warnings outside the new storage module). The registry
image build passed Dart analysis and **22** verifier tests. These are distinct
from the live acceptance sequence above.

**Readiness:** updated application/private storage deployed and channel closed;
isolated remote verification qualified with synthetic fixtures. Deployed caller identity, creator sign-in,
tenant/independent actors and the complete Studio → approval → Player rehearsal
are still unproven. #403 stays open.
