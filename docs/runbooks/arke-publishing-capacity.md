# Arke publishing capacity profiles (#428)

`large-v1` is an explicit Dynamic Sessions profile. It covers the audited largest
catalogue ZIP (1,107,515,522 bytes), largest asset (48,587,809 bytes) and inventory
(1,144 assets), with approximately 45%, 38% and 79% headroom respectively. It is
not a change to production channel enablement or editorial rights.

| Limit | Existing session profile | `large-v1` |
|---|---:|---:|
| ZIP bytes | 32 MiB | 1,536 MiB |
| Assets | 1,024 | 2,048 |
| One asset | 16 MiB | 64 MiB |
| One JSON document | 4 MiB | 4 MiB |
| Worker-to-sandbox chunk | 1 MiB | 4 MiB |
| Container | 1 vCPU / 2 GiB | 2 vCPU / 4 GiB |
| Child address space / processes | 2 GiB / 32 | 2 GiB / 32 |
| Scratch / maximum child file | 64 MiB / 16 MiB | 64 MiB / 64 MiB |
| Child CPU / verification wall time | 45 s | 180 s |
| Sandbox hard lifetime from begin | 150 s | 840 s |
| Upload inactivity deadline | Hard lifetime | 150 s |
| Worker transport deadline | 180 s | 900 s |
| Validation including private readback | 240 s | 1,080 s |
| Validation lease | 300 s | 1,200 s |
| Outbox batch / minimum lease | Existing settings | 1 / 1,260 s |
| Azure session lifetime | 300 s | 1,200 s |
| Verification report / review snapshot | 256 / 512 KiB | 1,024 / 2,048 KiB |

The archive remains a sealed anonymous memory file. The child has no credentials,
network egress, writable runtime, caller-selected executable or arbitrary path. A
single owned scratch directory is removed on success, refusal, timeout or forced
child termination. `stopSession` runs even after caller cancellation; an uncertain
cleanup cannot produce a passing report. The sandbox has an independent hard
deadline, and an abandoned large upload expires after 150 seconds of inactivity.
Polling never extends verification or job deadlines.

The reader's package contract and `checkAsset` still validate every asset. The
verifier now reuses the bounded ZIP directory instead of reparsing it per asset.
The real reader commit remains pinned at
`27df122566bcdef18126932c15a74112e18b8573`. Evidence that exceeds its UTF-8 byte bound
becomes a small refusal; it cannot retain an oversized presentation as a fallback.

Rights evidence remains capped at 100 KiB, with at most 1,024 IDs per group and
2,048 total. Studio groups a large inventory deterministically and retains exact
coverage. Long identifiers can still exceed the metadata limit and must be
shortened before exporting; capacity is not an unlimited metadata allowance.

## Deployment and rollback

1. Build the verifier using `tools/arke-package-verifier/build.ps1` and the pinned
   Dart/Python images in `reader-pin.json`. Build `Dockerfile.sessions` with
   `CAPACITY_PROFILE=large-v1`. The profile is fixed inside the immutable image;
   HTTP requests and environment variables cannot increase it. Legacy Docker
   verification retains its existing profile and refuses `large-v1`.
2. Deploy `iac/azure/modules/publishing-verifier.bicep` with the immutable image,
   `poolName=aonikdevpublishinglarge` and `capacityProfile=large-v1`. The pool keeps
   two-session maximum concurrency and one warm session. API/Worker receive only
   Session Executor and Reader; the registry pull identity never enters a session.
3. Run `scripts/Test-ArkeDevVerifier.ps1 -CapacityProfile large-v1 -ExpectedImage
   <digest-reference>`, including `-CheckAbandonedJob`. Wait for the active image
   and provisioning state to match; a desired template alone is insufficient.
4. Deploy the matching API/Worker code and generated `AonikDbContext` migration.
   The migration updates model metadata only: both evidence columns already use
   `nvarchar(max)`. Its empty operations and generated snapshot are intentional.
5. Preview and apply `scripts/Set-ArkeDevPublishingCapacity.ps1`. This changes only
   capacity settings in the existing dev apps. It preserves tenant, baseline,
   channel enablement, reviewer, delivery, mirror and unrelated secret settings.
   Use `iac/azure/environments/dev/arke-studio.large.json` for the matching Studio
   rehearsal. The existing Studio/dev deployment defaults remain available.
6. Run the large, boundary, negative, interruption, concurrency and cleanup
   rehearsals. Record exact image/source/package identities and sanitized results.
   Verification admits a package to review; it never approves or releases it.

`-Profile standard -Apply` restores the existing capacity settings and removes the
optional profile key. Restore Studio's standard profile too. Changing verifier
policy invalidates previous exact-policy review evidence; do not re-use an old
approval or suppress that check. Pending jobs whose captured policy changed are
held before expensive readback/verification. The legacy policy JSON is unchanged
when the optional capacity profile is absent.

The larger profile claims one outbox item at a time so messages do not wait behind
a long verification with already-expiring leases. Existing dispatch remains
sequential per Worker; pool capacity is not a promise of parallel outbox dispatch.
Allow up to 21 minutes for crash recovery of a claimed message. Intake retains its
bounded validation attempts and immutable-part replay rules.

## Reproducible capacity evidence

`generate_capacity_fixture.py` writes original synthetic comics as stored ZIPs at
an exact byte size. Images contain original geometric pixels and legal ancillary
padding. This establishes byte-handling and inventory capacity, not complex image
decoding quality or permission to distribute real catalogue content.

```powershell
python tools/arke-package-verifier/generate_capacity_fixture.py output/boundary.zip --bytes 1610612736 --assets 2048 --largest-asset 67108864
pwsh scripts/Test-ArkeDevCapacity.ps1 -ArchivePath output/boundary.zip -ExpectedImage <digest-reference> -EvidencePath output/boundary-result.json
```

The test records transport/verification time, child CPU, child peak RSS, sampled
scratch high-water, input bytes, report size and acknowledged session cleanup.
`-StopAfterChunks` exercises cancellation; `-TimeoutSeconds` and `-ExpectedRefusal`
exercise bounded failure. Sampling is a lower bound on transient scratch usage;
the tmpfs and file limits establish the hard ceiling independently. Dynamic
Sessions hides cgroup counters in this environment, so those are reported as
unavailable, never as measured zero.

Private intake reserves logical archive bytes. During assembly, parts and the
assembled archive coexist, so provision at least twice retained-byte quotas plus
temporary write headroom. The dev large profile reserves 6 GiB per creator and
12 GiB per tenant; it is not an increase to production quotas. Session memory
includes the sealed input as well as the child and scratch.

Cost qualification must include warm-pool/node costs, not just child CPU. Each
large session is bounded at 2 vCPU / 4 GiB and 1,200 seconds; the pool caps concurrent
sessions at two and keeps one warm. Actual provider billing is based on the custom
session pool's resources; measured child CPU alone is not an Azure invoice.
See [Azure Container Apps billing](https://learn.microsoft.com/en-us/azure/container-apps/billing).

Live qualification evidence and final measured results are recorded alongside this
runbook when the rehearsal completes. Production enablement remains separate.
