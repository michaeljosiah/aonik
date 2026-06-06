# ADR-008: General-Purpose Task Primitive (WorkItem) in the Platform Module

**Status**: Proposed
**Date**: 2026-06-02
**Decision Makers**: Development Team
**Related**: [ADR-005](005-adopt-module-first-modular-monolith.md), [ADR-006](006-extract-personal-finance-module.md), [Spec 034](../specifications/034.task-work-item-scheduling.html), [Spec 030](../specifications/030.proposal-execution-dispatcher.html), [Spec 014](../specifications/014.background-jobs-quartz-persistence-admin.html)

## Context

Several product needs share one shape but have no common home:

- A Payabo customer wants to be **reminded** before their insurance renews.
- A customer wants a recurring **nudge** (or a scheduled payment) before sending monthly family support.
- The system owner wants to **assign an agent** a standing job ("every Monday, draft a cash-position summary").

Each is *"do this thing, about this subject, at this time (or on this cadence)."* Today AONIK can schedule **code** (Quartz jobs — [Spec 014](../specifications/014.background-jobs-quartz-persistence-admin.html)), deliver **notifications** (Platform `INotificationService`), run **agents**, and gate high-risk mutations behind **proposals** ([Spec 030](../specifications/030.proposal-execution-dispatcher.html)) — but there is no **data-defined, user-/module-authored unit of future work**. A Quartz `IJob` is compiled by a developer and runs the same logic platform-wide; it is not a per-tenant, per-user row created at runtime with its own subject and payload.

Without a shared primitive, the three needs would be built three times — three sets of entities, jobs, and endpoints across PersonalFinance, Finance, and Agents — for what is fundamentally one model.

## Decision

Introduce a general-purpose task primitive, the **`WorkItem`**, owned by the **Platform** module, and route due work to a **keyed action handler** that each module contributes.

### Key choices

1. **One model, four dimensions.** A task is fully described by *subject* (what it is about), *schedule* (when), *assignee* (who acts), and *action* (what happens) — plus lifecycle. Two anemic, tenant-scoped entities capture it: `WorkItem` (the durable task) and `WorkItemRun` (each execution occurrence — the audit/idempotency anchor, mirroring `WorkflowRun`/`AiRun`).

2. **Placement: inside the Platform module**, beside Notifications. The task system is cross-cutting platform infrastructure used by many modules; it reuses `PlatformDbContext` rather than standing up a new module and DbContext. (A dedicated `Aonik.Tasks` module was considered and rejected as premature scaffolding; it can be extracted later using the ADR-006 pattern if scale demands.)

3. **Execution via keyed `ITaskActionHandler`**, the same shape as the shipped `IProposalHandler` dispatch (keyed by `ProposalType`). A due task carries an `ActionType` string; the dispatcher resolves the handler registered for it. This was chosen over a fire-and-forget `WorkItemDueEvent` because it returns a **structured result** (recorded on the run, including any spawned proposal/AI run), supports **retry with attempt counting**, and has clear failure semantics. The contract lives in `SharedKernel.Abstractions.Tasks` so modules implement handlers without depending on Platform's task code.

4. **Durable rows are truth; one Quartz job is the clock.** A single `WorkItemDispatchJob` heartbeat (every minute, `[DisallowConcurrentExecution]`) scans due rows, **leases** each (the `OutboxProcessor` pattern — clustering-safe), inserts a unique `(WorkItemId, ScheduledForUtc)` run row (occurrence idempotency), dispatches, and re-arms recurrences. We explicitly reject one-Quartz-trigger-per-task: it does not scale to many tenants × many reminders and complicates clustering.

5. **Naming: the CLR entity is `WorkItem`, not `Task`**, to avoid colliding with `System.Threading.Tasks.Task` in every service signature. Product/API vocabulary stays "task" (`/tasks/*`, `ITaskService`, `ScheduleTaskRequest`/`TaskResponse`).

6. **The scheduler never bypasses human-in-the-loop for mutations.** A `WorkItem` action handler **must not** call a high-risk domain service (payment capture, payout, ledger posting, partner call) directly. For such actions its only permitted effect is to **create a `Proposal`** (Spec 030), returning `Outcome=Proposed`; money moves only after approval. This keeps the new on-its-own-clock capability from becoming a backdoor around the Spec 030 / Spec 032 mutation boundary. "Pay mum's bill automatically" means "*on schedule, raise a proposal that is approved*" — not "the scheduler moved money."

### Cross-module contract

`SharedKernel.Abstractions.Tasks.ITaskService` (`ScheduleAsync`, `Get/List/Pause/Resume/Cancel`) is the only surface other modules touch — they never reference Platform's `WorkItem` entities, consistent with the ADR-006 SharedKernel-contract boundary. Modules contribute behaviour by registering `ITaskActionHandler` implementations keyed by `ActionType` (`notify_user` → Platform, `create_payment_proposal` → Finance, `run_agent` → Agents).

## Consequences

### Positive

- Future work is scheduled with one `ITaskService.ScheduleAsync` call; no module touches Quartz.
- Adding a new kind of action is one `ITaskActionHandler` in the owning module — the scheduler is closed to modification, open to extension.
- The propose-don't-execute rule is preserved by construction; the scheduler reuses the audited Spec 030 path for anything risky.
- Reuses existing infrastructure (Quartz Worker host, lease pattern, notifications, proposals, `AiRun`) rather than inventing new machinery.

### Trade-offs

- **Minute-level granularity only.** Sub-minute/real-time scheduling stays the domain of code-defined Quartz jobs (Spec 014). Acceptable for reminders/nudges/agent jobs.
- **Platform module surface grows.** Tasks join Notifications as Platform-owned cross-cutting infra. Mitigated by the clean SharedKernel contract, which keeps later extraction cheap.
- **A new self-firing capability raises the stakes of the mutation boundary.** Addressed by the hard rule in choice 6 and covered by acceptance tests (no money moves without an approved proposal).
- **Two new DbSets on the canonical `AonikDbContext`** plus `PlatformDbContext`, with one tool-generated migration (`AnkWorkItems`, `AnkWorkItemRuns`).
- **Delivery is at-least-once, not exactly-once-in-execution.** The lease + unique `(WorkItemId, ScheduledForUtc)` run row guarantee one run *row* per occurrence, but — as with any lease-based dispatcher (the `OutboxProcessor` included) — a worker that stalls past its lease and later resumes can briefly run a handler concurrently with its replacement. The dispatcher shrinks this window to near-zero with **lease renewal** (the holder heart-beats while a handler runs, so a slow handler is never reclaimed) and protects its own bookkeeping with a **fenced outcome write** (a worker that lost its lease abandons the occurrence rather than double-recording). The residual is closed by the standing contract that **action handlers must be idempotent for their side effects** (GET-before-act), exactly as proposal handlers are and as the outbox relies on inbox idempotency. The reference `notify_user` handler models this by keying each notification on the occurrence's run id.

## See Also

- [Spec 034](../specifications/034.task-work-item-scheduling.html) — full specification: domain model, action-handler seam, dispatch mechanics, scenario walk-throughs, phasing, acceptance criteria, open decisions.
- [ADR-006](006-extract-personal-finance-module.md) — the SharedKernel-contract / no-cross-module-reference pattern this follows.
- [Spec 030](../specifications/030.proposal-execution-dispatcher.html) — the `IProposalHandler` pipeline this mirrors and the high-risk execution path it delegates to.
