# Arke publishing through AONIK

Full target specification: [Spec 100](../specifications/100.arke-player-publishing.html).

Studio retains local authoring and the owning Player exporter. AONIK becomes the private submission, human review and approved catalogue authority. Player retains verified installation and offline use.

## Delivery issues

Parent epic: [AONIK #397](https://github.com/michaeljosiah/aonik/issues/397).

| Work | Issue |
| --- | --- |
| AONIK module, private creator receipt and declaration foundation | [#398](https://github.com/michaeljosiah/aonik/issues/398) |
| Private resumable intake and complete package verification | [#399](https://github.com/michaeljosiah/aonik/issues/399) |
| Exact-revision editorial decisions and reviewer separation | [#400](https://github.com/michaeljosiah/aonik/issues/400) |
| Immutable publication, catalogue snapshots and legacy mirror | [#401](https://github.com/michaeljosiah/aonik/issues/401) |
| Live Admin UI and matching editable artboards | [#402](https://github.com/michaeljosiah/aonik/issues/402) |
| Deployed qualification, migration and operator runbook | [#403](https://github.com/michaeljosiah/aonik/issues/403) |
| Studio creator identity, submission and receipt/status | [Studio #2391](https://github.com/michaeljosiah/ArkeStudio/issues/2391) |
| Player shared feed configuration and migration compatibility | [Player #50](https://github.com/michaeljosiah/ArkePlayer/issues/50) |
| Comic pin generation regression | [Player #51](https://github.com/michaeljosiah/ArkePlayer/issues/51) |

Existing [pilot #395](https://github.com/michaeljosiah/aonik/issues/395), [Studio #2213](https://github.com/michaeljosiah/ArkeStudio/issues/2213) and [Player #24](https://github.com/michaeljosiah/ArkePlayer/issues/24) retain their operational readiness gates and manual fallback.

## Implemented first slice

Private uploads and package verification are now implemented as a second slice: see the [intake contract, setup and qualification notes](arke-publishing-intake.md). The foundation behaviour below describes receipt creation before any bytes are received.

The editorial review backend is now the third slice: see the [review authority, decisions, snapshots and replacement contract](arke-publishing-review.md). Approval records a retained approved revision; public delivery and the live staff UI remain separate issues.

- `Aonik.Publishing`, module identity/catalogue, API/Worker composition and canonical EF mapping.
- `PublicationSubmission` and first `SubmissionRevision`, saved together. Tenant/party/client-request identity is unique permanently, including soft-deleted records.
- `POST /publishing/submissions`: a private receipt and unverified immutable declaration. Identical normalized replay returns 200; a new receipt returns 201; changed admitted material under the same client request returns 409.
- `GET /publishing/submissions/{id}` and a bounded own-receipt list. Another creator's receipt looks absent even within the same tenant.
- Server-resolved creator identity/party, consumer policy, in-process module gate and current channel admission. `PersonalUser` may create for their own party; staff needs `Publishing.Submit`. `ReadOnly` alone cannot create.
- `Publishing.Submit` is registered in the normal permission catalogue/provisioning paths without adding a default Operations grant. Existing tenants use the normal permission seed and explicit role-management workflow.
- Arke Worlds pack v2 and Publishing route requirements. Reapply the pack through the normal module-management flow for existing tenants; preserve explicit operator overrides.

New receipts are **`awaiting-upload`**, with **`packageVerified: false`**. Declared SHA/size and rights assertions are not trusted validation or editorial approval. The intake slice adds private upload/submit/preview/download routes and a Worker verification path; there is still no approval, publish or public feed route. The existing staff screens remain connection states until their integration issue lands.

## Channel configuration

No channel accepts requests by default. Configure the publisher tenant, an explicit package admission cap and the actual accepted distribution statement version before enabling receipt creation:

```json
{
  "Publishing": {
    "Channels": {
      "arke-worlds": {
        "TenantId": "<publisher-tenant-guid>",
        "Enabled": false,
        "MaxPackageBytes": 134217728,
        "RightsDeclarationVersion": "<owner-approved-statement-version>"
      }
    }
  }
}
```

The 128 MiB value is an illustrative admission cap, not a production qualification result. `Enabled` opens receipt admission; uploads additionally require explicit namespace admission, private storage and quotas described in the intake guide. It does not publish assets. Never configure a draft permission statement as if it had legal approval.

See Spec 100 §12 for the request shape. The client supplies metadata and declarations; owner user, party, tenant, status, validation and approval are server authority. No creator email, private source world or credentials belong in the declaration.

## Foundation validation

Verified locally on 3 November 2026:

- Solution build succeeds with zero errors. Existing dependency/analyzer warnings remain outside this change.
- Latest results across all solution projects: **5,192 passed, 12 existing skips**. The full solution run was followed by a corrected Application project rerun for the newly added module inventory. The completed Application suite also passes after registering the explicit staff permission.
- All **763 HTTP tests**, **255 SQL Server tests**, **62 architecture tests** and **72 SharedKernel tests** pass. The new Publishing SQL cases were additionally rerun with a two-request barrier that forces the unique-index conflict path: **4 passed**, no skips.
- After registering the explicit staff permission, the full Application suite passes again and a final Publishing/permission HTTP rerun passes **28 tests**.
- Focused navigation/profile coverage: **29 passed**, including Publishing-disabled and Workspaces-disabled boundaries.
- Migration `20261103144406_AddPublishingSubmissionFoundation` was generated using EF CLI against canonical `AonikDbContext`. It adds only the intended receipt/revision tables and constraints. The full migration stream applied successfully to a fresh disposable LocalDB database, which was then removed using the reviewed EF cleanup target.
- EF reports no pending model changes. Spec section anchors/local links and `git diff --check` pass.

Commands used include `dotnet build Aonik.sln -m:1 -nr:false`, `dotnet test Aonik.sln -m:1 -nr:false`, targeted Application/Publishing SQL reruns, and `npm test -- src/modules/registry.ownership.test.ts`. Logs and TRX results are local under the ignored `output/research/` directory.

Review/delivery issues retain their separate acceptance evidence. This foundation is not a deployed publishing service and does not make the existing staff connection-state screens live.

## Private intake validation (#399)

Verified locally on 3 November 2026:

- Final solution build succeeds with **zero errors**. An existing analyzer warning in `PayoutBeneficiaryServiceTests` remains outside this change.
- Full solution run: **5,216 passed, 12 existing skips**, including **767 HTTP** and **259 SQL Server** tests, with no failures. After final recovery/policy changes, **81 focused Publishing tests** pass: 60 Application, 11 HTTP, 8 SQL Server, 1 SharedKernel and 1 architecture test. These include five additional recovery/policy cases added after the full run.
- Pinned native Dart worker: analysis, **22 positive/adversarial fixtures** and executable compilation pass. Schemas 1–7 and whole/guide/part/music/story/comic editions are exercised with complete asset checks.
- Migration `20261103203610_AddPublishingPrivateIntake` was generated through canonical `AonikDbContext`. The complete migration stream applied to a fresh disposable LocalDB database; the reviewed scratch database was then dropped. EF reports **no pending model changes**.
- Specification anchors, local documentation links and `git diff --check` pass.

Build/test logs, TRX files and native verifier provenance are local under ignored `output/research/`. See the [intake guide](arke-publishing-intake.md) for configuration, route contracts, retention and recovery details.

Docker Desktop's Linux engine was unavailable, so live image/resource-isolation qualification is still pending in #403. Container command/policy tests and native fixture tests do not prove that deployment boundary. No container image is configured by default: unavailable verification holds the revision, and this slice cannot approve or publish it.

## Editorial review validation (#400)

Verified locally on 3 November 2026:

- Final solution build: **zero errors**, with the existing unrelated analyzer warning in `PayoutBeneficiaryServiceTests`.
- Complete solution run with `DOTNET_PROCESSOR_COUNT=4`: **5,263 passed, 12 existing skips**, including **773 HTTP**, **265 SQL Server** and **62 architecture** tests. A preceding default-concurrency run encountered HTTP timeouts across several existing suites and was interrupted; the complete run with bounded concurrency passed without failures. Repository test configuration was not changed.
- After moving the immutable-history enforcement to cover both async save overloads, the final **124 focused Publishing/permission tests** pass: 91 Application, 17 HTTP, 14 SQL Server, 1 SharedKernel and 1 architecture test. They cover the final source, including synchronous, normal async and explicit `SaveChangesAsync(bool, ...)` audit rewrite denial through canonical persistence.
- Editorial SQL cases prove duplicate/conflicting decisions, competing editors, replacement-versus-approval and insert-failure rollback. Creator/tenant/channel/permission, non-primary party separation, rights/artwork coverage, safe creator feedback and immutable revision replay are covered in service/HTTP cases.
- Updated pinned Dart worker: analysis, **22 fixtures** and executable compilation pass. Positive fixtures now assert the manifest hash/projection and production evidence required by review, in addition to complete asset checks.
- Migration `20261103213118_AddPublishingEditorialReview` was CLI-generated against canonical `AonikDbContext`, reviewed and applied with the full migration stream to fresh disposable LocalDB. It adds three editorial history/access tables, four revision fields, the active decision pointer and scoped keys/indexes. The reviewed scratch database was then removed. No hand-written migration operations or snapshot edits were used; EF reports no pending model changes.
- Specification/local documentation links and `git diff --check` pass. Logs, native build provenance and TRX results remain local under ignored `output/research/`.

Reproduce the complete run with `$env:DOTNET_PROCESSOR_COUNT='4'` followed by `dotnet test Aonik.sln --no-build -m:1 -nr:false`. See the [editorial workflow](arke-publishing-review.md) for permission/channel setup, exact HTTP contracts, evidence and revision rules. Review and named-editor assignment default closed. Qualified container deployment/consenting identity acceptance remains #403. The subsequent delivery/UI implementation is recorded below.


## Public delivery and staff workspace validation (#401/#402)

Implemented locally on 4 November 2026. The [delivery and staff workspace guide](arke-publishing-delivery.md) records configuration, contracts, retention, operator journeys and reproduction commands.

- Complete solution build: **zero errors**. Full bounded-concurrency solution regression: **5,304 passed, 12 existing skips**, including **3,530 Application**, **781 HTTP** and **269 SQL Server** cases. These add 29 publication/storage/mirror application cases, eight public-scope/permission HTTP cases and four SQL recovery/race cases to the preceding slice.
- Delivery tests prove exact archive/image identities, no catalogue promotion on failed copy/readback, unchanged intent after retry, authority/hold/policy changes before commit, same-part contention, monotonic withdrawal, attempt limits and qualified baseline ownership. SQL tests include rollback after SQL writes but before commit, independent concurrent heads, committed redelivery and an expired worker replaced during public readback. Mirror tests exercise latest-head reconciliation, unknown remote-write outcomes, stale CAS and remote serial/content conflicts.
- Final focused checks after the full run: **121 Publishing Application**, **eight HTTP** and **four SQL** tests pass. This includes two additional regressions for 181–200 character public titles and bounded immutable pin projection. Final verifier/exporter/qualification CLI analysis reports no issues.
- Native pinned reader analysis, **22 package fixtures** and executable build pass. The additional qualification CLI accepts **seven complete .NET-generated catalogues and 19 exact public pins**, covering whole/listening editions, guide, part, music, story and comic. ZIP/report fixtures are synthetic and include reader provenance; no real creator packages or public publishing calls are used.
- The CLI-generated canonical migration `20261104080759_AddPublishingPublicDelivery` was reviewed and applied with the complete stream to a new disposable LocalDB database. EF reports no pending model changes. The reviewed scratch database was removed. Generated designer/snapshot files remain tool output.
- Admin UI production build and targeted lint pass; all **690 tests across 70 frontend files** pass. The existing Vite chunk-size advisory remains.
- Real routed Admin UI browser checks use intercepted APIs and verify exact revision/digest payloads, native confirmation focus, uncertain-response identical-UUID retry, queued-versus-published status, stale conflicts, read-only controls, provenance and light/dark rendering. Fourteen matching native artboards, 28 theme exports and their full-canvas integration pass browser checks. The existing 28-scene Arke operational journeys also pass after publishing delegation. Source and generated views share CSS/icons; no production mock fallback was added.
- Production enablement, persistent public/private volume and TLS readback qualification, actual restricted container execution, consented identities, live optional GitHub mirror and Studio/Player cutover remain **#403 and the linked repository integration issues**. This work neither publishes a real edition nor changes production credentials/configuration.
