# Arke publishing production proposal

Status: **draft, not approved for deployment or distribution**. Prepared on
11 November 2026 for epic #397. The dev rehearsal and this proposal do not activate
production. Michael Josiah is the designated owner of all five existing namespaces
and the reviewer/support contact; production contact and identity mappings remain
to be supplied.

## Proposed deployment

Use a separate `rg-aonik-prod-uksouth` resource group and `arke-worlds` channel,
subject to confirmation of the target subscription or an existing production
environment. Keep the dev tenant, test accounts, storage, pools and public feed
separate. The current accessible inventory has no qualified production runtime.

The existing ACA stack supplies API, Worker, Admin UI, Qdrant, SQL S0, Key Vault,
ACR, logs, SQL/Key Vault private endpoints and resource locks. Publishing adds
private 32 GiB intake and 10 GiB delivery shares plus one isolated `large-v1`
verifier pool. Its workload limits remain 1.5 GiB archives, 2,048 assets and two
concurrent sessions. This is a bounded initial deployment, not a throughput or
availability guarantee. The current production stack's replica range is:

| Service | Minimum / maximum replicas | Resources per replica |
|---|---:|---|
| API | 2 / 10 | 0.5 vCPU / 1 GiB |
| Worker | 2 / 5 | 0.5 vCPU / 1 GiB |
| Admin UI | 2 / 5 | 0.5 vCPU / 1 GiB |
| Qdrant | 1 / 2 | 1 vCPU / 2 GiB |
| Verifier | Maximum 2 sessions, 1 warm | 2 vCPU / 4 GiB per session; observed provider allocation is one E16 node |

Requalify effective concurrency before increasing active verification beyond two.
SQL S0 and the retained single-node dependencies do not imply zone-redundant
availability merely because the application has two replicas.

The verifier module now derives registry, API/Worker, environment and pull identity
names from `workloadName` / `environmentName`. The production parameter file pins
the qualified verifier digest but does not create its registry or import the image.
The default dev what-if resolves the same existing resource IDs. Compile checks
cover standalone modules and environment parameter files, not only the main stack.

## Deployment sequence to approve

1. Confirm subscription, permanent HTTPS API/Admin origins, production contact,
   budget owner and change window. Confirm the Auth0 plan and production API/native
   client configuration; do not reuse Mailinator identities as production staff.
2. Prepare the protected GitHub `prod` environment, scoped Azure deployment identity
   and Key Vault values. Review the complete ARM what-if against the chosen target.
   All channel intake/review/delivery/public/mirror flags start disabled.
3. Import the qualified API, Worker and verifier images by digest into the production
   registry and verify their identities. Rebuild Admin UI with the actual production
   API/OIDC configuration; the dev Admin image embeds dev configuration.
4. Deploy runtime, canonical migrations, durable stores and isolated verifier.
   Verify the active revisions, no verifier egress/metadata access, private storage
   isolation and the production creator/reviewer permissions using an approved
   synthetic production qualification fixture before admitting existing content.
5. Create Michael Josiah's production Party and resolve the five namespace mappings.
   Retain the approved private distribution-evidence reference. The previously audited
   catalogue is serial 53, SHA-256
   `3fdc4558650817334e99f68d3364c9a3a00a2519930eaed016a30dff4d1229b4`;
   re-read and re-audit the latest version before import. Preserve every original
   public pin, ID and byte. Do not re-upload the existing 31 pins unnecessarily.
6. Back up and prove restoration of matching SQL/private/public data. Agree retention,
   response coverage and the staffed private intake route. A contact's name alone
   does not prove mailbox readiness or a staffed response target.
7. Obtain the exact baseline/cutover decision, stop all legacy catalogue writers,
   initialise the production serial floor once, and compare anonymous canonical
   readback. Activate the pinned Studio release workflow only after those checks.
   Follow [the handoff and recovery procedure](arke-publishing-handoff.md).
8. Verify supported installed Player behavior at the unchanged release URL and the
   new canonical origin, then qualify one explicitly approved new production edition.
   Confirm progress/offline behavior and withdrawal without claiming remote recall.

Independent exact-revision review remains server-enforced. Michael can own and
operate the namespaces; a submission made by Michael still needs an independent
reviewer. The owner accepted automated separate test identities for dev, not an
invented independent production decision.

## Cost model awaiting owner acceptance

The [machine-readable estimate](evidence/arke-production-cost-proposal-2026-11-11.json)
uses Azure's 11 November UK South USD retail meters and a 730-hour month. USD is
deliberate: the GBP API response rounded several per-second rates to zero. Those
zeros were not treated as free compute. Microsoft identifies non-USD retail
responses as reference estimates. [Retail price API documentation](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices).

| Component / assumption | Modelled USD per month |
|---|---:|
| One E16 verifier node, including one management charge | 1,637.69 |
| API/Worker/Admin/Qdrant active at minimum replica counts | 441.50 |
| SQL S0 and Basic registry | 23.47 |
| Files: 192 GB stored across proposed publishing and existing stack quotas | 14.40 |
| Two private endpoints, 200 GB processed, two DNS zones and 1M DNS queries | 18.00 |
| Logs: 10 GB ingestion | 28.80 |
| Public outbound: 100 GB | 8.70 |
| 100,000 Key Vault operations and 1M ACA requests | 0.70 |
| **Model subtotal at minimum replicas** | **2,173.26** |
| **Model subtotal with all configured maximum replicas active for the month** | **3,056.27** |

The model deducts no subscription-level free grants and assumes active compute;
eligible idle replicas may cost less. The verifier is charged for its allocated
node, not just package-verification seconds. [Container Apps billing](https://learn.microsoft.com/en-us/azure/container-apps/billing).

Allow an additional **USD 100 placeholder** for unmeasured blob/file transactions,
registry overage, backup storage and alerting; it is not a price quote or spending
cap. Confirm Auth0, ACS email, domain and staffed-mailbox entitlements separately.
Taxes, agreement discounts, extra nodes, traffic above these assumptions and changes
to replica/storage limits are excluded. A budget alert does not stop spending.

No paid resource or plan is created by this proposal. The final activation request
must include the owner's target environment, completed private identity/rights
mapping, refreshed estimate, concrete what-if and chosen alert thresholds.
