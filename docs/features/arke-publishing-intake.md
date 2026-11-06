# Private Arke publishing intake — issue #399

Target contract: [Spec 100](../specifications/100.arke-player-publishing.html). Delivery: [#399](https://github.com/michaeljosiah/aonik/issues/399), under [epic #397](https://github.com/michaeljosiah/aonik/issues/397).

This slice receives a creator-selected frozen Player ZIP privately, verifies its complete bytes and queues independent format verification. Successful checks move the submission to **in review**. They do not approve or publish it. Editorial authority and public catalogue delivery remain #400 and #401; the staff UI/artboards are #402.

## Implemented journey

1. Create the immutable declaration/receipt using the foundation API.
2. `POST /publishing/submissions/{id}/uploads`, without a body, creates or resumes the current upload. The response identifies its session, part size, total parts, received byte count, expiry and missing part numbers.
3. `PUT /publishing/submissions/{id}/uploads/{uploadId}/parts/{part}` sends raw `application/octet-stream`, an exact `Content-Length` and `X-Part-Sha256`. Numbers start at one. Packages up to the configured single-shot threshold use one binary PUT; larger packages use fixed-size resumable parts.
4. `POST .../uploads/{uploadId}/complete`, without a body, streams the parts into a private ZIP and checks the declared whole-file SHA-256 and length. The state becomes **uploaded**, with `packageVerified: false`.
5. `POST /publishing/submissions/{id}/submit` with `{ "revisionId": "...", "operationId": "..." }` records a pending validation report, freezes the submitted bytes and stages the validation event in the **same SQL save/transaction**. Keep the operation UUID for retry. Duplicate requests cannot enqueue repeated checks.
6. A Worker outbox handler claims the report, invokes the pinned isolated verifier and records its result. Matching bytes, capabilities, namespace and policy give **in-review**; failure, unavailable verification or unknown part lineage gives **held**. No client field can grant verification or approval.

`GET .../uploads/{uploadId}` provides resumable status. `DELETE .../uploads/{uploadId}` cancels an unsubmitted upload. Replaying a cancellation for an old session cannot detach or cancel its replacement. Submitted packages remain retained for validation/review.

`GET .../preview` returns private, typed validation/preview metadata: exact archive hash/length, reader commit, verifier image, status/reason and asset inventory. This endpoint does not execute or render uploaded HTML/EPUB/scripts. `GET .../package` downloads the creator's exact retained ZIP as an attachment with private/no-store, nosniff and sandbox headers. The future reviewer UI uses its own explicit publisher authority; creator scope is not a reviewer shortcut.

Creator reads/writes resolve tenant, user and party server-side. Another creator in the same tenant and every foreign tenant see 404. Private object hashes and storage paths are not bearer capabilities. Nothing is mounted as public assets or exposed through `IFileStore.GetUrl()`.

## Storage, reservations and recovery

- Dedicated private session-owned storage, configured outside static-file and generic blob mounts. A durable store marker and location fingerprint prevent another local volume or a moved root from silently impersonating the owner. API and Worker instances must share the same qualified private volume and configured absolute path.
- Tenant and creator retained-byte/package reservations are admitted under serializable SQL transactions. Namespace and daily session admission are bounded too. Part identity is committed before bytes are accepted; changed retries cannot create unlimited unreserved objects.
- Parts and completed ZIPs are immutable. Full archive assembly is streaming, not an in-memory concatenation. Duplicate identical uploads do not double-count received bytes.
- Assembly and validation use durable claims. A replaced/expired claim cannot finalise over the current owner. Uploaded bytes survive response loss and validation redelivery.
- Cancelled, expired and failed uploads remain charged until their actual private bytes are deleted. Deletion succeeds before the reservation is released. A deletion/response failure is retried through a durable cleanup disposition, without freeing capacity for bytes still on disk.
- The Worker maintenance service retires expired unsubmitted sessions and cleans retired sessions after the configured grace. It does not remove uploaded or submitted editions. Later rejection/withdrawal retention needs the explicit review/publication workflow; there is no automatic deletion of held evidence.
- Logical quota is not a disk capacity measurement: allow space for parts, assembly, bounded concurrent temporary writes and retained objects. Budget at least three times the retained-byte cap plus temporary-write headroom, and monitor the real volume. The verifier's scratch/RAM limits are separate.

## Configuration and rollout boundary

All defaults fail closed. The following is an illustrative **private test channel** configuration, not an approved production deployment or permission statement:

```json
{
  "Publishing": {
    "Channels": {
      "arke-worlds-test": {
        "TenantId": "<publisher-tenant-guid>",
        "Enabled": false,
        "NamespaceAdmissionEnabled": false,
        "MaxPackageBytes": 134217728,
        "RightsDeclarationVersion": "<owner-approved-version>"
      }
    },
    "Intake": {
      "PrivateRoot": "<absolute-private-shared-volume-path>",
      "MaxCreatorRetainedBytes": 536870912,
      "MaxTenantRetainedBytes": 2147483648,
      "MaxCreatorPackages": 20,
      "MaxTenantPackages": 200,
      "MaxCreatorNamespaces": 100,
      "MaxSessionsPerCreatorPerDay": 100,
      "SingleShotThresholdBytes": 33554432,
      "PartSizeBytes": 8388608,
      "SessionHours": 48,
      "AssemblyMinutes": 10,
      "CleanupGraceHours": 24,
      "MaxValidationAttempts": 3
    },
    "Verifier": {
      "Provider": "Docker",
      "DockerExecutable": "docker",
      "Image": "<qualified-registry-image>@sha256:<64-hex-digest>",
      "ReaderCommit": "27df122566bcdef18126932c15a74112e18b8573",
      "TimeoutSeconds": 45,
      "MemoryMiB": 512,
      "ScratchMiB": 512,
      "MaxAssets": 1024,
      "MaxAssetBytes": 268435456,
      "MaxDocumentBytes": 4194304
    }
  }
}
```

Enable namespace admission only after qualifying that channel's identity ownership. Import/reserve existing public world IDs and approved base/part heads through the audited catalogue work in #401/#403 before enabling the real public channel. New private claims never establish an approved public edition. A structurally valid part remains held when its exact approved base and next part number are not established.

No global intake quotas or public channel are seeded. Reusing workspace storage allowances does not grant publication authority. Configure identical verifier policy in API and Worker; changed policy invalidates a pending result rather than silently certifying it under different limits. Changes to bytes/declarations currently need a new receipt; revision/editing and change-request UX belong to #400/#402.

## Pinned verifier build

The repository-owned wrapper imports the owning pure-Dart Player contracts at the exact commit in `tools/arke-package-verifier/reader-pin.json`. It does not import an uncommitted Player checkout or copy private source worlds. The builder uses `git archive` into an ignored output directory and defaults to cached/offline dependency resolution.

```powershell
pwsh -NoProfile -File tools/arke-package-verifier/build.ps1 `
  -PlayerRepository C:\src\ArkePlayer `
  -OutputDirectory output/arke-verifier
```

Use a compatible installed Dart SDK (the pinned packages require Dart 3.11.1 or compatible). `-AllowNetwork` is an explicit build-time dependency restore option. The prepared workspace keeps a resolved lockfile. Analysis and the verifier's positive/adversarial fixtures must pass before the native executable and provenance receipt are emitted.

The native executable is for local fixture qualification. **The server has no native-process verification fallback.** Its production adapter requires a qualified container image by immutable digest. Build that image from the prepared workspace using `tools/arke-package-verifier/Dockerfile`, with an operator-qualified Debian-compatible Dart SDK image supplied as `DART_IMAGE` **by digest**. The Dockerfile reruns analysis/tests and includes a hard 90-second supervisor inside the container, so a dead API/Worker cannot leave an unbounded parser process behind.

At runtime the Docker adapter enforces no network, read-only root/input, a non-root user, no capabilities or privilege escalation, CPU/PID/memory limits, bounded tmpfs scratch, bounded output and a host timeout. It rechecks report provenance, archive identity and edition fingerprint. Containers are uniquely named and labelled `aonik.publishing.verifier=true`; host cleanup force-removes the generated name. Qualify resource termination and host/container cleanup in the actual deployment before enabling the channel.

### Azure Dynamic Sessions provider

ACA hosts cannot launch the Docker adapter. `Provider: AzureDynamicSessions` uses a dedicated custom-container pool, selected by its ARM `SessionPoolResourceId`. `Docker` remains the default; unknown providers fail closed. There is no native parser fallback or reuse of the AI code-interpreter pool.

The dev qualification profile uses 32 MiB archives, 16 MiB assets, 4 MiB documents, 64 MiB scratch, 1 CPU and 2 GiB pool memory. Configure the channel and Studio archive limits to the same 32 MiB ceiling. Set `MemoryMiB: 2048`, `ScratchMiB: 64`, `MaxAssetBytes: 16777216` and the qualified session image digest. API and Worker settings must match. The builder emits `Dockerfile.sessions` and its transport alongside the pinned reader workspace; both `DART_IMAGE` and `PYTHON_IMAGE` build arguments must be qualified digests.

Each verification gets a new cryptographically random session and nonce. The adapter validates the ARM pool configuration before and after execution: immutable image, egress disabled, one container, no command override, secrets, environment injection or mounts, image-pull identity unavailable inside the session, bounded lifecycle and capacity. The caller's system-assigned managed identity needs only Session Executor and Reader on this pool. Its named HTTP client disables redirects, cookies and request logging. No storage credentials, paths, SAS URLs or creator bearer tokens are sent to the sandbox.

The single-use transport streams bounded chunks into an anonymous Linux memfd, verifies length/hash and seals the input against writing, resizing and removing seals. A separate non-root reader inherits only that descriptor and a minimal environment. Its kernel limits bound address space, CPU time, file size, open descriptors and processes; `no_new_privs` is set before exec. The runtime directories are not writable by UID 65532; scratch is the bounded `/dev/shm` filesystem. This is not a claim that ACA exposes Docker's read-only-root setting. Parent transport memory is non-dumpable, and child stdout/stderr are bounded.

The parser wall deadline is at most 45 seconds. A fixed 150-second job watchdog exits even if the caller disappears; the Azure session has a non-renewable 300-second maximum lifetime. Every adapter exit attempts stop-session with an independent cleanup deadline. Unconfirmed cleanup invalidates a result. There is no automatic transport replay into a possibly reused sandbox. Existing held-submission/revision recovery remains authoritative.

Use `scripts/Test-ArkeDevVerifier.ps1 -CheckAbandonedJob` for fixed runtime/network/allocation/expiry probes and `tools/ArkeVerifier.Qualification` for all seven synthetic package types through the real adapter. These tools target the dedicated dev pool only. They do not create submissions, approve rights, publish artefacts or qualify human/device acceptance. Record actual results in the [dev runbook](../runbooks/arke-publishing-dev.md).

The worker checks every media asset through the owning `extractAsset`/`checkAsset` primitives, not just `readWorldPackage`. It also bounds central-directory/document sizes, refuses unsafe/duplicate/unreferenced/compressed outer entries, checks supported document capabilities, inspects media containers and constrains nested EPUB expansion. Art/media rights, age suitability and editorial approval are separate checks.

## Verification status

Native pinned-worker fixtures cover schemas 1–7, whole editions, illustrated listening, guides, parts, music, stories and comics, plus byte tampering, traversal, duplicates, missing/unreferenced assets, compressed entries, document/media/count limits, EPUB expansion and unknown capabilities.

Local qualification: **22 native worker fixtures**, a solution run with **5,216 passed / 12 existing skips**, and **81 final focused Publishing tests** after recovery/policy changes. The final solution build has zero errors; the generated migration applied to fresh LocalDB and matches the canonical model.

Backend tests cover creator/tenant isolation, interrupted and identical/changed part retry, measured length/hash failure, quotas/expiry, retained cleanup accounting and deletion-failure recovery, expired assembly fencing, changed-policy holds, atomic submission/outbox, unavailable verifier holds and SQL admission/part/submission races. Exact results are recorded in the parent [delivery record](arke-publishing.md).

The local Docker Desktop CLI is installed, but its Linux engine was unavailable during this implementation. Native reader/fixture qualification and container command/policy tests are distinct from live container execution. Deployment resource-isolation qualification remains in #403; unavailable verification always holds submissions.
