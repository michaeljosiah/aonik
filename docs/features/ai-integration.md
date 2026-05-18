# AI Integration

AONIK includes a dedicated AI module (`Aonik.Ai`) and an Agents module (`Aonik.Agents`) that together provide AI-native capabilities across the platform.

## Current State

- **Microsoft Agent Framework (MAF)** is adopted for agent orchestration (via `Microsoft.Extensions.AI` / `IChatClient`).
- AI providers, models, and routing policies are managed as platform entities.
- Prompts and tools are versioned and immutable once published.
- Every AI execution is recorded as an `AiRun` for audit and governance.
- Agents follow the **Propose -> Approve -> Apply** pattern and cannot directly mutate financial state.
- MCP (Model Context Protocol) servers are available per domain module (`Aonik.Finance.Mcp`, `Aonik.Platform.Mcp`).

## Agent Framework Architecture

### Domain Agent Descriptors

Domain agents are registered via the `IDomainAgentDescriptor` interface, using `IEnumerable<IDomainAgentDescriptor>` multi-registration in DI. Each descriptor defines:

- **Name** and **Description** for the agent
- **Instructions** (system prompt) for the LLM
- **Tools** (via `GetToolsAsync(IServiceProvider)`) built with `AIFunctionFactory.Create()`

Current domain agent descriptors:

| Descriptor | Module | Tools |
|-----------|--------|-------|
| `FinanceAgentDescriptor` | `Aonik.Finance` | ~14 billing, ledger, and payment tools |
| `FinancialLifeGraphAgentDescriptor` | `Aonik.Finance` | ~17 Financial Life Graph read tools |
| `PlatformAgentDescriptor` | `Aonik.Platform` | Tenant, user, and compliance tools |

### Master Orchestrator

`MasterOrchestratorService` builds a `ChatClientAgent` orchestrator that composes domain agents as tools via `agent.AsAIFunction()`. Key behaviors:

- Resolves all `IDomainAgentDescriptor` instances from DI
- Builds and caches the orchestrator agent (thread-safe via `SemaphoreSlim`)
- Uses MAF `AgentSession` via `agent.CreateSessionAsync(sessionId)` for native conversation history tracking (no in-process memory leaks)
- Integrates MCP tools from `McpToolProvider` alongside domain agent tools
- Gracefully degrades if MCP servers are unavailable

### Human-in-the-Loop (Approval)

Mutating tools are wrapped with MAF's `ApprovalRequiredAIFunction` to enforce human approval before execution. This replaces the previous custom `ProposalMiddleware`.

**Mutating tools requiring approval:**
- `CreateInvoice`, `IssueInvoice`, `CancelInvoice`, `MarkInvoicePaid`
- `CreatePaymentIntent`, `CapturePayment`, `CancelPayment`
- `CreateLedger`, `CreateAccount`

Read-only tools (all Get/List operations, all FLG tools) execute without approval gates.

### Audit Pipeline

`AuditMiddleware` (in `Aonik.Ai.Middleware`) is wired into the `IChatClient` pipeline using the `.AsBuilder().Use(...)` pattern. It integrates with `IAiRunWriter` to:

- Call `StartRunAsync` before each LLM invocation
- Call `MarkRunCompletedAsync` or `MarkRunFailedAsync` after completion
- Capture `response.Usage` token counts for cost tracking

### Workflows

Workflow classes implement the `IWorkflowFactory` interface and are registered as keyed singletons:

| Workflow | Key | Purpose |
|---------|-----|---------|
| `InvoiceProcessingWorkflowFactory` | `invoice-processing` | Invoice lifecycle orchestration |
| `OnboardingWorkflowFactory` | `onboarding` | Tenant onboarding steps |
| `ReconciliationWorkflowFactory` | `reconciliation` | Ledger reconciliation |

Workflows are resolved via `IServiceProvider.GetKeyedService<IWorkflowFactory>(name)` in `RunWorkflowEndpoint`.

> **Advisory note:** Current workflows are advisory-only. They define agent composition patterns using MAF's `AgentWorkflowBuilder` but do not have tools wired for direct financial mutations.

## Where to Look

### AI Module (`Aonik.Ai`)

- Entities: `src/Aonik.Ai/Entities/` (AiProvider, AiModel, AiRoutePolicy, PromptSpec, ToolSpec, AiRun, etc.)
- Middleware: `src/Aonik.Ai/Middleware/AuditMiddleware.cs`
- Persistence: `src/Aonik.Ai/Persistence/`
- Services: `src/Aonik.Ai/Services/`
- Module registration: `src/Aonik.Ai/AiModule.cs` (registers `IChatClient` with audit pipeline)

### Agents Module (`Aonik.Agents`)

- Contracts: `src/Aonik.Agents/Contracts/Services/` (`IDomainAgentDescriptor`, `IWorkflowFactory`, `IMasterOrchestratorService`, `IMcpToolProvider`)
- Entities: `src/Aonik.Agents/Entities/` (Agent, AgentRun, Proposal, OrchestratorPolicy)
- Framework: `src/Aonik.Agents/Framework/MasterOrchestratorService.cs`, `McpToolProvider.cs`
- Workflows: `src/Aonik.Agents/Workflows/` (keyed `IWorkflowFactory` implementations)
- Endpoints: `src/Aonik.Agents/Endpoints/` (ChatEndpoint, ListAgentsEndpoint, RunWorkflowEndpoint)
- Persistence: `src/Aonik.Agents/Persistence/`

### Domain Agent Registrations

- Finance: `src/Aonik.Finance/Agents/FinanceAgentRegistration.cs` (`FinanceAgentDescriptor`, `FinancialLifeGraphAgentDescriptor`)
- Platform: `src/Aonik.Platform/Agents/PlatformAgentRegistration.cs` (`PlatformAgentDescriptor`)
- Tools: `src/Aonik.Finance/Agents/Tools/` and `src/Aonik.Platform/Agents/Tools/`

### MCP Servers

- Finance MCP: `src/Aonik.Finance.Mcp/`
- Platform MCP: `src/Aonik.Platform.Mcp/`

## Key Architectural Decisions

- [ADR-001: Custom AI -> MAF adoption](../decisions/001-custom-ai-implementation-vs-maf.md)
- [ADR-004: Adopt Microsoft Agent Framework](../decisions/004-adopt-microsoft-agent-framework.md)
- [ADR-005: Modular monolith restructuring](../decisions/005-adopt-module-first-modular-monolith.md)

## Personal-finance sub-agents (Spec 025)

Simi (personal-finance-agent) delegates analytical work to three read-only
sub-agents — `pf-insights`, `pf-forecast`, `pf-classify` — via the
`pf_run_insights`, `pf_run_forecast`, and `pf_run_classify_review` trigger
tools. Each sub-agent runs a single `execute_code` call inside a Python
sandbox that calls back into Simi's host tools through a `call_tool(name,
**kwargs)` bridge — one LLM hop typically replaces 50+ sequential tool
invocations.

The sandbox provider is pluggable behind `ICodeActSandboxProvider`:

| Provider | Hosts | Notes |
|---|---|---|
| `Hyperlight` | Local Linux dev with `/dev/kvm` or `/dev/mshv` | In-process Hyper-V sandbox; uses `Hyperlight.HyperlightSandbox.*` NuGet packages. |
| `AcaSessions` | Azure Container Apps (cloud) | Managed sandbox over REST; Python posts back to `POST /ai/codeact/call-tool/{nonce}` to invoke host tools. |
| `Disabled` (default) | Anywhere | Forces the conventional tool-loop fallback — the sub-agent prompts gracefully degrade with no quality loss other than higher LLM-turn count. |

Selected at runtime by `Ai:CodeAct:Provider`. Each sub-agent descriptor's
`Build()` calls `ICodeActSandboxProvider.TryBuildExecuteCodeTool(...)`; a
`null` return means the provider can't service the request and the
sub-agent gets the conventional `tools: hostTools` configuration instead.

- Operator guide: [Runbook: CodeAct sandbox providers](../runbooks/codeact-sandbox-providers.md)
- Design rationale: [Spec 025 — Personal Finance Agent Split & CodeAct](../specifications/025.personal-finance-agent-split-and-codeact.html)

## Related Documentation

- [AI Observability (OpenTelemetry + Langfuse)](ai-observability.md) — Trace instrumentation, OTLP exporters, sensitive data controls
