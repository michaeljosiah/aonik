# Governed decision workflows

Use this runbook to enable, verify and roll back the Spec 099 workflow features for one tenant. The live pilot uses an approved automation identity, the exact workflow versions and a consenting Gmail account.

## Enable a pilot tenant

1. Deploy the same API and Worker version. Apply migrations through the canonical `AonikDbContext` stream. Use the shared SQL-backed Data Protection key ring and the same private blob storage on API and Worker. Confirm at least one Worker replica is running before exercising automatic admission, expiration and recovery.
2. Leave `Workflows.ExecutionEnabled` and `Workflows.AutomaticTriggersEnabled` disabled until the tenant is ready. A deployment-level `Workflows:ExecutionEnabled=false` or `Workflows:AutomaticTriggersEnabled=false` overrides tenant enablement. User preferences cannot enable either setting.
3. In the workflow editor, save an invoice workflow, validate it, and simulate normal, boundary and missing-data fixtures. Pin action versions, connection grants and a real tenant reviewer role. Publish through the exact governance proposal.
4. In Tenant Integrations, create an automation identity. Select the immutable workflow version, explicit invoice scope and required action versions. Select the optional mailbox synchronization capability only when the trigger needs it. Capability discovery does not issue a grant.
5. For Gmail, the account owner completes consent and confirms the displayed account and scopes. Issue separate exact AutomationIdentity and Workflow account grants. Organizational ownership or administrator access alone does not grant mailbox use.
6. Enable workflow execution and, separately, approved automatic sources. Open **Validate, simulate and launch → Manage automatic triggers** in the editor. Save and validate a typed trigger draft, inspect its exact high-risk activation preview, and approve as an authorized human publisher/integration administrator. Separation of publisher duties continues to apply when configured.
7. Check the run page from a fresh session. It must show the pinned version, automation actor, source binding/revision/event, action approval and committed domain receipt. Review the pinned amount, currency and **Draft → Issued** change before approving invoice issue. An automation identity has no initiating human login and cannot decide an approval.

Draft trigger authoring through Aonik tools requires medium confirmation. Activation, disablement and changes to an active source require a durable high-risk proposal. The HTTP editor is an authenticated human administration surface with explicit change review.

## Rehearse the Aonik tools

Select **AONIK Orchestrator** in Chat or **AONIK** in AI Playground. Use a synthetic invoice workflow draft to rehearse creation, a threshold change and deletion. Inspect each exact change before confirming the draft operation. Playground uses your signed-in workspace permissions and the same server approval endpoint as Chat.

Ask Aonik to request the exact Gmail actions and ownership needed for the rehearsal. Confirm the request in the conversation, then open its review link. My Connections handles personal requests and Tenant Integrations handles organizational accounts and grants. Mailbox access begins after the applicable reviews and owner consent.

## Operator interface

The organization picker labels the environment and remembers the last organization selected by the signed-in user on this browser. Selection remains explicit; the tenant API does not identify a default organization. My Connections uses the shared application shell while retaining its existing personal-access route and server permissions. Admin screens remain subject to their manifest and policies.

Gmail access requests initially select the available **Search email**, **Read email** and **Read attachments** actions. Expand **Additional email permissions** only when needed; exact action keys and versions remain under technical references. Availability is checked before submission. A policy restriction requires an integration administrator to review connection settings and refresh. An unprovisioned connector can still receive an access request for review. **Troubleshooting** contains the existing mutation status and recovery controls.

In a narrow editor, use **Add nodes** and **Properties** to open the existing panels as sheets; Escape or Close dismisses them. Use a node's **Add** button to place it on the visible canvas and open its properties. **Test** opens validation, simulation and publication controls. Fixture errors identify the node, one-based fixture number and field; exact paths remain under technical validation details. Provider credit, rate and connection errors use one actionable notice, with a safe diagnostic code.

These presentation changes do not provision a mailbox or prove a live workflow run. Synthetic responsive checks cover the editor and connection forms at 694 × 925, the editor at 375 × 812, and the desktop editor at 1600 × 1000. Live consenting-account qualification remains tracked separately below.

## Source behavior

Invoice creation and issue stage typed events in Finance's own save. Events contain invoice references and trusted causation, rather than model-provided facts. Admission reloads current facts and preserves the source event, binding revision and exact activation receipt. Repeated delivery returns the original run.

Schedules use existing recurring WorkItems and the Quartz recurrence calculator. Supply an explicit IANA timezone, UTC start/end window of at most 366 days, maximum occurrences, and an approved catch-up bound of 0–10. A zero catch-up bound skips an occurrence first observed over two minutes late. Catch-up retains the first observation time across deferral so retries cannot grow the batch. Source occurrence identities use UTC instants across DST changes. Validation rejects invalid timezone fallback and sub-minute cron patterns.

Admission limits are 1–1000 starts per rolling hour, 1–10 concurrent runs and causation depth 1–8. One binding/subject can have only one active run. Capacity waits retain the same durable source occurrence, with a future retry deadline; they do not consume failure attempts. Repeated bindings in a causation path are stopped. Self-generated events are ignored by default.

Gmail triggers can use a fixed invoice reference or admit supported attachments as private financial documents. The intake workflow extracts a grounded candidate, then waits for explicit confirmation before creating a sales invoice. Keep the creation, issue, payment intent and mail approvals independently reviewed.

## Gmail deployment configuration

Configure the existing OAuth client on the server under `Integrations:Gmail:OAuth`: `ClientId`, `ClientSecret`, `RedirectUri` and `ReviewReturnUri`. Register the exact callback `/integrations/gmail/oauth/callback` with Google. Keep secrets out of browser configuration, workflow JSON and chat. Read-only connections use the Gmail read scope plus verified account identity scopes. Attachments additionally require the existing private content store and malware scanner. Confirm OCR and scanner readiness on the Worker before enabling attachment admission. The API and Worker images include Tesseract with English language data and Poppler. Set `DOCUMENTS__OCR__ENABLED=true` and configure ClamAV for both hosts. If the environment has a private scanner, set `CONTACTENQUIRIES__CLAMAV__ENABLED`, `CONTACTENQUIRIES__CLAMAV__HOST` and `CONTACTENQUIRIES__CLAMAV__PORT`. Dev can instead set the GitHub environment variable `CLAMAV_IMAGE` to a reviewed image reference such as `docker.io/clamav/clamav@sha256:336558e74278168c8e87061eeb1116fae64723f2d1644fda7382311912405b4e` (ClamAV 1.4, Linux amd64). The ACA stack then adds an opt-in scanner to each API and Worker replica, selected via loopback and sharing the replica lifecycle. This increases each replica's allocation to 2 vCPU and 4 GiB. The scanner exposes no application ingress and requires no separate dev service. Verify a benign attachment passes scanning before enabling automatic intake. Remove `CLAMAV_IMAGE` and redeploy to remove the sidecars. [ACA container resources](https://learn.microsoft.com/en-us/azure/container-apps/containers) · [ClamAV image guidance](https://docs.clamav.net/manual/Installing/Docker.html)

For a new development client, use the [exact Gmail OAuth setup and rehearsal checklist](decision-workflows-google-oauth.md). Setup and owner consent remain distinct from financial action approval.

Polling works without Pub/Sub. For authenticated push with polling recovery, configure these deployment-owned settings on API and Worker:

```json
{
  "Integrations": {
    "Gmail": {
      "Mailbox": {
        "PushTopic": "projects/YOUR_PROJECT/topics/YOUR_TOPIC",
        "PushSubscription": "projects/YOUR_PROJECT/subscriptions/YOUR_SUBSCRIPTION",
        "PushAudience": "https://YOUR_API/integrations/gmail/notifications",
        "PushServiceAccount": "YOUR_PUSH_ACCOUNT@YOUR_PROJECT.iam.gserviceaccount.com"
      }
    }
  }
}
```

Use a topic in the Google application project, grant Gmail's publisher service access, and configure the Pub/Sub push subscription with the exact HTTPS endpoint, audience and service account. Follow Google's [Gmail push setup](https://developers.google.com/workspace/gmail/api/guides/push) and [authenticated push configuration](https://docs.cloud.google.com/pubsub/docs/authenticate-push-subscriptions). This code does not create cloud resources or grant external permissions.

Ingress verifies Google's signature, audience, expiry, issuer, verified service-account email and exact configured Pub/Sub subscription. It resolves the application/account watch claim to a local subscription, then commits a tenant-scoped outbox hint before acknowledging. Caller tenant headers and an email address alone cannot select authority. Hints contain no mail body; the Worker fetches actual changes through the authorized provider.

One connection has one local subscription and can serve several independently approved bindings. A global unique watch claim prevents another tenant/connection from overwriting the same provider account/application watch. A conflict requires explicitly approved polling fallback or isolated provider configuration. It never shares credentials or data between tenants.

## Recovery and operator checks

- **Watch health:** My Connections/Tenant Integrations → account → **Check mailbox synchronization** shows mode, last successful page, watch expiry, polling schedule expiry and safe error code. Watches renew daily; transient renewal failure retains polling recovery.
- **History cursor expired:** Gmail's [history/sync contract](https://developers.google.com/workspace/gmail/api/guides/sync) requires full synchronization after a cursor 404. Recovery uses bounded resumable pages and suppresses historical automation unless the exact binding approved backfill. A backfill request is limited to the preceding seven days.
- **Page interrupted:** Cursor/page progress and normalized discovered events commit together in Platform. A failed uncommitted page is retried; durable source/run identity prevents repeated effects. Synchronization has a lease, a 60-second pass limit, at most two pages per pass, bounded provider responses and a 200-page full-recovery cap.
- **NeedsAttention:** Permanent provider errors or recovery bounds hold the subscription. Review the account/configuration and explicitly reactivate the exact binding after correction. Do not reset cursors or rewrite receipts with SQL. Reactivation can authorize the next bounded recovery batch.
- **Access revoked:** Current tenant status, actor, owner, account, action, workflow grant, scopes and tenant policy are checked before deferred work can use the account. Lost access suspends synchronization. On a paused intake run, recovery targets the original Gmail account while source access is lost. When the source is available, recovery targets the blocked step’s exact account and action. Explicit resume checks the current source evidence and the pinned grant for the pending connector step. Duplicate wakes leave held runs at their recorded checkpoint. A run deadline still expires while the run is held. Reconnect or grant changes do not constitute a financial action approval.
- **Worker restart:** Keep the same database and key ring. The existing outbox and WorkItem dispatcher reclaim leases and resume the recorded source/occurrence. Do not issue a new admission key merely to retry delivery.
- **Polling schedule ending:** Maintenance WorkItems have a one-year bound, visible in mailbox health. Review/reactivate the binding before the series ends. An approved reactivation can create a fresh maintenance series while retaining cursor/deduplication evidence. Cancelling an old task never resurrects it.
- **Application/account change:** Disable the old binding memberships and release/expire its provider watch first. Obtain the correct owner consent, review the new configuration, and activate through a new high-risk proposal. Existing credentials never fall back to another account.

## Stop and roll back

Disable automatic tenant admission first. Request disabling the exact trigger revision. This immediately blocks authority for that revision and all earlier runs, including when a later revision is activated before cancellation delivery. Worker processing cancels pending approvals and remaining work while preserving reconciliation of already dispatched effects. Revoking the actor/account grant independently blocks the next effect.

Use the existing workflow retirement/rollback proposal for a bad published graph. Preserve immutable versions, source IDs, approvals, operation receipts and checkpoints. Resolve uncertain financial/provider outcomes from recorded evidence before deliberately reprocessing. Rebind triggers explicitly after a rollback or upgrade. Deactivating the tenant stops the next workflow action and connector call even when existing grants and execution settings still allow them.

## Review a voice approval

When the voice client cannot show an approval or its exact changes, open the link in the voice transcript. Sign in to Admin and select the tenant used in the voice session. The link opens the specific pending request and checks current approval authority. For a Medium conversational approval, ask the agent to continue after approving so the server can consume the approval against the original arguments.

In Azure Container Apps, the API defaults the review link to the deployed Admin UI origin. Set `Voice__ApprovalReviewBaseUrl` to the Admin HTTPS origin when using a custom hostname. The Admin voice tester opens the review in its current workspace.

## Pilot evidence

| Exercise | Record |
| --- | --- |
| Deploy | The API and Worker revisions, tag, canonical migration result, private storage selection, running replica, and OCR/scanner health. |
| Connect | The consent request and confirmed account reference, actions/scopes, tenant policy, and exact workflow and actor grants. |
| Intake | One synthetic attachment, admitted document/run IDs, published definition hashes, extraction candidate ID and grounded evidence. Replay the same event and confirm the original run and document are reused. |
| Route and confirm | The rule's matched inputs and selected route. Record the distinct actor and human approval IDs for creation and issue. If the high-risk payment intent is in scope, verify its Pending Order-linked receipt without executing payment. |
| Notify | Use a separate exact approval for the selected test-recipient Gmail effect. Record the journal outcome, then qualify access revocation and uncertain-outcome reconciliation with disposable fixtures. |
| Recover | Restart the Worker while the run is waiting. Verify the same step, operation, actor, definition and pending approval survive. |
| Roll back | Disable admission, revoke the test grant, switch through the reviewed rollback proposal and check preexisting runs retain their recorded version and receipts. |
| Retention | Check the hourly Connector Content Retention job. Temporary connector references expire after seven days. The Worker deletes matching uploads older than seven days and ten minutes, up to 500 per pass, from the dedicated private store. Private financial documents follow their own erasure policy. |

Collect IDs, hashes, safe error codes and outcome timestamps. Visit the same operator review surface from browser and the review link from voice. For the 100-item capacity run, record child count and distinct checkpoints before and after the Worker restart, total wall time and maximum observed continuation time. The local in-memory scenario completed 100 children in 31 seconds. Use SQL and actual Worker timing for pilot evidence.

## Child workflows, foreach and parallel branches

Publish the child first, then select its exact version/hash in the parent. Child input is an invoice reference; its output is the recorded run/outcome receipt. Publication validates transitive dependencies. Editing or publishing a child never changes a parent's pin. Retiring a dependency prevents new effects until an explicitly reviewed parent revision adopts a valid replacement.

For unattended work, grant the actor each child version, action and invoice scope explicitly. A parent grant does not imply descendant access. Named human financial approval remains separate for every invocation; revoking the actor's action grant also blocks a pending approval.

Foreach accepts a declared typed invoice collection with unique stable item keys, maximum iterations and timeout. Wire completion, exhaustion and error separately. Parallel accepts two to four named children, explicit concurrency and All/AnySuccess. Write-capable or human-task branches must run serially with All. AnySuccess permits read/pure children, selects a deterministic recorded winner and cancels the remaining reads. A missing or uncertain outcome never wins.

Limits are 100 iterations, four simultaneous branches, depth five, a conservative 1,000 node invocations across the execution tree, and seven days elapsed lifetime. Publication rejects a graph whose worst case exceeds these bounds. Every child uses the existing runner and its own scoped database context. The run page links parent/child evidence. Duplicate delivery reuses the same child and operation identities. Cancellation withdraws pending approvals and propagates to descendants; dispatched effects retain reconciliation evidence. Keep the original database/key ring when restarting API or Worker.

## Advanced decisions and libraries

New schema 2 tables support FirstMatch, Unique, Collect and Score. Enable advanced rules explicitly in the table editor, select one invoice currency when amounts or scores are used, and review Normal/Boundary/MissingData fixtures. Conditions are bounded to depth four and 100 nodes. Optional missing facts remain Unknown under Not; use isMissing for intentional absence. Unique conflicts and unknown inputs hold for manual review. Score uses exact decimal strings, bounded weights and adjacent complete bands. A score never changes an action's approval tier.

Collect returns typed invoice references. Select **Typed Collect decision output** in a foreach and enter the source decision's stable key. Connect that decision directly to the foreach on its collection route. The collection persists before the first child. Every child still uses its own version/resource/action grants and human approval. Collect without this explicit consumer cannot publish.

Choose **Reusable decision library** when creating a draft. Use one pure table and declare its input facts on the trigger; output routes follow the table. Publish through the existing governance approval. In a consumer Decision, choose **Use a published decision library**, then select the definition and exact version. Review the hash, inputs and output contract before publishing the consumer. A newer library never updates consumers automatically. Retirement blocks future effects and pending financial approval for consumers of that library; historical traces remain. Libraries cannot start financial runs or automatic sources themselves.

## Gmail mutations and uncertain delivery

Platform and tenant `Integrations.Gmail.AllowedActions` must both include an action before it can be granted. Request only the needed actions through the existing connection request and incremental Google consent flow. Existing read-only accounts do not gain writes. Draft actions use compose/modify consent; sends additionally need read access for receipt recovery. AONIK grants keep use narrower than the OAuth capability. Permanent deletion requires `Integrations.Gmail.AllowPermanentDelete=true` at both global and tenant scope, full-mail consent and its separate high-risk approval.

Use `gmail_prepare_message` to retain a reviewed immutable plain-text message. Its result contains a protected reference and hash. Subsequent draft/send tools require that reference/hash and a stable `idempotencyKey`. Draft update/delete/send also require the latest draft hash from `gmail_get_draft`. Review the exact sender, To/Cc/Bcc, subject/body, reply/thread reference and attachment hashes in the server approval card. High sends/deletion run only through their approved proposal; medium draft/reversible changes run after confirmation. A named workflow reviewer needs current mailbox action access as well as the workflow role. Room approvals load private review evidence for the current authorized user.

The supported contract is plain text up to 16,384 characters, 50 recipients or message targets, five scanned attachments totaling 5 MiB and MIME at most 8 MiB. HTML/ambiguous MIME or unsafe attachments need manual handling. Message references use the existing private store's retention window; expired references require fresh preparation/review.

After a timeout, interruption or partial batch, keep the original operation key and approval request ID. In **My Connections/Tenant Integrations → Check a Gmail operation**, load its recorded status. **Reconcile with Gmail** performs only reads and records matching evidence. It never sends or repeats the provider mutation. Missing/ambiguous evidence remains NeedsAttention. A generated Message-ID alone is insufficient: send recovery verifies one SENT message with the exact reviewed content and recipients. A partial batch is not replayed wholesale.

For a held workflow, inspect/reconcile its operation, then explicitly revalidate and resume the same run. The runner can complete from a verified receipt while retaining the failed High proposal's original terminal history. It does not reapprove or redispatch that proposal. If current grants, source authority or content evidence are unavailable, investigate the provider manually before proposing any replacement operation. Keep the prior outcome reference in that review.

Workflow schema v2 adds durable waits and typed human tasks. Choose one wait duration or an explicit UTC deadline; a run retains its original deadline across restarts and has a seven-day lifetime. An operator can revalidate a due timer but cannot shorten it. For a human task, select a current tenant user or role, purpose, bounded choices and timeout, then wire every response/rejection/timeout route. The recorded expiry is a WorkItem; named-user tasks carry that assignment without turning the worker into that user. Review and respond from the run page. Responses are hash-bound audit evidence, and subsequent financial actions still require their own approval. Cancelling the run invalidates its pending response and timer. Schema v1 publications and checkpoints retain their original interpretation.

## Local document extraction

`Documents:Ocr:Enabled` defaults to `false`. Enable it only on a host with Tesseract, its configured language data and Poppler installed. Set absolute `TesseractPath`, `PdfToTextPath` and `PdfToPpmPath` under `Documents:Ocr`; Linux defaults are `/usr/bin/tesseract`, `/usr/bin/pdftotext` and `/usr/bin/pdftoppm`. `Language` defaults to `eng`; `TimeoutSeconds` is bounded to 1–60 seconds. The existing `ContactEnquiries:ClamAv` scanner must also be enabled and reachable. An unavailable scanner or engine holds extraction rather than supplying financial evidence.

The adapter accepts PNG, JPEG and PDF, checks signatures and malware, and bounds source bytes, image dimensions, pages, output and process lifetime. Native PDF text uses Poppler; scanned documents use Tesseract TSV. Results include the actual extractor/version, page count and available OCR confidence. Confidence never grants approval. Temporary files are removed when extraction finishes. Financial candidates and confirmed creation use the existing workflow path below.

CI installs the native engines and sets `AONIK_OCR_TESTS_REQUIRED=true`, so its scanned/native synthetic-document cases cannot silently skip. Developer hosts without those binaries report explicit skips. Configuration follows the [Tesseract command-line contract](https://tesseract-ocr.github.io/tessdoc/Command-Line-Usage.html) and [Poppler rasterization options](https://manpages.debian.org/trixie/poppler-utils/pdftoppm.1.en.html).

## Financial document review

For automatic intake, publish a schema-2 Document workflow first. Select **Gmail message added → Intake financial attachments** on its automatic trigger. Pin the account, workflow grant and automation identity; approve activation separately. The actor needs an explicit **all documents** grant for that exact version, and both the actor and workflow need search, message and attachment read actions. The grant editor lists those optional source capabilities without adding dummy graph steps or granting them automatically.

Intake reads current labels/date and subject metadata before attachments. An optional exact subject prefix can restrict a rehearsal to synthetic mail. Self-generated messages remain ignored by default; enabling them requires explicit trigger review. At most five PDF, PNG, JPEG or plain-text attachments totalling 10 MiB are accepted per message. Bodies are excluded from the metadata fetch. Source files are scanned, retained privately and bound to the original provider account, workflow/version/grant/actor and binding revision. No customer or personal owner is inferred from email. The owner is the existing tenant party; missing ownership holds intake.

Replayed delivery reuses the original Document/File receipt; source commit and run admission use existing outboxes and uniqueness boundaries. Erased sources retain a deduplication tombstone and cannot be recreated by replay. A different binding cannot silently adopt another source's authority. Concurrent losing uploads are deleted only after the competing receipt is verified; uncertain commits retain their private blob for investigation. Grant loss/account changes during a download discard its result. Subsequent extraction, human review and invoice continuation independently revalidate retained source access.

For payment initiation, explicitly map the sales invoice to its existing Order during the Medium creation review. Add `finance.payments.create_invoice_intent@1` after the existing issue action in its pinned Invoice child, binding only `invoiceId` to `invoice.id` and choosing a named reviewer role. The automation identity needs that exact child/action grant. The reviewer needs current `Payment.Create`, `Orders.Read`, invoice and retained source access; approving creation or issue does not approve the payment proposal.

The High preview derives the amount, currency and payer/payee from the stored issued invoice, active customer/parties and matching fundable Order. It pins semantic/rowversion hashes and source AiRun references. A changed Order, invoice or proposal payload cannot create an intent. The dispatcher uses the existing PaymentService creation path to create a **Pending** intent, with an idempotency key reserved for that invoice's workflow funding and its complete operation/approval/provenance receipt. The invoice remains Issued. Funds authorization, capture and ledger posting require their own established actions and evidence.

After a process loss, preserve the original run, proposal and operation. The runner can verify the committed domain receipt without creating another intent or reapproving a terminal failed High proposal. An approved proposal whose dispatcher completion acknowledgement is missing can likewise have its domain receipt recorded on the workflow; its original proposal history remains visible. No receipt means no automatic redispatch. Expiry stops an unapproved proposal through its existing scheduled wake; expiry after an approved but unconfirmed dispatch holds the outcome for investigation. Review an existing or cancelled funding attempt before deliberately proposing replacement funding through the appropriate domain path.

Create a `FinancialSource` through the existing Documents API and upload one immutable source file. Its classification is `Restricted`; its file uses `BlobStorage:PrivateDocuments`, defaulting to the checked `documents-private` path/container. A public base URL or overlap with a public store is refused. Financial uploads are bounded to 10 MiB and scanned before private storage. Source content is read through the authorized financial-content endpoint, never a public file URL. The normal document deletion path scrubs extraction payloads and source metadata while retaining hash/AiRun references.

An intake workflow uses schema 2 with `subjectType: "Document"` on its Trigger. Existing definitions continue to mean Invoice. Capabilities advertise `document.id`, `document.fileId` and `document.ownerPartyId`. Configure `documents.financial.extract@1` with the first two facts. It requires `Document.Extract` and `Document.Read`, scans the source, verifies its physical hash and calls the routed `financial_document_extraction_v1` AI task. Provision that versioned task and an AI route; there is no model fallback in this action. The immutable candidate retains source IDs/hash, extractor provenance, exact fields/evidence, prompt/schema hash, model and actual AiRun.

Configure `finance.invoices.create_from_document@1` with extractionId/candidateHash from that step and an explicit known customer ID. Select a current reviewer role. The reviewer needs `Invoice.Create`, `Customers.Read` and `Document.Read`, plus current source mailbox grants when applicable. The server displays the exact proposed invoice and source evidence, checks customer identity, current currency precision and totals, and requires the existing Medium confirmation. Confidence cannot bypass it. Source grounding checks explicit total/due labels, numeric line evidence and verbatim excerpts; unsupported layouts remain for manual review. Supplier bills, personal bills without an explicit owner path, unsupported tax/discount, missing dates, conflicting totals or low confidence stay candidates for manual review.

Pass the returned invoiceId to an existing pinned Invoice child workflow for decision/issue routing. Each child has its own actor/resource grant and financial approvals. A userless identity needs explicit grants for both the Document intake version and Invoice child version. Invoice continuations also revalidate the original document/source grant. Hash or access changes invalidate review/execution. Repeated source bytes and repeated customer/invoice-number/currency evidence cannot create another invoice; changed content is retained for review. A worker loss after invoice commit reconciles the original operation receipt without another creation or confirmation.

The automated attachment, candidate, confirmed creation and Order-linked intent paths require their recorded CI evidence before tenant enablement. The complete provider/operator journey and source-access recovery remain T13/T15 qualification work.

## Qualification evidence

Spec 099 sections 13.1–13.19 and GitHub epic #438 carry commit/CI evidence. SQL migration and concurrency qualification runs in CI against an isolated SQL Server container because LocalDB cannot initialize on this development machine. A live consenting Gmail/Pub/Sub rehearsal and the full automatic document/payment/mail journey remain distinct release gates; passing local fixtures does not establish those external prerequisites.

Use **New workflow → Financial document intake** for a stored-source draft. Configure the known customer and human reviewer role; add an exact published invoice child for issue/payment routing when needed. The template otherwise ends in manual review. The run links to the existing document page, where authorized reviewers can inspect fields/issues/provenance and download the private source through AONIK. Supplier evidence is not mapped to a sales invoice. Deleting a source immediately invalidates stale creation/issue approval; the document erasure outbox then cancels remaining workflow work and scrubs copied approval previews. Existing invoices, immutable operation hashes and AiRun references remain financial/audit evidence.

## Retained financial-source access

A document or its invoice child can pause with `WaitingForConnection` when the original mailbox grant, consent or policy becomes unavailable. The run page remains available to a domain-authorized reader, with private evidence hidden if that reader also lacks source access. Use **Request access for the pinned account**: the server derives the original workflow/account/grant and three read actions from source evidence and approved parent lineage, including when the current step is a native financial action.

An integration/workflow administrator reviews the request; the account owner completes consent through AONIK. Fulfilment does not restart the workflow. Use **Revalidate and resume this run** after access is restored. Changed source bytes, subject facts, provider account, disabled triggers or revoked actor/domain authority require review; reconnecting cannot replace the source or widen the action set.
