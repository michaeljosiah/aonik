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
| New Studio-to-API upload part | Configured intake size | 8 MiB |
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
5. Provision the dev intake share at 32 GiB (`publishingIntakeShareQuotaGiB`), then
   preview and apply `scripts/Set-ArkeDevPublishingCapacity.ps1`. The script refuses
   the large profile without that storage headroom. This changes only
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
12 GiB per tenant with a 32 GiB private intake share and at most 32 retained
packages per creator; it is not an increase to
production quotas. Existing upload sessions retain their originally issued part
size when new sessions move to 8 MiB parts. Session memory
includes the sealed input as well as the child and scratch.

Cost qualification must include warm-pool/node costs, not just child CPU. Each
large session is bounded at 2 vCPU / 4 GiB and 1,200 seconds; the pool caps concurrent
sessions at two and keeps one warm. Actual provider billing is based on the custom
session pool's resources; measured child CPU alone is not an Azure invoice.
See [Azure Container Apps billing](https://learn.microsoft.com/en-us/azure/container-apps/billing).

Custom sessions use dedicated **E16 nodes (16 vCPU / 128 GiB)**. On 11 November,
Azure reported `nodeCount=1` for each of the large and retained standard dev pools.
The UK South GBP retail meters retrieved that day were GBP 0.0608 per vCPU-hour,
GBP 0.005 per GiB-hour and GBP 0.0752 per dedicated-management hour. One E16 node
therefore estimates **GBP 1.6128/hour compute**, plus applicable management:
`16 * 0.0608 + 128 * 0.005`. With one management charge the estimate is
**GBP 1.688/hour, or GBP 1,232.24 for 730 hours**. Both dev pools retain nodes, so
their combined compute alone is GBP 3.2256/hour. These are public retail estimates,
not the subscription's discounted invoice; taxes, storage transactions/capacity,
registry, logs, API/Worker and applicable bandwidth charges are additional.

For a continuously warm node, allocate that pool's hourly infrastructure cost
across its completed jobs; do not call the 34-second reader execution the entire
cost of a job. At the measured 269.42-second boundary session, a serial, fully
occupied pool would complete about 13.36 jobs/hour: about GBP 0.126 per job with
the illustrative management charge, before the additional costs above. At one
job/hour that allocation becomes GBP 1.688/job. This is a utilization model,
not a production throughput guarantee or invoice. Monitor `nodeCount`, retries,
queue latency and actual Azure Cost Management charges. Keep maximum concurrency
at two until requalified; retire the unused rollback pool deliberately after its
retention window rather than assuming idle custom sessions are free.

Sources: [dedicated-session billing](https://learn.microsoft.com/en-us/azure/container-apps/billing#custom-container),
[pricing units](https://azure.microsoft.com/en-gb/pricing/details/container-apps/)
and the public [Azure Retail Prices API](https://prices.azure.com/api/retail/prices).
The selected rates and pool observations are frozen in the capacity evidence.

## Dev qualification, 11 November 2026

The immutable verifier is
`aonikdevacr.azurecr.io/aonik-arke-verifier-sessions@sha256:23069f32cd6c5cf6c07a983c74f8ac0698409fe97ace4e9f2096b3620938e13b`.
The reader remains the pinned commit above. Both fixtures are original synthetic
material; the baseline fixture reproduces the audited byte/count/asset envelope,
not the audited third-party content.

| Concurrent live sandbox run | Baseline envelope | Exact large boundary |
|---|---:|---:|
| Archive bytes | 1,107,515,522 | 1,610,612,736 |
| Assets | 1,144 | 2,048 |
| Largest asset bytes | 48,587,809 | 67,108,864 |
| Upload seconds | 194.082 | 234.700 |
| Verification seconds | 23.424 | 34.119 |
| Child CPU seconds | 25.663 | 38.423 |
| Child peak RSS KiB | 50,996 | 54,240 |
| Sampled scratch peak bytes | 48,590,848 | 67,108,864 |
| Report bytes | 368,133 | 658,309 |
| Total session seconds | 219.420 | 269.420 |

Both passed with exact input identity and acknowledged cleanup. Child RSS excludes
the parent's sealed archive, so it is not total container memory. A separate live
stop/readback check confirmed that the stopped session no longer existed. The
two runs overlapped with different nonces, archives, asset counts and sessions.

Live refusals passed for 2,049 assets, a 64 MiB + 1 byte asset, an archive declaration
one byte over 1.5 GiB, a caller-added profile, another job's nonce, incorrect and
replayed offsets, changed archive identity and restarting the same job. A one-second
verification timeout failed closed. Explicit cancellation after 16 MiB was cleaned
up, and an abandoned upload was observed reclaimed after 160 seconds. Both the
unchanged standard pool and the large pool passed the network, identity, readonly
runtime, scratch and child resource probes.

The observed concurrent runs consumed at most 438.84 / 538.84 allocated vCPU-seconds
and 877.68 / 1,077.68 allocated GiB-seconds respectively while their sessions were
held. These are resource reservations, not billed CPU usage or currency estimates.
The hard Azure lifetime bounds one occupied large session at 2,400 vCPU-seconds
and 4,800 GiB-seconds. Warm capacity and provider/node overhead remain additional;
production pricing must use the deployment's Azure agreement and workload, not
these child CPU measurements.

Evidence: [capacity measurements](evidence/arke-capacity-2026-11-11.json).
Private Studio upload/resume receipts and reviewer evidence are recorded in
[issue #428](https://github.com/michaeljosiah/aonik/issues/428). The dev runtime
images came from [qualified build 42932506212](https://github.com/michaeljosiah/aonik/actions/runs/42932506212).
Local validation passed 5,394 .NET tests (12 environment/integration skips), 36
Studio publishing tests, Studio lint/typecheck/build, 25 pinned-reader Dart tests
and 12 Linux sandbox tests. An additional test-only layer on the exact immutable
image passed 14 sandbox tests, including real 64 MiB tmpfs exhaustion (`ENOSPC`),
`SIGKILL`, removal of owned scratch and successful subsequent reader jobs. Run it
with `az acr build --registry <registry> --file Dockerfile --build-arg
VERIFIER_IMAGE=<qualified-digest> tools/arke-package-verifier/qualification`.
This layer is never deployed and cannot expose fault programs to HTTP callers.
The generated migration applied cleanly to a dedicated
LocalDB database. Production enablement remains separate.

### Private Studio-to-review acceptance

The real Studio account, frozen-preview, publishing and resumable-upload services
uploaded the fixtures into the dev API. A minimal test world/export sidecar points
at each original synthetic ZIP; this is a service-level capacity test, not another
full authoring-UI qualification. The baseline (1,144 assets), boundary (2,048)
and small compatibility fixture (1 MiB / eight assets) all reached **in-review**.
The reviewer UI rendered both large inventories through their last asset, private
artwork and exact archive/review identities. The 2,048-asset server declaration
retained exactly two rights groups of 1,024 IDs with complete coverage.

The boundary deliberately lost the response after the server stored part four.
Its next resume began at part five on the same upload, with no duplicate receipt.
Both concurrent local harnesses later paused during host resource pressure; the
baseline resumed at part 58 and the boundary at part 343 and both completed.
No checkpoint failure was reproduced on sequential resume. Preserve that
distinction: this qualifies recovery, not sustained concurrent desktop throughput.
The existing boundary session kept its issued 1 MiB parts (1,536 total), while the
new baseline used 8 MiB parts (133 total). Boundary assembly completed in 41.571 s;
its final resume, including remaining parts and assembly, took 885.351 s.

A 1.5 GiB + 1 byte API declaration returned `413 package-too-large` before intake.
A valid 128 MiB / 2,048-asset fixture with long portable paths exceeded the report
bound and was held instead of exposing an oversized review snapshot. Generate it
with `--bytes 134217728 --assets 2048 --largest-asset 131072 --path-padding 180`.
Each path component remains at most 90 characters. Studio separately rejected an
earlier fixture with a single overlong component; no path protection was relaxed.
The report test also confirmed retained-package admission: the existing creator's
20-package allowance returned `429 upload-quota-exceeded`. The explicit dev large
profile now permits 32 retained packages, still within the same 6 GiB creator /
12 GiB tenant byte bounds. The same saved receipt resumed successfully after rollout.

No capacity receipt was approved or published. The public dev catalogue remained
empty at serial **26**, and the legacy mirror remained disabled. Temporary
operator grants for direct sandbox qualification were removed. API/Worker retain
their normal scoped service access; no database firewall exception was added.
