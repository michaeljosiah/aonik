# Changelog

All notable changes to the AONIK project will be documented in this file.

## [Unreleased]

## [0.1.0] - 2026-05-12

## [0.0.1-rc.1] - 2026-05-10

### Added
- **Observability (Service Topology Runtime Controls)**: Restored the Admin UI service-topology view as a dedicated Observability route, enriched topology nodes with live Azure Container Apps runtime state, and added a dev-focused operator action to wake startable services such as the background worker from scaled-to-zero directly from the topology surface.
- **AONIK CLI (Initial Foundation)**: Added a new `Aonik.Cli` console application and `Aonik.Cli.Tests` project with a simple command-driven operator experience for AONIK authentication and agent access. The initial release includes file-backed session persistence, `auth login/status/whoami/logout`, `agent list/run/stream/threads/thread`, approvals and explicit ops commands, a lightweight interactive `shell` powered by Spectre.Console, JSON/text/NDJSON output modes for agent and harness use, focused tests covering session storage, command handlers, and command-tree wiring, plus user-facing documentation in `docs/guides/aonik-cli.md` and `src/Aonik.Cli/README.md`.
- **Admin UI AI Playground (Voice Mode + Frontend Tool Coverage)**: Added voice-mode playback to the Admin UI AI Playground using the same `speech.render` flow as mobile, wired the playground streaming endpoint to accept client tool definitions and emit speech render metadata, aligned frontend tool reruns with the AG-UI assistant/tool message contract, added automated frontend tests covering all supported playground frontend tools (`confirmAction`, `display_fx_rate_chart`, `display_budget_breakdown`, `display_autopilot_proposal`), and preserved rendered speech text plus clearer provider/quota failures in the output panel when live synthesis cannot complete.
- **Payabo Mobile Push Delivery**: Added Firebase-backed mobile push foundations across Payabo mobile and the AONIK backend. The Flutter app now registers Android FCM device tokens, handles notification-tap routing into app screens, and the backend now persists notification devices, dispatches push payloads through FCM when notifications are created, deactivates invalid tokens reported by the provider, and includes the Infrastructure migration plus deployment workflow wiring needed to flow FCM configuration into dev/runtime environments.
- **Personal Finance (Customer Insight Snapshot Pipeline)**: Added the deterministic snapshot, AI interpretation, prompt projection, legacy-path convergence, and release-readiness verification foundations for customer insight generation, including the canonical `CustomerInsightSnapshot` and `CustomerInsightAiSummary` artifacts with migrations, structured snapshot and summary schemas, snapshot-based AI interpretation with `AiRunId` linkage, current-versus-superseded versioning for both layers, checkpointed worker jobs for snapshot and AI summary batches, admin/user read and rebuild surfaces for the new artifacts, `UserBrief` projection updates that inject canonical snapshot and AI interpretation context with partial/no-summary fallbacks and updated token-budget prioritization, cleanup of older behavioural/narrative flows so generic `Insight` rows now remain secondary/admin artifacts derived from canonical customer insight snapshots, extra worker/API verification coverage, and an Admin UI customer details insights tab that now prefers the canonical customer insight summary instead of legacy behavioural insight rows.
- **AI Model Catalog Import**: Added a source-driven model catalog flow for Admin UI. Operators can now browse externally discovered model providers, preview all models under a selected model provider, and bulk-import them into the local AI provider/model catalog as inactive records for later review and activation.
- **Infrastructure (Dev Auto Deploy)**: Added automatic dev runtime delivery via GitHub Actions. Successful `CI` runs for pushes to `master` now build/push runtime images for the validated commit SHA and deploy the `dev` Azure Container Apps environment using the existing reusable image-release and runtime-deploy workflows.
- **AI Task Profiles + Tenant Overrides**: Added centralized non-agent LLM task profile resolution (`IAiTaskProfileResolver`), tenant-aware prompt/template overrides backed by `PromptSpec`, admin APIs for prompt templates and route policies, and Admin UI pages for managing prompt templates and AI routing configuration.

### Fixed
- **AI Observability (Trace Root Discovery)**: Improved the App Insights trace explorer so the root trace list now includes AI observations that sit under orchestration/request roots instead of only zero-parent AI spans, raised the trace explorer page-size ceiling so richer live traces can render deeper operation trees without truncation, and simplified the Admin UI traces screen back toward the starterkit layout by removing extra summary chrome that was not backed by additional live telemetry.
- **AI Observability (Trace Span Coverage)**: Restored rich App Insights trace detail coverage in the Admin UI trace explorer by keeping root-trace discovery AI-scoped while expanding selected-trace detail queries to include the full correlated `requests` and `dependencies` operation tree, normalizing App Insights span IDs so SQL/HTTP children attach correctly under AI root spans, and improving the trace waterfall labels so recovered request, HTTP, and DB spans render with clearer kinds and actors instead of anonymous generic spans.
- **Admin UI / Settings + AI Observability Traces**: Restored starterkit-style Settings parity in the Admin UI by wiring the missing authentication and audit routes into the shared settings shell, refreshing the Settings landing and detail pages to match the starterkit interaction pattern, and fixing the App Insights trace reader so root AI observations in `dev` are no longer filtered out by zero-parent span IDs or drowned out by unrelated generic request/dependency spans.
- **Worker / Quartz Persistence**: Fixed standalone worker startup and scheduled background-job persistence by adding the scheduled-job admin control-plane tables to the canonical `AonikDbContext` migration stream, provisioning the Quartz `QRTZ_*` schema through an idempotent infrastructure migration for local/runtime startup parity, and aligning `Aonik.Worker` with the same Development LocalDB connection fallback used by the API so the worker can bootstrap persistent Quartz scheduling outside AppHost.
- **Personal Finance (Transaction Category PATCH)**: Fixed manual transaction category overrides so `PATCH /personal-finance/transactions/{id}` now accepts the Payabo mobile category-only payload without requiring the full manual transaction body, preserves the existing transaction values, and clears stale subcategory state when the top-level category changes.
- **AI Playground / Speech Rendering (Currency Symbols + Duplicate Output)**: Fixed spoken-text normalization so currency symbols like `£250`, `₦5000`, `GH₵20`, `R30`, `KSh9`, `₹11`, and `¥13` are expanded to explicit spoken currency names before ElevenLabs synthesis, and fixed the Admin UI AI Playground output panel so frontend-tool reruns no longer duplicate the final assistant text when voice mode is off.
- **Text-to-Speech (Long Spoken Responses)**: Fixed the text-to-speech backend to synthesize long `speech.render` payloads without reintroducing hard trimming by splitting oversized utterances into bounded provider requests, preserving full spoken content while keeping tenant utterance policy enforcement per chunk.
- **Infrastructure / Canonical EF Migration Stream**: Stabilized the canonical `AonikDbContext` migration stream by mapping `PreRegistrationChallenge` into the canonical model, fixing pending customer insight self-referencing foreign keys to use SQL Server-safe `NO ACTION` behavior, updating the design-time context factory to honor connection-string environment overrides, and adding CI/release/deployment guards that fail when `dotnet ef migrations has-pending-model-changes` detects drift before migrations run.
- **Worker / Scheduled Jobs**: Fixed behavioural insight batch persistence by setting tenant context per user inside `BehaviouralInsightJob`, and added a corrective infrastructure migration to normalize scheduled-job control-plane audit actor columns back to `uniqueidentifier` so queued admin commands can be materialized and processed reliably.
- **Migrator / Agents Persistence**: Aligned `Aonik.Migrator` with the canonical `AonikDbContext` migration stream only, and fixed `AgentsDbContext` to use the existing `dbo.ConversationSummaries` table so `StaleSessionDetectorJob` no longer fails on a mismatched `AnkConversationSummaries` table name.
- **Infrastructure (SQL Server FQDN Double-Dot)**: Fixed malformed SQL Server hostname in Bicep-generated Key Vault connection string. `environment().suffixes.sqlServerHostname` already includes a leading dot (`.database.windows.net`), but `iac/azure/modules/data.bicep` line 49 added another dot, producing `aonik-dev-sql..database.windows.net`. This caused DNS resolution failures that blocked `AgentConfigurationSeedingService` (an `IHostedService` with EF Core retry) from completing `StartAsync`, preventing the Kestrel web server from ever starting. New container revisions went to `Degraded` state with "Deployment Progress Deadline Exceeded". Fixed by removing the extra dot separator.
- **Infrastructure (Bootstrap Enabled Flag)**: Added `Bootstrap__Enabled=true` environment variable to the API container when `BOOTSTRAP_SETUP_SECRET` is configured. Previously only `Bootstrap__SetupSecret` was set, so the API reported `bootstrapEnabled: false` and the setup wizard could not proceed. The env var is conditionally included alongside the secret in `iac/azure/stacks/aca/main.bicep`.
- **Infrastructure (Deploy Workflow Default Mode)**: Changed `cd-deploy.yml` default `mode` from `what-if` to `deploy` for both `workflow_dispatch` and `workflow_call` triggers. The previous default caused all deployments to silently run as dry-run previews unless the caller explicitly passed `-f mode=deploy`, leading to stale containers after CI published new images.
- **Infrastructure (Admin UI Nginx Reverse Proxy)**: Added custom nginx config with `/api` reverse proxy to the backend API and SPA fallback (`try_files`). The admin UI Docker image previously used the default nginx config with no proxy, causing all `/api/*` requests to return 404 and breaking bootstrap status checks and all API interactions in the deployed environment.

### Changed
- **Infrastructure (ACA Dev Scale-To-Zero)**: Updated the Azure Container Apps stack so all dev environment services, including the worker and Qdrant apps, can scale down to `minReplicas=0` to reduce idle environment cost while keeping staging/prod minimum replica behavior unchanged.
- **Bootstrap (First-Run Install Code Flow)**: Reworked initial platform bootstrap to use a one-time `Bootstrap:SetupSecret` plus owner email instead of requiring an IdP login before the first tenant exists. `/bootstrap/status` now reports explicit readiness states, the Admin UI no longer treats status failures as implicit fresh-install setup, bootstrap creates a pending owner profile for later identity linking, and first sign-in links that external identity back to the bootstrap owner record.
- **Infrastructure (Bootstrap Secret Delivery)**: Runtime deployment now passes the bootstrap install code through a dedicated GitHub environment secret (`BOOTSTRAP_SETUP_SECRET`) into ACA as `Bootstrap__SetupSecret`.
- **Infrastructure (CI/CD Simplification)**: Simplified the delivery model to infra bootstrap + CI image publishing + runtime deployment. `CI` now keeps PR feedback fast by running build/test only on pull requests while pushes to `master` also publish runtime images and release manifests, `CD: Deploy` remains the approval-gated rollout path, Azure tenant/subscription/client identifiers are sourced from repository variables, and the legacy `cd-dev-auto.yml` / `cd-pipeline.yml` workflows are retired.
- **Agent Framework (MAF Best Practices Refactor)**: Comprehensive refactoring of the AONIK Agent Framework to align with Microsoft Agent Framework (MAF) idioms and best practices:
  - **R1 — IDomainAgentDescriptor**: Replaced `AonikDomainAgent` base class with `IDomainAgentDescriptor` interface using `IEnumerable<IDomainAgentDescriptor>` multi-registration pattern in DI
  - **R2 — Native Session Management**: Rewrote `MasterOrchestratorService` to use MAF `AgentSession` via `agent.CreateSessionAsync(sessionId)` for native conversation history tracking, eliminating the `ConcurrentDictionary<string, List<ChatMessage>>` memory leak. Orchestrator agent is cached with double-checked locking via `SemaphoreSlim`
  - **R3 — ApprovalRequiredAIFunction**: Replaced custom `ProposalMiddleware` (`DelegatingChatClient`) with MAF's built-in `ApprovalRequiredAIFunction` for human-in-the-loop approval on 9 mutating tools (CreateInvoice, IssueInvoice, CancelInvoice, MarkInvoicePaid, CreatePaymentIntent, CapturePayment, CancelPayment, CreateLedger, CreateAccount)
  - **R4 — AuditMiddleware**: Moved `AuditMiddleware` from `Aonik.Agents` to `Aonik.Ai.Middleware`, integrated with `IAiRunWriter` for real audit records (`StartRunAsync`, `MarkRunCompletedAsync`/`MarkRunFailedAsync`), captures `response.Usage` token counts for cost tracking
  - **R7 — Agent Split**: Split Finance agent's 26+ tools into two sub-agents: `finance-agent` (~14 billing/ledger/payment tools) and `financial-life-graph-agent` (~17 FLG read tools) for better LLM tool selection
  - **R8 — MCP Integration**: Wired `McpToolProvider` into the master orchestrator's tool set alongside domain agent-as-tool functions; gracefully degrades if no MCP servers are configured
  - **R9 — IChatClient Pipeline**: Replaced `IChatClientFactory` + `ConfigDrivenChatClientFactory` with direct `IChatClient` registration using `.AsBuilder().Use(...)` pipeline pattern with `AuditMiddleware` inline
  - **R10 — Keyed Workflows**: Replaced switch statement in `RunWorkflowEndpoint` with keyed `IWorkflowFactory` services pattern; workflow classes converted to `IWorkflowFactory` implementations registered as keyed singletons
  - **R6 — Advisory Workflows**: Documented all three workflow classes (InvoiceProcessing, Onboarding, Reconciliation) as advisory-only (no tools wired for direct financial mutations)
  - Build: 0 errors, 0 warnings. Tests: 249 passing (10 SharedKernel + 173 Application + 22 Infrastructure + 54 Api)

### Changed
- **Database Naming Standard**: Standardized runtime table mapping to `dbo` with a unified table prefix (`Ank`) across `AonikDbContext` and module-scoped DbContexts. Added migration `20260301105723_StandardizeDboPrefixedTables` to move/rename existing tables (including `platform` schema fallback), and aligned API/Migrator to use `AonikDbContext` as the canonical migration stream.
- **Bootstrap + Setup Flow**: Simplified fresh-install initialization by aligning API and migrator startup behavior (shared migration ordering and seed parity, including global settings), enforcing one-time `/bootstrap` semantics, assigning `PlatformAdmin` for the initial bootstrap user, and updating setup/docs to prefer `Aonik.Migrator` as the primary migrations+seed entrypoint.
- **Architecture (PR 6.1 — Delete Legacy Layers)**: Completed Phase 6.1 modular clean-up. Seed services and seed data moved from Infrastructure to Platform (`Aonik.Platform.Services.Seeding`), composition roots (`Aonik.Api`, `Aonik.Migrator`) now seed via `PlatformDbContext` + `FinanceDbContext`, and legacy Infrastructure seed implementations were removed. Module boundary visibility was aligned (`InternalsVisibleTo`) for Platform/Finance composition roots. Build: 0 errors, 0 warnings. Tests: 106/106 passing.
- **Architecture (Phase 6 Complete)**: Completed modular monolith restructuring phases 0-6. Legacy `Domain` and `Domain.Tests` projects are removed, Platform/Finance/AI/Agents modules own vertical slices, module-scoped DbContexts are active, and endpoint/contracts migration to modules is complete.
- **Architecture (PR 2.5 — Finance Module Clean-Up & Integration Events)**: Completed Phase 2 Finance module extraction. Removed 39 Finance DbSet properties from `IAonikDbContext` and `AonikDbContext`, completing the separation of Finance data access. Moved `PopulateOrderCompatibilityColumns` (Order shadow property population) to `FinanceDbContext.OnBeforeSave`. Fixed `DemoSeedService` to use `FinanceDbContext` for Finance entities and `IAonikDbContext` for non-Finance entities (PersonalFinance). Fixed `InvoiceInsightWorkflow` to use `IBillingService.GetInvoiceAsync` instead of direct DB access. Defined integration event types in `SharedKernel/Events/Integration/` — Platform events (`TenantProvisionedEvent`, `PartyCreatedEvent`, `PartyUpdatedEvent`, `UserPermissionsChangedEvent`) and Finance events (`OrderCreatedEvent`, `OrderStatusChangedEvent`, `PaymentCompletedEvent`, `InvoiceIssuedEvent`, `InvoicePaidEvent`, `JournalEntryPostedEvent`). Updated API test files to use `FinanceDbContext`/`PlatformDbContext` for data verification. Build: 0 errors, 0 warnings. Tests: 107/107 passing.
- **Architecture (PR 2.4 — Move Billing, Orders, Pricing, Partners to Finance)**: Moved all four remaining Finance sub-domains into `Aonik.Finance`. This includes 30 entity files, 9 EF configurations, 8 DTO files, 8 service interfaces, 9 service implementations, 4 API contract files, and 24 endpoint files. Created `ITenantCurrencyProvider` cross-module interface in SharedKernel with Platform implementation to decouple Finance from Platform DbContext. Moved cross-cutting types (`PagedResult<T>`, `IAuditLogWriter`, `AuditEventNames`, `IPartyService`, `IComplianceService` + DTOs) to SharedKernel to eliminate Finance→Platform circular dependency. Created `PartyReadModel` as temporary read-only projection in Finance for cross-module Party queries. Updated DI registrations in `FinanceModule`. Build: 0 errors, 0 warnings. Tests: 107/107 passing.
- **Architecture (Phase 1 Complete)**: Completed extraction of the Platform module (`Aonik.Platform`). All Platform domain entities (Identity, Party, Compliance, Notifications, Operations, Settings, Features, ReferenceData), 49 service interfaces, 28 service implementations, 38 FastEndpoints, 16 API contract files, and 7 settings constants files now live in `Aonik.Platform` with `PlatformDbContext` as the module-scoped DbContext. Phase 1 spans PRs 1.1–1.5 (commits `028fae7`–`07abb4c`). Build: 0 errors, 0 warnings. Tests: 107/107 passing.

### Added
- **AI Observability (Langfuse Sessions & User Attribution)**: Added session and user identity propagation to all AI/Agent OTel traces for Langfuse grouping. Custom `BaggageSpanProcessor` in `ServiceDefaults` copies `langfuse.session.id` and `langfuse.user.id` from OTel baggage into span tags on every span start, ensuring MEAI and MAF library spans inherit session context without modification. Session/user baggage set at both entry points: `MasterOrchestratorService.ChatAsync()` (REST via `ChatRequest.SessionId`) and `AguiStreamingEndpoint` (AG-UI via `AguiRunInput.ThreadId`). User ID resolved from `ICurrentUserProvider`. Constants in `AiTelemetry`. Build: 0 errors. Tests: 270/270 passing.
- **AI Observability (OpenTelemetry + Langfuse)**: Added end-to-end OpenTelemetry instrumentation for the AI and Agent subsystems following GenAI semantic conventions. IChatClient pipeline instrumented via `.UseOpenTelemetry()` in `AiModule`, master orchestrator and all domain agents wrapped centrally in `MasterOrchestratorService`. Shared constants in `AiTelemetry` (`Aonik.SharedKernel`). Dual OTLP exporters in `ServiceDefaults`: Aspire dashboard (signal-specific) + Langfuse Cloud (HTTP/protobuf with Basic Auth). Sensitive data (prompts, responses, tool args) controlled by `AI:OpenTelemetry:EnableSensitiveData` — disabled by default, enabled in Development. Backend is swappable to any OTLP HTTP endpoint (Honeycomb, Grafana Tempo, Jaeger, Datadog, etc.). Build: 0 errors. Tests: 270/270 passing.
- **Personal Finance (Financial Life Graph)**: Added a tenant-scoped Financial Life Graph foundation for Payabo-style personal finance reasoning, including graph read APIs, native graph annotations, shared-cache-backed graph hydration, inferred annotation proposal flows with `AiRunId`, Finance agent/MCP graph tools, focused application/API coverage for graph reads, writes, validation, cache invalidation, and approval workflows, plus bounded recent-history transaction projection with load-volume logging to avoid full-history graph hydration on cache misses.
- **Payabo Mobile**: Extended the new Spend -> Accounts hub with linked-account actions for reconnect, refresh, and disconnect, plus persisted OAuth-resume handling and a configurable Plaid native launcher path with safe fallback.
- **Payabo Mobile**: Replaced the placeholder Android app identifier with `com.payabo.mobile`, aligned the native Plaid launcher to Android package-name registration, and deferred iOS OAuth setup until mobile iOS support is in scope.
- **Personal Finance (Linked Accounts)**: Added provider-agnostic account-link backend foundations for session creation, temporary-code exchange, linked-connection listing, and Spend summary projection, with a simulated Plaid adapter and persistence for financial connections, sessions, and linked accounts.
- **Personal Finance (Linked Accounts)**: Added reconnect-targeted session support plus provider-neutral refresh and disconnect endpoints so linked accounts can be restored, re-synced, or removed from active Spend views without breaking the abstraction layer.
- **Personal Finance (Plaid Adapter)**: Added a real Plaid backend adapter path for Android-native Link, including `/link/token/create`, public-token exchange, account/item fetches for linked-account projection, refresh support, and Item removal on disconnect when `Finance:PersonalFinance:Plaid:UseRealPlaidApi=true` is configured.
- **Personal Finance (Plaid Webhooks)**: Added Plaid webhook ingestion for linked-account connections, persisting webhook events and propagating action-required, disconnect, and sync-available states into linked connections and Spend-facing account summaries.
- **Personal Finance (Transaction Sync)**: Added on-demand linked-account transaction sync, using Plaid `transactions/sync` to upsert and remove Personal Finance projection transactions with provider provenance, dedupe, and stored sync cursors.
- **Personal Finance (Recurring Sync)**: Added recurring linked-account sync scheduling metadata, a Worker-hosted recurring sync loop for due connections, and webhook-driven transaction sync orchestration when Plaid reports transaction updates.
- **Personal Finance (Payabo)**: Added Phase 1-2 backend foundation for transaction classification and insights workflows. This includes personal account and manual personal transaction APIs/services, statement import APIs/services (upload/list/get/rows/apply), new statement import domain entities (`StatementImport`, `StatementImportRow`), expanded personal transaction/account fields for classification provenance, and focused application tests for personal finance account/transaction/import services.
- **Documentation (PR 6.2)**: Added ADR-005 documenting the adopted module-first modular monolith architecture and updated architecture docs/checklists to reflect restructuring completion.
- **Infrastructure (Runtime Config Completeness)**: Closed variable/secret gaps between `appsettings.json` and deployed containers by adding Key Vault secrets for `ACS_CONNECTION_STRING` and `VERIFICATION_HASH_KEY`, and environment variable mappings for Settings (IdP Management API, 11 keys), Communication (2 keys), Bootstrap (1 key), and Feature Management (6 flags) across Bicep modules, deployment workflows, drift detection, and all documentation.
- **Infrastructure (Runtime Config Overrides)**: Added optional runtime app-settings injection for deployment workflows via explicit GitHub environment variables for key API/Worker settings (auth, platform-admin, blob storage, Plaid), allowing per-environment API/Worker configuration overrides without rebuilding images.
- **Infrastructure (Azure CD Orchestrator)**: Added `cd-pipeline.yml` as an operator orchestration workflow that conditionally runs image release and then runtime deploy; when `build_images=true` it auto-propagates the resolved release version, and when `build_images=false` it requires explicit `image_version`. Also enabled reusable `workflow_call` interfaces for `cd-images.yml` and `cd-deploy.yml`.
- **Infrastructure (Azure CD)**: Added separated Azure workflows for platform bootstrap (`cd-infra.yml`), image build/tag/push (`cd-images.yml`), runtime rollout (`cd-deploy.yml`), plus workflow linting (`lint.yml`) to enforce CI validation of GitHub workflow syntax.
- **Deployment Runbooks**: Added concise operator runbooks under `docs/runbooks/` for bootstrap, build-and-push, and runtime deployment execution.
- **Infrastructure (Azure IaC CD)**: Added a GitHub Actions workflow (`azure-iac-cd.yml`) to run Azure IaC `what-if` previews and deployments for both ACA and App Service profiles using OIDC and environment-scoped secrets.
- **Infrastructure (Azure IaC)**: Added a Bicep-based Azure Infrastructure as Code baseline under `iac/azure/` with an ACA-first profile (`Aonik.Api` + `Aonik.Worker` on Azure Container Apps), an App Service fallback profile, reusable shared/data modules, and environment parameter templates for `dev`, `staging`, and `prod`.
- **Infrastructure (Azure IaC/CD)**: Expanded both ACA and App Service deployment profiles to also deploy `Aonik.AdminUi`, including new `adminUiImage` parameters, runtime resources, and environment templates for `dev`, `staging`, and `prod`.
- **Deployment Docs**: Updated Azure deployment guidance and added an Azure IaC roadmap document covering current implementation and hardening phases.
- **Deployment Docs**: Added a step-by-step GitHub Actions Azure deployment runbook covering OIDC setup, GitHub environments/secrets, workflow inputs, what-if preview, deploy execution, and post-deploy validation.
- **Admin UI + API**: Added cache management tooling under System Tools with cache-set overview and invalidate actions, backed by new admin cache endpoints and infrastructure cache management service.
- **Payabo Web**: Completed Phase 2 authenticated account integration by switching to backend token + userinfo login bootstrap, adding registration API wiring, and enforcing auth guard loading/session checks.
- **Payabo Web**: Added live profile management pages for personal details, email updates, password updates, and photo upload/delete backed by customer profile endpoints.
- **Payabo Web**: Added `Payabo/AGENTS.md` guidance for LLM/browser automation to run authenticated Playwright flows, including shared test login steps and environment prerequisites.

### Fixed
- **Infrastructure (Azure Image Release)**: Fixed Admin UI provider fallback mismatch by ensuring build-time `VITE_AUTH_PROVIDER` defaults to `azure-ad` when unset, aligning docker build args with validation behavior.
- **Infrastructure (Azure CD Orchestrator)**: Reduced `cd-pipeline.yml` `workflow_dispatch` inputs to 10 (GitHub platform limit) and documented that advanced override knobs remain available via the underlying image-release/runtime-deploy workflows.
- **Infrastructure (Azure CD Orchestrator)**: Fixed reusable workflow output wiring by declaring `cd-images.yml` outputs under `on.workflow_call.outputs`, ensuring `cd-pipeline.yml` receives `release_version` and `acr_login_server` when `build_images=true`.
- **Infrastructure (Docker Images)**: Updated API and Worker runtime Dockerfiles to run with the pre-defined non-root `APP_UID` user from .NET 10 base images instead of invoking `adduser`, fixing Linux image builds that failed with `/bin/sh: adduser: not found` on the hosted GitHub runner.
- **Infrastructure (Azure Runtime Deploy)**: Classified ACR query failures separately from true image-not-found results in `cd-deploy.yml`, so auth/transport errors no longer appear as misleading missing-tag failures.
- **Infrastructure (Platform Bootstrap)**: Replaced single `bootstrap_image` override with service-specific bootstrap image inputs (`bootstrap_api_image`, `bootstrap_worker_image`, `bootstrap_adminui_image`) so ACA bootstrap honors API/Admin UI port assumptions and avoids first-run false starts.
- **Infrastructure (Platform Bootstrap)**: Added `bootstrap_adminui_target_port` and changed the default admin UI bootstrap image to `mcr.microsoft.com/dotnet/samples:aspnetapp`, then threaded `adminUiTargetPort` through ACA profile parameters so bootstrap deployments avoid admin UI revision timeouts caused by image/port mismatches.
- **Infrastructure (Workflow Lint)**: Fixed additional ShellCheck SC2129 in `cd-images.yml` metadata output export by grouping `GITHUB_OUTPUT` writes under one redirect block.
- **Infrastructure (Workflow Lint)**: Fixed ShellCheck SC2129 in `cd-images.yml` by grouping `image-release.env` writes under a single redirection to satisfy `actionlint` shell checks.
- **Infrastructure (Azure IaC/CD)**: Updated `azure-iac-cd.yml` deploy-mode safeguards to always validate `apiImage`, `workerImage`, and `adminUiImage` tags from effective parameters before `az deployment group create`, so deployments fail fast when default environment tags (for example `:dev`) are missing in ACR.
- **Infrastructure (Azure Runtime Deploy)**: Fixed `cd-deploy.yml` ACR validation to honor `acr_login_server` overrides when resolving the registry name used by `az acr repository show`, preventing false missing-tag failures for non-derived registries.
- **Infrastructure (Azure IaC/CD)**: Prevented late-stage ACA deployment failures caused by missing container tags by adding pre-deploy ACR image existence validation in `azure-iac-cd.yml`; deploy mode now fails fast with actionable errors before `az deployment group create`.
- **Infrastructure (Azure IaC / ACA)**: Removed current Bicep compile warnings by replacing `listKeys(...)` with resource symbol usage, removing unsupported ACR policy properties, using cloud-aware SQL host suffixes, and applying null-safe outputs in shared modules; also reduced first-revision ACA provisioning races by introducing dedicated user-assigned ACR pull identities with explicit role-assignment ordering for API/Worker/Admin UI.

- **Infrastructure (Azure IaC/CD)**: Hardened deployments by keeping ACR retention policy disabled unless `Premium` is selected, removing unsupported ACR policy fields that trigger Bicep type warnings, and adding workflow validation that fails fast when environment parameter files still contain `REPLACE_WITH_*` placeholders (such as container image references).
- **Infrastructure (Azure IaC)**: Fixed Azure Container Registry policy configuration for `Basic`/`Standard` SKUs by disabling retention policy unless `Premium` is selected and aligning `azureADAuthenticationAsArmPolicy` casing with Azure resource schema, preventing false `SkuNotSupported` deployment failures on non-Premium tiers.

- **Infrastructure**: Removed the invalid FusionCache DI package reference (`ZiggyCreatures.FusionCache.Microsoft.Extensions.DependencyInjection`) and rely on `ZiggyCreatures.FusionCache`, which already provides `AddFusionCache()`.
- **Payabo Web**: Removed calls to non-existent `/public/payments/instruments` and now resolves saved payment instruments from local persisted/seeded data until a real endpoint is introduced.
- **Payabo Web**: Prevented stale cached `paymentIntentId` reuse on provider-return flows by only reusing cached IDs when callback context matches the saved provider reference.
- **Payabo Web**: Prioritized query-level cancellation (`result=cancelled`) as failed in status reconciliation so immediate cancel returns do not show as pending while provider intent remains pending.
- **Payabo Web**: Updated payment status reconciliation to prioritize backend payment/order failure states over query params so failed/cancelled intents never render as success.
- **Payabo Payments**: Fixed Stripe simulated checkout URL generation to append provider query parameters with `&` when return URLs already include query strings (prevents malformed `result` values on cancel redirects).
- **API Tests**: Fixed test database isolation issue
  - Each `CustomWebApplicationFactory` instance now uses a consistent database name across all requests
  - Previously, each DbContext registration created a new unique database, causing resources created in one request to be invisible in subsequent requests
  - Fixed storage file locking issue by using unique storage paths per test factory instance
  - 21 of 22 API integration tests now passing (up from 10)
  - Remaining failure is due to missing Azure Communication Services configuration in test environment (expected)

- **API Tests**: Fixed database context registration in test environment
  - Added `IAonikDbContext` registration to `CustomWebApplicationFactory`
  - Tests now properly resolve database dependencies

### Changed
- **Deployment Documentation**: Refactored Azure deployment guidance to document the new bootstrap -> image release -> runtime deploy architecture, first-run flow, rollback, troubleshooting, and migration from the legacy single workflow path.
- **Containerization**: Added `docker/adminui.Dockerfile` and updated Docker documentation to include Admin UI image build support.
- **Infrastructure (Azure IaC/CD)**: Added optional `image_tag` workflow input to override all service image tags (`apiImage`, `workerImage`, `adminUiImage`) per run while keeping existing ACR host substitution behavior.
- **Infrastructure (Azure IaC)**: Switched Azure Container Registry SKU default from `Standard` to `Basic` in the shared module to improve compatibility in constrained subscriptions/regions.
- **Infrastructure (Azure IaC CD)**: Updated Azure IaC deployment workflow to support optional `AZURE_CLIENT_SECRET` authentication fallback while preserving OIDC as the default path; refreshed deployment docs to describe both auth modes.
- **Deployment**: Implemented first-class containerisation assets with multi-stage Dockerfiles for `Aonik.Api` and `Aonik.Worker`, a Docker Compose stack (`sql` + `api` + `worker`), and updated deployment guidance for local and production container workflows.
- **Infrastructure**: Introduced FusionCache-based caching with standardized short/medium/long cache policies, migrated settings/reference-data caching to the shared cache store, and added event-driven cache invalidation for automatic cache set expiry on writes.
- **Engineering**: Added a tag-driven GitHub `Release` workflow that builds/tests the solution, publishes API artifacts, and creates GitHub Releases automatically for `v*` tags.
- **Documentation**: Added a GitHub release runbook with prerequisites, workflow behavior, and release commands.
- **Engineering**: Added a GitHub Actions `.NET CI` workflow that restores and builds `Aonik.sln` on pull requests targeting `main` and on pushes to `main`.
- **Database Configuration**: Removed InMemory database option for Development environment
  - `UseInMemoryDatabase` configuration setting removed from `appsettings.Development.json`
  - Application now uses SQL Server for all non-test environments
  - InMemory database still used for automated tests
  - Added `dbContext.Database.MigrateAsync()` to Program.cs for automatic migrations on startup in Development
  - Updated DependencyInjection.cs to remove InMemory configuration logic
  - Updated CustomWebApplicationFactory to explicitly use InMemory for tests

### Added
- **Payabo Web**: Implemented phase 1 payment status flow with provider return handling, public payment intent status lookup endpoint, and live status/confirmation screens backed by order and payment state.
- **Documentation**: Added `docs/Payabo-MVP-Next-Steps.md` with a prioritized implementation plan to move Payabo from prototype state to a working MVP.
- **Personal Finance**: Added household creation and member invitation endpoints with service support.
- **Party Relationships**: Added relationship type catalog constants and a party endpoint to create an individual related party (e.g., friend) linked to an existing customer.
- **Autonumbering**: Added autonumbering profiles, reservations, and service models with tests for sequencing and reset behavior.
- **Autonumbering**: Added documentation covering performance considerations and reservation table usage guidance.
- **Documentation**: Added a flexible document and file model proposal for multi-purpose evidence and verification workflows.
- **Personal Finance**: Added personal accounts for imported finance sources and account-level transaction grouping.
- **Compliance**: Added document evidence entities, services, and API endpoints with storage-ready metadata fields.
- **Compliance**: Added document listing support plus Admin UI document management pages with create/detail flows.
- **Notifications**: Added notification template entities, bindings, and Scriban-based rendering service for multi-tenant shared templates.
- **Admin UI**: Added an autonumbering settings page with configuration overview and test preview.
- **Ledger**: Added tenant ledger, account, and journal entry API endpoints plus Admin UI pages for creating ledgers, accounts, and transactions.
- **Compliance**: Added related-entity document filtering and ledger-facing document upload workflows in the Admin UI.
- **Admin UI**: Role display in sidebar user profile
  - Added `identityService.getUserInfo()` to fetch user roles from `/identity/userinfo` endpoint
  - Added `formatRoleLabel()` helper to convert role names to Title Case
  - Role fetching with loading state and error handling
  - Displays user's role(s) in bottom-left sidebar profile menu
- **Admin UI**: FX Rate management page and navigation entry
  - Added a dedicated FX Rates settings page with rate sources, spread policies, and refresh cadence overview
- **Payabo Web**: Added a new root-level `Payabo/` React app scaffold with routing, layouts, and asset imports for the Payabo migration.
  - Linked the page in Settings navigation for quick access
- **Payabo Web**: Implemented the Payabo dashboard layout with upcoming bills, bill payment tabs, transactions, budgets, and organizations sections.
- **Pricing**: Added FX management tables for rate sources, spread policies, and refresh schedules.

### Added - 2026-01-17
- Added customer profile endpoints for read/update/email/password/photo flows with profile storage support and IdP account updates.

### Added - 2026-01-12
- Added reference data entities, service, and endpoint for global/tenant lookup values.

### Added - 2025-03-13
- Added onboarding verification flow tests covering service start/confirm paths, rate limiting, policy gates, and API endpoints.

### Changed - 2025-03-12
- Standardized audit event names, passed tenant/actor/correlation IDs explicitly, and masked PII fields before audit logging.
- Added audit log verification coverage for user provisioning and verification workflows.

### Added - 2025-03-11
- Added identity and onboarding endpoints for current-user profile, verification flows, and onboarding snapshots.
- Added customer profile application models and user profile service with audit logging for profile updates.

### Added - 2025-03-10
- Added messaging abstractions with Azure Communication email/SMS senders and configuration bindings.
- Added identity verification service for email/phone challenges with hashing, TTL enforcement, rate limiting, and audit logging.

### Added - 2025-03-09
- Added verification challenge domain model and EF Core configuration with supporting enums for identity verification flows.
- Added correlation IDs to audit logs and captured them from HTTP request context.
- Added audit log emission for JIT user auto-provisioning.

### Added - 2025-03-08
- Added tenant admin and operations authorization policies that accept role or permission checks.
- Added user role service plus tenant-scoped endpoints for role assignment and retrieval.
- Documented policy conventions in the permissions reference.
- Added a dev bootstrap flow to create the first tenant and assign the current user the TenantAdmin role.

### Added - 2025-03-07
- Added `ICurrentUserContext` and `HttpContextCurrentUserContext` for unified current-user data, plus claim-to-role mapping helper.

### Changed - 2025-03-07
- Updated authentication token validation to populate current-user context and resolve roles from claims or the database.

### Added - 2025-02-14
- Added scoped tenant context (`ITenantContext`/`TenantContext`) and tenant context middleware to centralize tenant resolution.

### Changed - 2025-02-14
- Updated tenant validation and tenant provider to consume `ITenantContext` instead of raw `HttpContext.Items`.
- Allowed `X-Tenant-Id` routing in any environment when explicitly configured via `Auth:TenantRouting=Header`.

### Fixed - 2025-01-08

#### Build System
- Fixed NuGet package version conflicts in `Aonik.Infrastructure.csproj`
  - Updated `Microsoft.Extensions.DependencyInjection.Abstractions` from 9.0.0 to 10.0.1 to match .NET 10 dependencies
  - Changed from `Microsoft.AspNetCore.Http.Abstractions` to `Microsoft.AspNetCore.Http` version 2.2.0 to resolve `AddHttpContextAccessor` dependency

#### Entity Framework Configurations
- **LedgerAccountConfiguration**: Fixed property mappings to match actual `LedgerAccount` entity
  - Removed non-existent `Currency` and `CreatedUtc` properties
  - Added correct properties: `Code`, `AccountType`
  
- **PaymentIntentConfiguration**: Updated to match current `PaymentIntent` entity structure
  - Removed `Reference` and `CreatedUtc` properties that don't exist on entity
  - Added correct property configurations for `PurposeType`, `PaymentMethodType`
  
- **InvoiceConfiguration**: Aligned with actual `Invoice` entity properties
  - Replaced `CustomerId` with `CustomerAccountId`
  - Replaced `InvoiceNumber` with proper date-based properties (`IssueDate`, `DueDate`)
  - Changed `TotalAmount` to `Total`, added `Subtotal`, `TaxTotal`, `DiscountTotal`
  - Updated collection mapping from `LineItems` to `Lines`
  
- **JournalEntryConfiguration**: Corrected to match `JournalEntry` entity structure
  - Removed non-existent properties (`AccountId`, `Amount`, `Currency`, `EntryUtc`, `Reference`, `Description`)
  - Added correct properties: `LedgerId`, `Timestamp`, `SourceType`, `SourceId`, `Status`
  - Added relationship mapping for `Lines` collection

#### Test Infrastructure
- Removed outdated domain tests that tested rich domain behavior not present in anemic entity model:
  - `tests/Aonik.Domain.Tests/Billing/InvoiceTests.cs` (deleted)
  - `tests/Aonik.Domain.Tests/Payments/PaymentIntentTests.cs` (deleted)

- Fixed application layer tests to include required dependencies:
  - Added `TestTenantProvider` mock implementation to `BillingServiceTests`, `PaymentServiceTests`, and `LedgerServiceTests`
  - Updated all service instantiations to include `ITenantProvider` parameter
  - Fixed test assertions to reference correct entity properties (`SourceId` instead of `AccountId` in JournalEntry tests)
  - Updated `PaymentServiceTests` to set status directly on anemic entities instead of calling non-existent behavior methods

### Current Status
- ✅ **Build Status**: All projects compile successfully with 0 errors and 0 warnings
- ⚠️ **Test Status**: Some integration and API tests still failing (separate from build errors)
- 📦 **Dependencies**: All NuGet packages resolved correctly for .NET 10

### Known Issues
- Some application and API layer tests fail due to:
  - Services returning incomplete data (e.g., empty `InvoiceNumber`, "N/A" for `Currency`)
  - Tenant context issues in API integration tests
  - These are functional test issues, not build errors

### Notes
This update focused on resolving all compilation errors and making the solution buildable. The codebase follows an **anemic domain model** pattern where:
- Domain entities are simple data containers with no business logic
- All business logic resides in application layer services
- Tests should focus on service behavior rather than entity behavior

---

## Project Structure

```
aonik/
├── src/
│   ├── Aonik.SharedKernel/      # Cross-cutting primitives, interfaces, events
│   ├── Aonik.Platform/          # Identity, tenancy, party/profile, compliance
│   ├── Aonik.Finance/           # Ledger, payments, orders, billing, pricing
│   ├── Aonik.Ai/                # AI routing, prompts, execution records
│   ├── Aonik.Agents/            # Domain agents, orchestration, proposals
│   ├── Aonik.Application/       # Shared application services
│   ├── Aonik.Infrastructure/    # EF migrations, external adapters
│   ├── Aonik.Api/               # FastEndpoints HTTP API
│   ├── Aonik.Worker/            # Background jobs (Quartz)
│   ├── Aonik.Migrator/          # Database migration host
│   ├── Aonik.AppHost/           # .NET Aspire orchestration
│   ├── Aonik.AdminUi/           # Admin interface (React 19)
│   ├── Aonik.Finance.Mcp/       # Finance MCP server
│   └── Aonik.Platform.Mcp/      # Platform MCP server
├── tests/
│   ├── Aonik.SharedKernel.Tests/
│   ├── Aonik.Application.Tests/
│   ├── Aonik.Infrastructure.Tests/
│   └── Aonik.Api.Tests/
├── AGENTS.md                     # Coding standards for AI agents
├── CHANGELOG.md                  # This file
└── README.md                     # Project overview
```

---

## Contributing

When contributing to this project:
1. Ensure `dotnet build Aonik.sln` succeeds with 0 errors
2. Run `dotnet test` to verify tests pass
3. Follow the coding standards in `AGENTS.md`
4. Update this CHANGELOG with your changes
5. Update relevant documentation
