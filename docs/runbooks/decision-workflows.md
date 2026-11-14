# Governed decision workflows

This runbook covers the invoice workflow foundation, tenant automation identities and automatic sources in Spec 099. The expanded epic also requires document intake, richer control flow, Gmail mutations and the connector marketplace. Their qualification remains separate from this milestone.

## Enable a pilot tenant

1. Deploy the same API and Worker version. Apply migrations through the canonical `AonikDbContext` stream. Keep API and Worker Data Protection keys persistent and shared; connection credentials and source task proofs depend on those keys.
2. Leave `Workflows.ExecutionEnabled` and `Workflows.AutomaticTriggersEnabled` disabled until the tenant is ready. A deployment-level `Workflows:ExecutionEnabled=false` or `Workflows:AutomaticTriggersEnabled=false` overrides tenant enablement. User preferences cannot enable either setting.
3. In the workflow editor, save an invoice workflow, validate it, and simulate normal, boundary and missing-data fixtures. Pin action versions, connection grants and a real tenant reviewer role. Publish through the exact governance proposal.
4. In Tenant Integrations, create an automation identity. Select the immutable workflow version, explicit invoice scope and required action versions. Select the optional mailbox synchronization capability only when the trigger needs it. Capability discovery does not issue a grant.
5. For Gmail, the account owner completes consent and confirms the displayed account and scopes. Issue separate exact AutomationIdentity and Workflow account grants. Organizational ownership or administrator access alone does not grant mailbox use.
6. Enable workflow execution and, separately, approved automatic sources. Open **Validate, simulate and launch → Manage automatic triggers** in the editor. Save and validate a typed trigger draft, inspect its exact high-risk activation preview, and approve as an authorized human publisher/integration administrator. Separation of publisher duties continues to apply when configured.
7. Check the run page from a fresh session. It must show the pinned version, automation actor, source binding/revision/event, action approval and committed domain receipt. An automation identity has no initiating human login and cannot decide an approval.

Draft trigger authoring through Aonik tools requires medium confirmation. Activation, disablement and changes to an active source require a durable high-risk proposal. The HTTP editor is an authenticated human administration surface with explicit change review.

## Source behavior

Invoice creation and issue stage typed events in Finance's own save. Events contain invoice references and trusted causation, rather than model-provided facts. Admission reloads current facts and preserves the source event, binding revision and exact activation receipt. Repeated delivery returns the original run.

Schedules use existing recurring WorkItems and the Quartz recurrence calculator. Supply an explicit IANA timezone, UTC start/end window of at most 366 days, maximum occurrences, and an approved catch-up bound of 0–10. A zero catch-up bound skips an occurrence first observed over two minutes late. Catch-up retains the first observation time across deferral so retries cannot grow the batch. Source occurrence identities use UTC instants across DST changes. Validation rejects invalid timezone fallback and sub-minute cron patterns.

Admission limits are 1–1000 starts per rolling hour, 1–10 concurrent runs and causation depth 1–8. One binding/subject can have only one active run. Capacity waits retain the same durable source occurrence, with a future retry deadline; they do not consume failure attempts. Repeated bindings in a causation path are stopped. Self-generated events are ignored by default.

The current mailbox route selects one explicitly stored invoice. Automated document-to-domain intake is the T13 extension; an email never supplies financial authority or silently becomes a sales invoice.

## Gmail deployment configuration

Configure the existing OAuth client on the server under `Integrations:Gmail:OAuth`: `ClientId`, `ClientSecret`, `RedirectUri` and `ReviewReturnUri`. Register the exact callback `/integrations/gmail/oauth/callback` with Google. Keep secrets out of browser configuration, workflow JSON and chat. Read-only connections use the Gmail read scope plus verified account identity scopes. Attachments additionally require the existing private content store and malware scanner.

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
- **Access revoked:** Current actor, owner, account, action, workflow grant, scopes and tenant policy are checked again before provider I/O and before committing a page. Lost access suspends synchronization; queued financial effects independently recheck their own authority. Reconnect or grant changes do not constitute a financial action approval.
- **Worker restart:** Keep the same database and key ring. The existing outbox and WorkItem dispatcher reclaim leases and resume the recorded source/occurrence. Do not issue a new admission key merely to retry delivery.
- **Polling schedule ending:** Maintenance WorkItems have a one-year bound, visible in mailbox health. Review/reactivate the binding before the series ends. An approved reactivation can create a fresh maintenance series while retaining cursor/deduplication evidence. Cancelling an old task never resurrects it.
- **Application/account change:** Disable the old binding memberships and release/expire its provider watch first. Obtain the correct owner consent, review the new configuration, and activate through a new high-risk proposal. Existing credentials never fall back to another account.

## Stop and roll back

Disable automatic tenant admission first. Request disabling the exact trigger revision. This immediately blocks authority for that revision and all earlier runs, including when a later revision is activated before cancellation delivery. Worker processing cancels pending approvals and remaining work while preserving reconciliation of already dispatched effects. Revoking the actor/account grant independently blocks the next effect.

Use the existing workflow retirement/rollback proposal for a bad published graph. Preserve immutable versions, source IDs, approvals, operation receipts and checkpoints. Resolve uncertain financial/provider outcomes from recorded evidence before deliberately reprocessing. A new publication does not silently repoint an existing trigger or run.

## Child workflows, foreach and parallel branches

Publish the child first, then select its exact version/hash in the parent. Child input is an invoice reference; its output is the recorded run/outcome receipt. Publication validates transitive dependencies. Editing or publishing a child never changes a parent's pin. Retiring a dependency prevents new effects until an explicitly reviewed parent revision adopts a valid replacement.

For unattended work, grant the actor each child version, action and invoice scope explicitly. A parent grant does not imply descendant access. Named human financial approval remains separate for every invocation; revoking the actor's action grant also blocks a pending approval.

Foreach accepts a declared typed invoice collection with unique stable item keys, maximum iterations and timeout. Wire completion, exhaustion and error separately. Parallel accepts two to four named children, explicit concurrency and All/AnySuccess. Write-capable or human-task branches must run serially with All. AnySuccess permits read/pure children, selects a deterministic recorded winner and cancels the remaining reads. A missing or uncertain outcome never wins.

Limits are 100 iterations, four simultaneous branches, depth five, a conservative 1,000 node invocations across the execution tree, and seven days elapsed lifetime. Publication rejects a graph whose worst case exceeds these bounds. Every child uses the existing runner and its own scoped database context. The run page links parent/child evidence. Duplicate delivery reuses the same child and operation identities. Cancellation withdraws pending approvals and propagates to descendants; dispatched effects retain reconciliation evidence. Keep the original database/key ring when restarting API or Worker.

## Qualification evidence

Workflow schema v2 adds durable waits and typed human tasks. Choose one wait duration or an explicit UTC deadline; a run retains its original deadline across restarts and has a seven-day lifetime. An operator can revalidate a due timer but cannot shorten it. For a human task, select a current tenant user or role, purpose, bounded choices and timeout, then wire every response/rejection/timeout route. The recorded expiry is a WorkItem; named-user tasks carry that assignment without turning the worker into that user. Review and respond from the run page. Responses are hash-bound audit evidence, and subsequent financial actions still require their own approval. Cancelling the run invalidates its pending response and timer. Schema v1 publications and checkpoints retain their original interpretation.

Spec 099 sections 13.1–13.10 and GitHub epic #438 carry commit/CI evidence. SQL migration and concurrency qualification runs in CI against an isolated SQL Server container because LocalDB cannot initialize on this development machine. A live consenting Gmail/Pub/Sub rehearsal and the full automatic document/payment/mail journey remain distinct release gates; passing local fixtures does not establish those external prerequisites.
