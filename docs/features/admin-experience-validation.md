# Admin experience validation — 3 November 2026

This records verification for the starter-kit styling, shared icons and CGI portraits, Remittance pack and operations, collaborative rooms, and message/mention notifications.

- `dotnet build Aonik.sln --no-restore`: succeeded with zero errors; existing dependency/analyzer warnings remain (154 in the final run).
- Admin UI production build: passed. Full UI suite: 688 tests across 70 files passed.
- Isolated SQL room/notification checks: 28 tests passed, including concurrent reply commits, interrupted commit recovery, notification audience and idempotency.
- Authenticated room/inbox APIs: eight tests passed before the final boundary-validation additions. The final bounded API rerun passed ten tests covering room endpoints, malformed requests, request-validator architecture coverage, and two unrelated timeout controls.
- Six editable chat artboards: verified with 24 light/dark captures. Runtime two-client browser checks cover shared authors, portraits, membership, approval confirmation, notification reconciliation, inbox filters/actions, older-message links and distinct theme tokens.
- Both canonical CLI-generated chat migrations applied to a fresh isolated LocalDB verification database; no pending model changes were reported. The application database was not changed.

## Solution-wide test attempt

`dotnet test Aonik.sln --no-restore --no-build` passed 4,384 tests across the nine completed non-API test projects; 12 tests were skipped (two existing demo-seed cases and ten opt-in Finance integration cases). The API run was interrupted after a cascade of HTTP request timeouts, so the complete solution suite is **not** recorded as passing.

That attempt also found missing boundary validators for five new room request types. All five now have FastEndpoints validators; invalid requests return the application's configured 422 response, while services retain their authorization and validation checks. The architecture rule and malformed-request API regression pass.

Timeouts were observed in CommitmentLifecycle, PaymentLog, CareEntity, WorkspaceSync, WorkspaceOperation and Safety endpoint tests during the broad run. The CommitmentLifecycle and PaymentLog isolation cases both pass when rerun with `DOTNET_PROCESSOR_COUNT=2`. This suggests concurrency/resource pressure, but does not establish the cause of every timed-out case. No unrelated feature behavior was changed to hide these results.

The final targeted rerun used:

```powershell
$env:DOTNET_PROCESSOR_COUNT='2'
dotnet test tests/Aonik.Api.Tests --filter "FullyQualifiedName~RequestDtoValidatorCoverageTests|FullyQualifiedName~ChatRoomEndpointsTests|FullyQualifiedName~CommitmentLifecycleEndpointsTests.Isolation_OtherUserCannotMarkDone_Returns404|FullyQualifiedName~PaymentLogEndpointsTests.Isolation_OtherUserCannotReadLog_Returns404" --no-restore
```

Generated previews use fictional data and intercepted APIs. Verification did not send real email, invitations, notifications, model requests or financial operations. Reviewed marketing exports are included with the source; scratch output and logs remain local.

## Final publication checks — Support, Food Business and Arke Worlds

The later work adds four Support, 41 Food Business and 28 Arke Worlds editable artboards, connected customer records and a new installed Arke Worlds business pack/profile. The Arke-specific main-application destinations deliberately show connection states; they do not load fictional production data or activate draft commercial offers.

- Full Admin UI suite: **690 tests across 70 files passed**. Strict design guardrails, token contrast and the production build also passed.
- Full solution build: **zero errors**, with 154 existing dependency/analyzer warnings.
- Nine completed non-API test projects: **4,386 passed**, with 12 existing skips. This includes 251 relational database tests and 3,412 application-service tests. API completion is tracked separately in the PR checks; these counts must not be described as a passing full solution suite.
- Native browser checks: Support 4 artboards / 20 theme captures; Food Business 41 / 106; Arke Worlds 28 / 64. Connected review flows, identity scoping, interrupted-delivery recovery, commercial guards, exact-package publication approval, role previews and retained artboard targets are covered. A transient Windows PNG write lock during the combined capture pass required an isolated Arke rerun.
- The real Admin UI Arke browser check passed against intercepted manifest/API fixtures, including navigation, explicit connection states and absence of writes. Its two actual-UI screenshots are separate from the native design exports.
- Generated profile/catalogue snapshots, all 110 canonical icons and gallery links were checked. No existing tenant, plan price, customer record or application database was changed.

The earlier interrupted broad API run and bounded successful API checks above remain part of the validation history. CI on the PR is authoritative for the complete merge candidate.
