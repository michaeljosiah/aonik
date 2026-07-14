# ADR-013: Product Identity Is Configuration, Not Platform Code

**Status:** Accepted (principle binds new work immediately; extraction of existing drift deferred to [Spec 064](../specifications/064.debrand-product-identity-from-platform-code.html))
**Date:** 2026-07-14
**Related:** [Spec 064](../specifications/064.debrand-product-identity-from-platform-code.html) (the extraction) · [Spec 033](../specifications/033.tenant-managed-agent-extensibility.html) (the persona-as-config seam) · [Spec 062](../specifications/062.circle-scoped-ai-read-tools.html) / [Spec 063](../specifications/063.circle-member-write.html) (first specs written to this convention) · [ADR-005](005-adopt-module-first-modular-monolith.md) · [ADR-006](006-extract-personal-finance-module.md)

## Context

AONIK is a modular, multi-tenant **agent platform** meant to be used by any organisation. Its products — **Simi** (B2C), **Payabo**, **MyBillAfrica**, **RemitExchange** — are built by *configuring a tenant on the platform*, not by branching platform code. That is the platform's reason to exist (ADR-005 module-first, ADR-006 PersonalFinance as a generic sibling module).

The code has drifted. A survey found **~115 references to one product, "Simi", across ~40 source files** — and not only in the consumer surface, but baked into compiled *platform* code:

- **The agent persona is hard-coded.** `PersonalFinanceAgentDescriptor.Instructions` opens *"You are Simi, AONIK's personal finance companion"* and carries Simi's name, tone, and rules inline as a `const`.
- **Platform tools are product-named.** `SimiKeeperTools` and the `simi_*` tool names (`simi_list_care_entities`, `simi_get_entity_profile`, …) break the module's own generic `pf_*` convention.
- **A wire field is product-named.** `simiTool` / `SimiTool` in the sub-agent structured-output contract (`InsightsStructuredModels` et al.).
- **A persisted/printed format is product-named.** `SupportStatementService` mints Support Statement verification codes prefixed `SIMI-` — printed on a PDF a member or accountant may keep.
- **Platform layers name the product in comments.** `Aonik.Documents`, `Aonik.Finance`, and `Aonik.SharedKernel` reference *"Simi's Vault"*, *"a Simi target"*, *'the "Simi" FX tool'*.

This couples the reusable engine to one brand: a second product configured on the platform would inherit Simi's name in its tool schemas, prompts, and printed artefacts. It also contradicts a capability the platform **already has** — the `Agent` entity's two-level (global + per-tenant) override of instructions, model, and enabled tools (`ToolsetIdsJson`), the mechanism [Spec 033](../specifications/033.tenant-managed-agent-extensibility.html) builds on. A product persona is exactly what that override is for; it was baked into the default instead of configured through the seam that exists.

Not everything is drift. `SIMI_TENANT_ID` is **correct** — Simi genuinely *is* a tenant, and a tenant id living in config/env is the right model. A product's own **mobile app carrying its brand** (e.g. the "SIMI" voice label) is also fine — that is the product layer, where identity belongs.

## Decision

**No platform code shall encode a product's identity. Product identity is tenant/agent configuration + data + branding.** The line:

| Concern | Belongs in… | Rule |
| --- | --- | --- |
| Domain capability (care entities, commitments, payment logs, vault, circle, describe-only reads) | **Platform code**, generic | Named by function; no product noun. |
| Agent tool names / classes / DB columns / JSON fields | **Platform code**, generic | Function-scoped prefix (`pf_*` for personal finance); a product name never appears in a symbol the model or a client binds to. |
| Agent persona: display name, system prompt, tone, enabled tools | **Tenant config** | The `Agent` per-tenant override row (instructions, `ModelId`, `ToolsetIdsJson`); Spec 033. |
| Branding literals (e.g. the Support Statement verification-code prefix) | **Tenant/branding config** | A setting with a neutral default; never a hard-coded product string. |
| Tenant identity (`SIMI_TENANT_ID`) | **Config / env** | Already correct — leave. |
| A product client app's own brand | **Product layer** | Already correct — leave. |

### Naming convention (binds new work from today)

- Platform agent tools use a **function-scoped prefix matching the module** (`pf_*` for personal finance). Product names never appear in a tool name, class name, DB column, or structured-output field.
- The persona name ("Simi") appears only in **configuration** (an `Agent` override's instructions/display name) and in **product client apps** — never in a platform symbol or a default prompt.
- The platform's default agent prompt is **generic and warm-but-unbranded**; a tenant supplies the product voice.

### Applies now vs. extraction

This ADR **binds all new work immediately**: no new product-named platform symbol ships. The **existing** drift is extracted per [Spec 064](../specifications/064.debrand-product-identity-from-platform-code.html), deferred to its own change because the renames cross **wire/persisted surfaces** — tool-name strings referenced in tenant tool allow-lists, the `simiTool` schema field, and printed `SIMI-` codes — that need a coordinated, migration-aware sweep. Simi is pre-launch, so that sweep can be a **clean rename** rather than a back-compat shim.

## Consequences

### Positive
- The platform stays genuinely reusable: a second product configures a tenant and inherits no other product's name in its tool schemas, prompts, or artefacts.
- Honest architecture: the persona-as-configuration seam (the `Agent` override; Spec 033) is *used*, not bypassed by a baked default.
- Every future spec inherits a clear, testable rule — and a review/lint check ("no product noun in a platform symbol") becomes possible. Specs 062/063 are the first written to it.

### Trade-offs
- The rename is more than cosmetic: it touches wire/persisted names, so Spec 064 must sequence it with the tenant tool allow-list and any stored tool references — deferred, not free.
- Stripped of "Simi", the default agent reads generically in code; the product warmth now lives in the tenant's configured prompt. That is the intent, but it moves where the personality is authored.

## Alternatives Considered

- **Leave it (rename nothing).** Rejected: the coupling defeats the platform's purpose and spreads with every new product-named symbol — as specs 062/063 demonstrated by adding `SimiCircleTools`/`simi_*` before this ADR caught it.
- **Rename symbols but keep the persona in the default instructions.** Rejected: the persona (name, tone, prompt) *is* the product identity; leaving it in compiled code just relocates the violation. It belongs in the `Agent` config override.
- **Build a full per-tenant theming/branding engine now.** Rejected (YAGNI): the `Agent` two-level override plus a couple of branding settings cover the real need; a general theming system is speculative.

## See Also
- [Spec 064 — Extract product identity ("Simi") from platform code](../specifications/064.debrand-product-identity-from-platform-code.html)
- [Spec 033 — Tenant-managed agent extensibility](../specifications/033.tenant-managed-agent-extensibility.html) (the `Agent` per-tenant override; the persona-as-config home)
- [Spec 062](../specifications/062.circle-scoped-ai-read-tools.html) / [Spec 063](../specifications/063.circle-member-write.html) (the circle deltas, written to this convention)
- [ADR-005](005-adopt-module-first-modular-monolith.md) (module-first) · [ADR-006](006-extract-personal-finance-module.md) (PersonalFinance as a generic sibling module)
