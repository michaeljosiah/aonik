# Arke publishing dev rehearsal — #403

This record separates deployment from end-to-end qualification. No production
catalogue migration or public release is authorised by the dev rehearsal.

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
this does not prove interruption after an accepted chunk or a process restart.

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
forced refresh passed for this one synthetic base EPUB. Consenting human pilot actors, the
failure-case and supported-consumer matrix, audited production baseline rights,
capacity and backup restoration are still unqualified. #403 stays open.
