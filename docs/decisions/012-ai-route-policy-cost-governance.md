# ADR-012: AI Route-Policy Cost Governance & Model Fallback — Define the Semantics, Defer the Build

**Status:** Accepted (design only — no implementation lands with this ADR)
**Date:** 2026-07-06
**Related:** [#215](https://github.com/michaeljosiah/aonik/issues/215), split from [#116](https://github.com/michaeljosiah/aonik/issues/116) (M3) · [Spec 060](../specifications/060.ai-route-policy-cost-governance.html)

## Context

[#116](https://github.com/michaeljosiah/aonik/issues/116) removed two columns from `AiRoutePolicy` — `CostCeiling` and `FallbackModelIdsJson` — because they were **displayed in the admin UI but never enforced**. A governance control that looks active but does nothing is worse than no control: an operator sets a ceiling, believes spend is capped, and it is not. Removal was behaviour-preserving; the enforcement *ambition* was deferred to #215 so it could be built properly rather than re-bolted on with guessed semantics.

Enforcement is a feature, not a wiring change, because two subsystems do not exist yet:

- **Cost ceiling** needs spend accounting: a per-scope budget tracked over a defined window, a *pre-flight* cost estimate (today `AiRun.CostEstimate` exists only *after* the fact), a running tally, and a defined action when the ceiling is hit.
- **Model fallback** needs a change to the resolution contract: `IAiModelResolver.ResolveModelNameAsync` returns a *single* model name; failover needs an *ordered chain* plus runtime-failure detection and retry, rippling to every call site.

The blocker to building it was never the code — it was the **undefined semantics**. #215 asked for one of two outcomes: a short ADR + spec defining those semantics, or an explicit decision to keep routing primary-only and close.

## Decision

**Define the semantics now in an ADR + spec; do not implement until there is a concrete need** (real cost pressure, or a tenant/commercial requirement for a hard budget). We choose the design-first path over both closing the issue and building speculative infrastructure.

The semantics below are the contract a future implementation MUST honour. Full rationale and the phased build plan live in [Spec 060](../specifications/060.ai-route-policy-cost-governance.html); this ADR records the load-bearing decisions.

### Cost ceiling

| Question | Decision |
| --- | --- |
| **Unit** | A **monetary budget in USD** over a window — never a token count or a per-request cap (those don't compose across models with different pricing). |
| **Scope** | **Per-tenant** is the primary boundary (it is the billing/isolation boundary). A route policy MAY carry its own optional sub-ceiling, checked *in addition to* the tenant budget. Per-use-case is expressed through route policies, not a third scope. |
| **Window / reset** | **Rolling calendar month, UTC**, reset at the month boundary. The tally is a persisted counter keyed `(tenantId, yyyy-MM)` (and `(routePolicyId, yyyy-MM)` for policy sub-ceilings), incremented atomically. |
| **Estimation** | **Pre-flight**: `max(estimatedInputTokens × modelInputPrice, floor)` from the existing `AiCostCatalog` pricing table — a conservative upper bound charged *before* the call. **Reconciliation**: the true `AiRun.CostEstimate` replaces the estimate after completion so the tally self-corrects. |
| **Action at ceiling** | Configurable per policy: **`AlertOnly`** (emit a `MoneyActionLog`/metric, do not block), **`Downgrade`** (drop to the next model in the fallback chain), or **`Reject`** (fail the call with a typed `AiBudgetExceededException`). **Default `AlertOnly`** — fail-*open* for availability. Blocking a tenant's AI on a spend threshold is a deliberate, opt-in operator choice, not a default. |

### Model fallback

| Question | Decision |
| --- | --- |
| **Contract** | Add `IAiModelResolver.ResolveModelChainAsync → IReadOnlyList<string>` (primary first, then fallbacks) sourced from the policy's fallback list. The existing single-name method becomes a thin wrapper returning `chain[0]`, so non-failover callers are unchanged. |
| **Triggers** | Advance to the next model **only** on: model inactive/unknown (config), provider rate-limit (429), timeout, or transient overload/5xx. **Never** on: content-policy rejection, authentication/authorization failure, or a valid-but-unwanted response — those are terminal and must surface. |
| **Retries** | Each model in the chain is tried **once, in order**, with a per-attempt timeout; total attempts are **capped (default 3)** to bound tail latency. An optional short backoff applies to rate-limit only. |
| **Audit** | `AiRun` records the **model that actually served** the request (`ResolvedModelId`) alongside the requested one, plus the fallback reason. Every downgrade is visible; the trace name is unaffected (the use-case stamp still drives it, per the trace-explorer dedupe convention). |

### Columns

`AiRoutePolicy.CostCeiling` (decimal?, USD), `CostCeilingAction` (string enum), and `FallbackModelIdsJson` (string) are **re-introduced via a tool-generated migration only when implementation lands** — not by this ADR. The admin UI re-exposes them only once they are enforced, so "displayed but inert" never recurs.

## Consequences

### Positive
- The next person to pick this up inherits **defined semantics**, not a blank cheque — no re-litigating unit/scope/window/action from scratch.
- Keeps the platform honest: no inert governance control ships. The columns return **with** their enforcement, together.
- The fallback-chain contract is additive (single-name wrapper), so it can land incrementally ahead of cost accounting.

### Trade-offs
- The issue stays **open** (design-complete, implementation-pending) rather than closed. Accepted: the ambition is real, just not yet needed.
- A future implementer is bound to these semantics; genuinely better ideas require superseding this ADR, not silently diverging.

## Alternatives Considered

- **Close as won't-do (keep routing primary-only).** Rejected: cost governance is a plausible near-term need for a multi-tenant AI platform, and discarding the analysis wastes it. Deferral with a written design is cheaper to resume than a cold restart.
- **Build it now.** Rejected: no current consumer, no spend-accounting subsystem, and (until this ADR) no agreed semantics — the exact "bolted on with guessed semantics" the split from #116 set out to avoid.
- **Re-add the columns now, enforce later.** Rejected outright: reinstates the displayed-but-inert control #116 removed.

## See Also
- [#215](https://github.com/michaeljosiah/aonik/issues/215) · [#116](https://github.com/michaeljosiah/aonik/issues/116) (M3)
- [Spec 060 — AI route-policy cost governance & model fallback](../specifications/060.ai-route-policy-cost-governance.html)
- `docs/observability/money-action-logging.md` (the `MoneyActionLog` convention the `AlertOnly` action reuses)
