# Runbook: CodeAct Sandbox Providers

Operator guide for the Python-sandbox provider that backs the personal-finance
sub-agents (`pf-insights`, `pf-forecast`, `pf-classify`) introduced by
[Spec 025](../specifications/025.personal-finance-agent-split-and-codeact.html).

## TL;DR

Three swap-in providers behind `ICodeActSandboxProvider`. Selected at runtime
via `Ai:CodeAct:Provider`:

| Provider | When to use | Notes |
|---|---|---|
| `Disabled` (default) | Always safe — falls through to the conventional tool-loop path. | Pick this in any environment that doesn't have a session pool yet. Sub-agent prompts include "when CodeAct is unavailable the same tools are exposed directly" so the LLM behaviour gracefully degrades. |
| `Hyperlight` | Local Linux dev hosts that expose `/dev/kvm` or `/dev/mshv`. | In-process Hyper-V sandbox via the `Hyperlight.HyperlightSandbox.*` NuGet packages. **Will not work on Azure Container Apps** (no hypervisor device exposed). |
| `AcaSessions` | Cloud deployments (dev/staging/prod) on Azure Container Apps. | Managed Python sandbox over REST. Sub-agent Python calls back into our API via `POST /ai/codeact/call-tool/{nonce}` to invoke host tools. |

If the selected provider can't service a request — wrong host, missing
configuration, missing hypervisor — its `TryBuildExecuteCodeTool` returns
`null` and the sub-agent transparently uses the tool-loop fallback. The
service stays up; the sub-agent quality degrades.

## Architecture (one diagram)

```
Simi (personal-finance-agent)
  └─ pf_run_insights  (one of three sub-agent triggers)
        ↓
        IDomainAgentDescriptor.Build(chatClient, scope)
          ├─ ICodeActSandboxProvider.TryBuildExecuteCodeTool(ctx, hostTools)
          │     ├─ AcaSessions  →  builds an execute_code tool whose handler:
          │     │       • mints HMAC nonce binding (runId,subAgent,tenant,user,whitelist,exp,jti)
          │     │       • bakes nonce + callback URL into a Python preamble
          │     │       • POSTs {preamble + LLM code} to ACA Sessions /executions
          │     │
          │     ├─ Hyperlight   →  in-process Python sandbox; tools wired directly
          │     │
          │     └─ Null          →  returns null  ⇒ fallback path
          │
          └─ Tool-loop fallback (null path)
                tools: hostTools                ← direct, no sandbox
```

When the ACA path runs, the Python in the sandbox calls
`call_tool(name, **kwargs)`. The preamble implements that as an HTTPS POST
back to our API. The route handler
(`src/Aonik.Finance/Endpoints/Agents/CodeActCallbackEndpoint.cs`):

1. Verifies the nonce signature, expiry, and per-nonce callback budget.
2. Re-establishes tenant + user scope from the nonce payload.
3. Looks up the requested tool in the matching sub-agent's whitelist.
4. Invokes it and returns the JSON result.

The nonce is the only auth on the callback endpoint — it's a single bearer
token bound to one sub-agent run.

## Configuration shape

```json
{
  "Ai": {
    "CodeAct": {
      "Provider": "AcaSessions",
      "NonceSigningKey": "<32-byte hex or base64; from Key Vault in cloud>",
      "AcaSessions": {
        "PoolManagementEndpoint": "https://<region>.dynamicsessions.io/subscriptions/<sub>/resourceGroups/<rg>/sessionPools/<pool>",
        "CallbackBaseUrl": "https://<api-fqdn>",
        "SessionCooldownSeconds": 300,
        "NonceTtlSeconds": 600,
        "MaxCallbacksPerNonce": 30,
        "DataPlaneApiVersion": "2024-02-02-preview"
      }
    }
  }
}
```

Convention: deploy-time env vars use `__` separators
(`AI__CODEACT__ACASESSIONS__POOLMANAGEMENTENDPOINT`). The deploy workflow
forwards any var prefixed `AI__` into the API container automatically.

### What ships from Bicep (operator does NOT set these)

The `iac/azure/stacks/aca/main.bicep` stack wires the following directly on
the API Container App (no env-var override required):

- `AI__CODEACT__ACASESSIONS__POOLMANAGEMENTENDPOINT` — built deterministically
  from `${location}.dynamicsessions.io/subscriptions/<sub>/resourceGroups/<rg>/sessionPools/aonik-<env>-sessions`.
- `AI__CODEACT__ACASESSIONS__CALLBACKBASEURL` — built deterministically from
  the API container's FQDN.
- `AI__CODEACT__NONCESIGNINGKEY` — `secretRef` into a Key Vault secret
  (`Ai--CodeAct--NonceSigningKey`) whose VALUE comes from the deploy-time
  parameter `codeActNonceSigningKey` (sourced from GitHub secret
  `AI__CODEACT__NONCESIGNINGKEY`).

### What the operator sets (GitHub environment configuration)

| Kind | Name | Value | Required for |
|---|---|---|---|
| Variable | `AI__CODEACT__PROVIDER` | `AcaSessions` to enable, `Hyperlight` for /dev/kvm hosts, anything else (or unset) for the safe tool-loop fallback. | Always (variable controls path selection). |
| Secret | `AI__CODEACT__NONCESIGNINGKEY` | 32-byte secret, hex or base64 encoded. | Only when `Provider=AcaSessions`. Without it the API still boots, but the first sub-agent invocation throws clearly: "Configuration value 'Ai:CodeAct:NonceSigningKey' is required when the AcaSessions CodeAct provider is enabled." |

Per the `release` skill's GitHub conventions, set these at the **environment
level** (`dev`/`staging`/`prod`), NOT at the repo level — that way prod can
have `Disabled` while dev cuts over to `AcaSessions` independently.

## Opt-in: enable AcaSessions in `dev`

1. **Generate the signing key.** 32 bytes, hex or base64. Either is accepted.
   ```bash
   openssl rand -hex 32
   # or
   openssl rand -base64 32
   ```
   Treat this like a JWT signing key — paste it into the GitHub secret and
   discard the local copy.

2. **Add the GitHub environment secret.**
   - Repository → Settings → Environments → `dev` → Add secret.
   - Name: `AI__CODEACT__NONCESIGNINGKEY`.
   - Value: the hex/base64 string from step 1.

3. **Add the GitHub environment variable.**
   - Same Environments → `dev` page → Variables → Add variable.
   - Name: `AI__CODEACT__PROVIDER`.
   - Value: `AcaSessions`.

4. **Trigger a runtime deploy.**
   ```bash
   gh workflow run "CD: Deploy" \
     -f environment=dev \
     -f mode=deploy \
     -f use_digest_references=true
   ```
   Approve the environment protection gate when GitHub Actions prompts.

5. **Verify in the playground.**
   - Open `https://aonik-dev-adminui.<defaultDomain>/ai/playground`.
   - Agent: `personal-finance-agent`. Model: any.
   - User Brief → Real User → pick a seeded persona (e.g. Seamus Keane).
   - Ask `"Why was last month tight?"`.
   - In the SSE stream (DevTools → Network → the `/ai/playground/run`
     request → EventStream), look for
     `TOOL_CALL_START name=execute_code` — this proves the ACA path ran.
     If you see `TOOL_CALL_START name=pf_list_snapshot_history` (or any
     other parent-level tool) without an `execute_code` start, Simi answered
     directly without invoking the sub-agent — that's expected for some
     question shapes.

## Kill switch

To revert to the tool-loop fallback without redeploying images:

1. Update the GitHub environment variable `AI__CODEACT__PROVIDER` to
   `Disabled` (or delete the variable entirely).
2. Re-run the deploy workflow. The new revision will load
   `NullCodeActSandboxProvider`, `TryBuildExecuteCodeTool` always returns
   `null`, and every sub-agent invocation takes the conventional path.

The signing key secret and Key Vault entry can stay — they're inert until
the provider is re-enabled.

## Failure modes

| Symptom | Likely cause | Fix |
|---|---|---|
| API boots, sub-agent invocation throws `Configuration value 'Ai:CodeAct:NonceSigningKey' is required` | Provider set to `AcaSessions` but `AI__CODEACT__NONCESIGNINGKEY` secret is missing or empty. | Set the GitHub environment secret and redeploy. The secret pushes into Key Vault → API picks it up on next container start. |
| Sub-agent invocation returns `call_tool_shadowed` | LLM-generated Python redefined `call_tool` (e.g. `def call_tool(...):` or `call_tool = lambda ...`). | The pre-check rejected it before HTTP. Adjust the sub-agent's prompt if the LLM keeps doing this; otherwise it's the LLM's mistake and a re-prompt should fix it. |
| Sub-agent invocation returns `tool_not_in_whitelist` from the callback | The Python script called a tool name the sub-agent's slice doesn't expose. | Check that the tool is included in the matching `PersonalFinanceTools.CreateForXxxSubAgent` whitelist. |
| Sub-agent invocation returns `budget_exhausted` (HTTP 429) from the callback | A single `execute_code` script fired more than `MaxCallbacksPerNonce` (default 30) callbacks. | Either tighten the script (use `pf_compare_snapshots` instead of N `pf_get_category_breakdown` calls) or bump `AI__CODEACT__ACASESSIONS__MAXCALLBACKSPERNONCE` if a higher cap is genuinely needed. |
| Bicep deploy fails with `SessionPoolInvalidPoolManagementType` | The session pool resource is being created with a value other than `Dynamic`. | Confirm `iac/azure/modules/sessions.bicep` uses `poolManagementType: 'Dynamic'` (the only accepted value; the docs' "System code interpreter pool" wording is misleading). |
| Bicep deploy fails with `LocationNotAllowed` for `Microsoft.App/sessionPools` | The region we deploy ACA into doesn't yet support session pools. | Override the location for the sessions module via the `location` parameter on the `sessions` bicep module. The endpoint URL is built deterministically from `${location}` so the API will still find it. |
| ACA Sessions returns HTTP 401 even with a fresh token | The API's system-assigned identity doesn't have the `Azure ContainerApps Session Executor` role on the pool. | The bicep grants this automatically (`iac/azure/modules/sessions.bicep`'s `sessionExecutorRoleForApi`). If it's missing, check the role assignment by `az role assignment list --scope <session-pool-id>`. |
| Sub-agent runs but reports `no recorded transactions` for a user that clearly has them | Impersonation propagation bug — Simi's parent scope sees the impersonated user but the sub-agent's scope doesn't. | Separate from sandbox provider work; see the open follow-up "Fix sub-agent impersonation propagation". The bug exists for both `Disabled` and `AcaSessions` paths. |

## Security notes

- The nonce is the ONLY thing preventing cross-execution Python state
  leakage in a shared session pool. Don't weaken it (e.g. switch to a
  static bearer) without rethinking the threat model — session identifiers
  are reused within a sub-agent run on purpose so Python state persists
  between `execute_code` invocations.
- The session identifier is `HMAC(RunId + SubAgentName)`. It never
  contains the user ID, so log leakage of the identifier doesn't reveal the
  scope. The user ID is in the nonce payload only.
- The callback endpoint enforces the tool whitelist server-side. The Python
  preamble baking the whitelist into the nonce is defence in depth — a
  leaked nonce can still only invoke the tools that sub-agent was built
  for, even if the attacker rewrites the preamble.
- ACA Sessions runs the sandbox `EgressEnabled` so the preamble can POST
  back to us. If we ever tighten this, switch to a VNet-integrated pool
  with an explicit egress allowlist to the API FQDN only.

## Code references

- Provider abstraction: `src/Aonik.SharedKernel/Abstractions/Agents/ICodeActSandboxProvider.cs`
- Providers: `src/Aonik.Finance/Agents/CodeAct/{Hyperlight,AcaSessions,Null}CodeActSandboxProvider.cs`
- Nonce service: `src/Aonik.Finance/Agents/CodeAct/CodeActCallbackNonceService.cs`
- ACA REST client: `src/Aonik.Finance/Agents/CodeAct/AcaSessionsClient.cs`
- Callback endpoint: `src/Aonik.Finance/Endpoints/Agents/CodeActCallbackEndpoint.cs`
- Options: `src/Aonik.Finance/Agents/CodeAct/AcaSessionsOptions.cs`
- DI selector: `src/Aonik.Finance/FinanceModule.cs` (search `ICodeActSandboxProvider`)
- Bicep: `iac/azure/modules/sessions.bicep`, secret in `iac/azure/modules/data.bicep`, wiring in `iac/azure/stacks/aca/main.bicep`
- Unit tests: `tests/Aonik.Application.Tests/Agents/CodeAct/`

## See also

- [Spec 025](../specifications/025.personal-finance-agent-split-and-codeact.html) — the design rationale for the three sub-agents and the CodeAct pattern.
- [Runbook: Deploy Runtime](deploy-runtime.md) — generic CD: Deploy workflow.
- [Azure Container Apps Dynamic Sessions docs (Microsoft Learn)](https://learn.microsoft.com/en-us/azure/container-apps/sessions-code-interpreter).
