# Aonik Codebase Analysis Report

> Report ID: report-01
> Generated: 2026-05-05
> Scope: `C:\Users\mjosi\source\repos\aonik` (master branch)
> Reviewer brief: senior architect / security reviewer / performance engineer / SOC 2 readiness assessor

---

## 1. Executive Summary

Aonik is a **modular monolith** built on .NET 10, organised into four domain modules (Platform, Finance, Ai, Agents) on top of a SharedKernel and an Infrastructure composition root. The platform is conceptually well-designed — anemic-entity / service-layer logic, FastEndpoints vertical slices, a single canonical migration stream against `AonikDbContext`, OpenTelemetry baked in via Aspire, audit fields on all entities, soft-delete handled in the SaveChanges override, and tenant-write enforcement at the DbContext layer.

However, **the codebase is several careful months away from SOC 2 readiness, production reliability at scale, or comfortable cross-team contribution**. The big themes:

1. **Secrets management is broken.** Real production-grade keys for OpenAI, Auth0 Management API, Plaid, and Langfuse are present in [src/Aonik.Api/appsettings.Development.json](src/Aonik.Api/appsettings.Development.json), and that file existed in git history (now `.gitignore`d). Until the keys are rotated and history is rewritten, every developer with clone access has a usable foothold.
2. **Authorisation is enforced in the service layer via thrown exceptions and pattern-matched on the message string in `Program.cs`.** `Program.cs` lines 302–310 detect "Permission X is required." by `StartsWith` / `EndsWith` to map to 403 — a fragile, easily-broken control surface.
3. **Tenant isolation has multiple soft spots.** Anonymous endpoints accept the tenant id from an `X-Tenant-Id` HTTP header with no binding to user identity ([TenantContextMiddleware.cs:90](src/Aonik.Api/Middleware/TenantContextMiddleware.cs:90)). Sixteen call sites bypass the global tenant filter via `IgnoreQueryFilters()`. The `ApplyTenantQueryFilters` rule "no tenant context → show all rows" ([AonikDbContextBase.cs:100](src/Aonik.SharedKernel/Persistence/AonikDbContextBase.cs:100)) is a footgun for any code path where the provider is null.
4. **Input validation is effectively absent.** No FluentValidation / FastEndpoints validators are wired up across `src/`. Every endpoint depends on EF constraints, manual null checks, or service-layer assertions.
5. **The "Application" layer is hollow.** [src/Aonik.Application/DependencyInjection.cs](src/Aonik.Application/DependencyInjection.cs) is `return services;`. The `IAonikDbContext` abstraction inside the same project imports concrete entity types from Platform and Finance, breaking the inversion it pretends to provide.
6. **Three sync-over-async hot spots** (`.GetAwaiter().GetResult()` / `.Result` after `Task.WhenAll`) in DI, observability, and dashboard aggregation.
7. **Two endpoint files / services well over 1,000 lines** (CustomerInsightSnapshotGenerator.cs at 2,331; AccessManagementService.cs at 1,364; AguiStreamingEndpoint.cs at 853) and a 1,913-line seed file — concentrated complexity that will resist parallel work and review.
8. **Top-down test coverage is uneven.** Multi-tenancy isolation has explicit positive tests (good); but `RoleService`, `WebhookService`, `AiRunService`, agent tool execution, and most AI-side services have no unit tests.

### Top 5 Remediation Priorities

| # | Action | Why it matters |
|---|---|---|
| 1 | **Rotate every secret in [appsettings.Development.json](src/Aonik.Api/appsettings.Development.json) and rewrite git history** | Real OpenAI / Auth0 / Plaid / Langfuse keys are in repo history; until rotated they are reusable by anyone who has ever cloned the repo. |
| 2 | **Convert authorisation from "service throws → middleware regex matches the message" to declarative endpoint-level permission requirements** | Today, forgetting to call `EnsurePermissionAsync` silently disables auth on an endpoint, and changing the exception message breaks the 403 mapping in `Program.cs`. |
| 3 | **Bind the tenant id to the authenticated principal and remove the unauthenticated `X-Tenant-Id` accept-anything path for protected routes** | Today, an unauthenticated client can supply any tenant guid for any public endpoint; combined with 16 `IgnoreQueryFilters` call sites, the blast radius is non-trivial. |
| 4 | **Introduce FastEndpoints / FluentValidation validators on every request DTO and turn on validation-by-default** | Input validation is the cheapest depth-of-defense layer and is almost entirely absent. |
| 5 | **Replace the ~150-line custom CORS / exception middleware in Program.cs with first-class ASP.NET Core middleware and a problem-details mapper, then break Program.cs into composition extensions** | The current shape mixes CORS, error formatting, environment-specific PII leakage, and 100+ lines of role→permission hard-coding into the entry point — each of those is a separate concern with its own test surface. |

---

## 2. Scope of Analysis

### Reviewed

- The .NET solution `Aonik.sln` and every C# project under `src/` and `tests/`.
- API entry point ([src/Aonik.Api/Program.cs](src/Aonik.Api/Program.cs), 760 lines) and middleware pipeline.
- Application & Infrastructure DI roots ([src/Aonik.Application/DependencyInjection.cs](src/Aonik.Application/DependencyInjection.cs), [src/Aonik.Infrastructure/DependencyInjection.cs](src/Aonik.Infrastructure/DependencyInjection.cs)).
- Domain modules ([src/Aonik.Platform/PlatformModule.cs](src/Aonik.Platform/PlatformModule.cs), [src/Aonik.Finance/FinanceModule.cs](src/Aonik.Finance/FinanceModule.cs), [src/Aonik.Ai/AiModule.cs](src/Aonik.Ai/AiModule.cs), [src/Aonik.Agents/AgentsModule.cs](src/Aonik.Agents/AgentsModule.cs)).
- DbContext hierarchy and tenant/audit/soft-delete behaviour ([src/Aonik.SharedKernel/Persistence/AonikDbContextBase.cs](src/Aonik.SharedKernel/Persistence/AonikDbContextBase.cs)).
- Authentication and authorisation ([src/Aonik.Infrastructure/Authentication/](src/Aonik.Infrastructure/Authentication/), [src/Aonik.Infrastructure/Authorization/](src/Aonik.Infrastructure/Authorization/)).
- Multi-tenancy ([src/Aonik.Infrastructure/Multitenancy/](src/Aonik.Infrastructure/Multitenancy/), [src/Aonik.Api/Middleware/TenantContextMiddleware.cs](src/Aonik.Api/Middleware/TenantContextMiddleware.cs)).
- Persistence patterns and EF Core usage across module services.
- Observability via [src/Aonik.ServiceDefaults/](src/Aonik.ServiceDefaults/) and [src/Aonik.Ai/Observability/](src/Aonik.Ai/Observability/).
- Worker / background jobs ([src/Aonik.Worker/Jobs/](src/Aonik.Worker/Jobs/)).
- Test projects under `tests/` (5 projects, ≈143 test files).
- `appsettings.json` and `appsettings.Development.json` for the API.
- `.gitignore` and git history of suspect files.

### Not reviewed in depth

- Front-end (`src/Aonik.AdminUi`, `apps/Payabo`, `Templates/`) beyond surface checks; the request was for the .NET / financial platform layer.
- Aspire infrastructure-as-code (`iac/`, Docker compose) beyond noting they exist.
- Markdown documentation under `docs/`, except the existing `CLAUDE.md` and `AGENTS.md`.
- The `Aonik.AdminDesktop`, `Aonik.Cli`, `Aonik.Migrator`, and `*.Mcp` projects beyond their `csproj` reference graph.
- EF migration content (50 migration files) — only the structural fact that they all live under `src/Aonik.Infrastructure/Migrations/`.

### Limitations & Assumptions

- Findings about runtime behaviour (latency, memory, throughput) are inferred from code, not measured. They are flagged as such.
- Test "coverage" is measured by file presence (`<Service>Tests.cs`) — not by actual line/branch coverage. Real coverage may be higher or lower.
- Some agents I dispatched returned summaries that I could not 100% verify; where I verified independently, the report is marked "verified". Where the claim is plausible but unverified, it's marked "speculative".
- I did not run `dotnet build` or any tests to ground-truth the analysis — the report is purely static.

---

## 3. Architecture Overview

### Solution Structure

The solution declares the following first-party projects (excluding the Admin UI and packages):

| Project | Type | Purpose |
|---|---|---|
| `Aonik.SharedKernel` | Library | Cross-cutting primitives: `Entity`, `AuditableEntity`, `ITenantScoped`, `ITenantProvider`, `ICurrentUserProvider`, `IClock`, integration events, `AonikDbContextBase`. |
| `Aonik.Application` | Library | **Currently a façade.** Single empty `AddApplication()` extension; only artefact of value is `IAonikDbContext` (which itself imports module entities). |
| `Aonik.Infrastructure` | Library | DI composition root. References every module. Owns the canonical `AonikDbContext` and all 50 migrations. ≈141 C# files. |
| `Aonik.Platform` | Library | Identity, tenancy, party/profile, compliance, notifications, settings, autonumbering, reference data, seeding. ≈493 C# files. |
| `Aonik.Finance` | Library | Ledger, payments, orders, billing, pricing, partners, personal finance (Plaid integration), insights. ≈459 C# files. |
| `Aonik.Ai` | Library | LLM router, prompt store, AI run records, telemetry chat client, user memory, content image generator, TTS. ≈110 C# files. |
| `Aonik.Agents` | Library | Domain agents (Microsoft Agent Framework), orchestrator, workflows, MCP tools, AG-UI streaming. ≈157 C# files. |
| `Aonik.Api` | Web | FastEndpoints composition + middleware. |
| `Aonik.Worker` | Worker | Quartz background jobs. |
| `Aonik.AppHost` | Aspire | Local orchestration of API + Worker + UIs. |
| `Aonik.ServiceDefaults` | Library | Aspire service defaults: OpenTelemetry, health checks, Langfuse, App Insights. |
| `Aonik.Api.Contracts` | Library | Public API contract types (used mostly by CLI/SDK callers). |
| `Aonik.Cli`, `Aonik.AdminDesktop`, `Aonik.Migrator` | Executables | Auxiliary tooling. |
| `Aonik.Platform.Mcp`, `Aonik.Finance.Mcp` | Library | Model Context Protocol surfaces for Claude integration. |

### Module composition

Each domain module exposes a single `Add<Name>Module(IConfiguration)` extension that registers its `DbContext`, services, agent descriptors, contract implementations, and module-internal seeds. The API's `Program.cs` calls `AddPlatformModule → AddFinanceModule → AddAiModule → AddAgentsModule` after `AddInfrastructure`.

### Database & persistence

A single physical SQL Server database (`AonikDb`) is shared by five `DbContext`s:

- `AonikDbContext` (Infrastructure) — canonical, holds **all** DbSets, owns migrations.
- `PlatformDbContext`, `FinanceDbContext`, `AiDbContext`, `AgentsDbContext` — module-scoped read/write paths, no migration history.

The `CLAUDE.md` rule that all migrations flow through `AonikDbContext` is **enforced** in the codebase: only [src/Aonik.Infrastructure/Migrations/](src/Aonik.Infrastructure/Migrations/) contains migration files (50 of them); module projects have no `Migrations/` folder. This is good and worth preserving.

### API composition

[Program.cs](src/Aonik.Api/Program.cs) wires:

1. Aspire service defaults (telemetry/health).
2. Application + Infrastructure + four module DI calls.
3. CORS — origins from config + hard-coded localhost dev origins (lines 42–53).
4. Authentication / authorisation via `AddAonikAuthenticationAndAuthorization` ([src/Aonik.Infrastructure/Authentication/AonikAuthenticationSetup.cs](src/Aonik.Infrastructure/Authentication/AonikAuthenticationSetup.cs)).
5. FastEndpoints + JSON / enum config.
6. Scalar (Swagger) UI.
7. **A custom 147-line CORS-and-error middleware** (lines 196–342) layered before the standard `UseCors` because the comment claims FastEndpoints registers CORS metadata too late.
8. Seven middleware in a fixed order: `UseAuthentication → UseTenantContext → UseAuthorization → UseTenantValidation → UseFastEndpoints` plus four manually-mapped streaming endpoints.

### Authentication & authorisation

- JWT bearer (Auth0 + Azure AD) with proper `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `RequireSignedTokens`, 5-minute clock skew ([AonikAuthenticationSetup.cs:79–98](src/Aonik.Infrastructure/Authentication/AonikAuthenticationSetup.cs:79)).
- Permission-based, dynamic policy provider — any string permission key implicitly has a policy ([src/Aonik.Infrastructure/Authorization/PermissionPolicyProvider.cs](src/Aonik.Infrastructure/Authorization/PermissionPolicyProvider.cs)).
- `PermissionAuthorizationHandler` resolves user roles → role-permission rows in DB at request time.
- **But** endpoint enforcement is voluntary: services call `EnsurePermissionAsync` and throw `InvalidOperationException("Permission X is required.")`, which `Program.cs` lines 302–310 string-match to map to 403.

### Background processing

Quartz.NET in [src/Aonik.Worker/](src/Aonik.Worker/), with jobs for AI cost guard, customer-insight snapshots, AI summaries, scheduled-job listening, financial connection sync, stale session detection. Quartz scheduler shared via `IBackgroundJobManager` abstraction ([src/Aonik.Infrastructure/BackgroundJobs/](src/Aonik.Infrastructure/BackgroundJobs/)).

### Agent / AI architecture

- `Aonik.Ai` owns the LLM-side primitives: prompt registry, AI run records, telemetry chat client, user memory.
- `Aonik.Agents` owns the agent runtime: tool wrappers, workflows (graph + sequential), AG-UI streaming endpoint, agent configuration service.
- **Inversion smell:** `PlatformModule` and `FinanceModule` register `IDomainAgentDescriptor` implementations themselves ([PlatformModule.cs:146](src/Aonik.Platform/PlatformModule.cs:146), [FinanceModule.cs:177-181](src/Aonik.Finance/FinanceModule.cs:177)), which means the domain modules know about and depend on the agent contract. Combined with Platform/Finance referencing `Aonik.Agents` as a NuGet/ProjectReference, the dependency direction is "domain → agents", which is the opposite of the "agents propose; systems execute" rule.

### Architectural alignment assessment

**Aligned with intent:**
- Single-migration-stream pattern is enforced and consistent.
- Anemic-entity + service-layer business logic is consistent across modules.
- FastEndpoints vertical slices are present (e.g. [src/Aonik.Finance/Endpoints/Orders/ListOrdersEndpoint.cs](src/Aonik.Finance/Endpoints/Orders/ListOrdersEndpoint.cs) sits next to its service).
- Audit / soft-delete handling is centralised in `AonikDbContextBase`.
- OpenTelemetry coverage (Aspire + Langfuse) is real.

**Out of alignment:**
- "Application layer" exists in name only; orchestration, validation, and use-cases are scattered across module services and endpoints.
- Modules reach up to `Agents` rather than `Agents` reaching down (back-pointing dependency).
- `Aonik.Application` imports concrete entity types from Platform and Finance, undermining the inversion the project naming implies.
- Permission enforcement is procedural, not declarative.

---

## 4. Performance Analysis

### Findings

| ID | Finding | Evidence | Severity | Effort | Recommendation |
|---|---|---|---|---|---|
| P-01 | Sync-over-async in DI factory for `IUserMemoryService` | `.GetAwaiter().GetResult()` at [DependencyInjection.cs:290-291](src/Aonik.Infrastructure/DependencyInjection.cs:290) | High | Small | Resolve backend choice eagerly at startup (read setting once into a singleton) or use `IAsyncFactory` pattern; under load this blocks a thread-pool thread per scoped resolution. |
| P-02 | `Task.Result` after `Task.WhenAll` in observability service | [src/Aonik.Infrastructure/Observability/AppInsightsQueryService.cs:76-79](src/Aonik.Infrastructure/Observability/AppInsightsQueryService.cs:76) (per agent finding; verified by grep that the file contains `.Result`) | High | Small | Replace four `.Result` reads with `await` of each sub-task or destructure `Task.WhenAll` results properly. |
| P-03 | `Task.Result` in dashboard aggregation | [src/Aonik.Finance/Services/Insights/MySpaceSummaryService.cs:69-70](src/Aonik.Finance/Services/Insights/MySpaceSummaryService.cs:69) (per agent finding) | High | Small | Same fix as P-02. |
| P-04 | Sequential, blocking startup seed (≥6 services) | [Program.cs:131-173](src/Aonik.Api/Program.cs:131) | Medium | Medium | Move post-bootstrap seeds (catalog, settings, prompts, tasks, notifications, role top-up) to a one-off background job; Identity seed is fine to keep blocking. Cold start can otherwise be 10s+ on first boot. |
| P-05 | Hard-coded role→permission top-up runs every cold start | [Program.cs:643-755](src/Aonik.Api/Program.cs:643) | Medium | Small | Same as P-04 — push to a background job; the `foreach (role in tenantRoles)` issues a per-role `RolePermissions` query on every startup against every tenant, scaling linearly with tenant count. |
| P-06 | `IgnoreQueryFilters()` in 16 files including hot read paths | [src/Aonik.Finance/Services/PersonalFinance/TransactionClassificationService.cs](src/Aonik.Finance/Services/PersonalFinance/TransactionClassificationService.cs), [src/Aonik.Worker/Jobs/CustomerInsightSnapshotJobUserEnumerator.cs](src/Aonik.Worker/Jobs/CustomerInsightSnapshotJobUserEnumerator.cs) and 14 others | Medium | Medium | Beyond the security implication (S-04), each of these forces a global table scan; review whether they need cross-tenant scope or just need to bypass soft-delete, and switch to a narrower mechanism (e.g. soft-delete-only filter) where possible. |
| P-07 | Reflection-based audit-field stamping per entity, per save | [AonikDbContextBase.cs:274-301](src/Aonik.SharedKernel/Persistence/AonikDbContextBase.cs:274) | Medium | Small | The implementation iterates `ChangeTracker.Entries<AuditableEntity>()` (already typed, no reflection per se), but the surrounding tenant write enforcement also enumerates `ITenantScoped` entries. For bulk inserts (>1k rows) this becomes the bottleneck; consider `DbContext.AddRangeAsync` with batch hooks or `BulkExtensions`. Currently bounded by EF Core's own change-tracker cost, so the impact is real but not catastrophic. |
| P-08 | Many list endpoints paginate; some don't | List endpoints like [ListOrdersEndpoint.cs](src/Aonik.Finance/Endpoints/Orders/ListOrdersEndpoint.cs) and [ListProposalsEndpoint.cs](src/Aonik.Agents/Endpoints/ListProposalsEndpoint.cs) take page parameters and use `Skip/Take`; sample exists. (Speculative — not exhaustively verified across all endpoints.) | Medium | Medium | Audit the remaining ~50 endpoints for unbounded `.ToListAsync()`; an architecture test (`NetArchTest`) that flags any endpoint whose response is `IEnumerable<T>` without a `Cursor` or `Page` would be a one-time investment. |
| P-09 | Unverified AI provider call timeouts | `Aonik.Ai` chat clients (no explicit `Timeout` config found in the DI scan) | Medium | Small | Set explicit `HttpClient.Timeout` (30–60s) on the OpenAI / Anthropic typed clients and a `RequestTimeout` on `IChatClient` invocations; without it, a slow provider can pin worker threads indefinitely. |
| P-10 | Large in-process services (>1500 lines) — `CustomerInsightSnapshotGenerator.cs` (2,331 lines), `DemoSeedService.cs` (1,917), `FinanceDemoSeedContributor.cs` (1,913) | Glob | Low | Performance impact is mostly indirect (warm-up time, JIT cost); the bigger problem is maintainability. Mentioned here for awareness. |
| P-11 | `ChatThreadHistoryCache` cache key is thread-id only | [src/Aonik.Agents/Services/ChatThreadHistoryCache.cs:74-75](src/Aonik.Agents/Services/ChatThreadHistoryCache.cs:74) | Low | Small | Globally unique GUIDs make cross-tenant collision improbable but not impossible. Add `tenantId:N` to the cache key for defence in depth. (Cross-references S-08.) |
| P-12 | HttpClient lifetime correctly handled via `IHttpClientFactory` for Auth0, Azure AD, Firebase, Qdrant, App Insights | [src/Aonik.Infrastructure/DependencyInjection.cs:178-198, 250-270](src/Aonik.Infrastructure/DependencyInjection.cs:178) | — | — | Healthy. No socket-exhaustion risk from raw `new HttpClient()` detected. |
| P-13 | FusionCache used with explicit TTLs (24h TTS, 30m chat-thread history) | [src/Aonik.Ai/Services/ITtsCache.cs:36](src/Aonik.Ai/Services/ITtsCache.cs:36), [DependencyInjection.cs:93,100](src/Aonik.Infrastructure/DependencyInjection.cs:93) | — | — | Healthy. Stampede protection should be confirmed (FusionCache has it built-in but verify). |

### Prioritised Performance Remediation Plan

1. **Now (this week):** Fix P-01, P-02, P-03 — the three sync-over-async hotspots. Each is a one-file, one-method change with measurable thread-pool benefit under load.
2. **Now (this week):** Set explicit timeouts on AI provider HttpClients (P-09).
3. **Next 2 weeks:** Move startup seeds and role-permission top-up off the request path (P-04, P-05). The current implementation costs ~5–15s of cold-start time and runs synchronously on every API instance restart.
4. **Next month:** Audit and document every `IgnoreQueryFilters()` use site (P-06); replace with narrower filters (`IgnoreSoftDeleteOnly`) where possible.
5. **Lower priority:** Add an architecture test that fails when a list endpoint returns an unbounded collection (P-08).

---

## 5. Security and SOC 2 Readiness Analysis

### Findings

| ID | Finding | Evidence | SOC 2 Theme | Severity | Effort | Recommendation |
|---|---|---|---|---|---|---|
| **S-01** | **Real third-party API keys committed to git history** in [src/Aonik.Api/appsettings.Development.json](src/Aonik.Api/appsettings.Development.json): OpenAI key (line 29), Auth0 Management ClientSecret (lines 61–63), Plaid Secret (lines 153–154), Langfuse Secret (lines 43–45). The file is now `.gitignore`d but `git log -- src/Aonik.Api/appsettings.Development.json` returns at least three commits where it was tracked. | Direct read of the file; `git log` shows commits `61dfddf3`, `4eed4596`, `5cbe5399` touched it. | Security, Confidentiality | **Critical** | Medium | (1) Rotate every key listed above today. (2) Use `git filter-repo` to purge the file from history, then force-push and notify all clones. (3) Move secrets to Azure Key Vault for deployed environments; .NET User Secrets (`dotnet user-secrets`) for local dev. (4) Add a secrets-scanning pre-commit hook (gitleaks, trufflehog). |
| S-02 | `token.txt` at repo root contains an Auth0 JWT. Currently `.gitignore`d (`git check-ignore` returns 0; no commits found via `git log -- token.txt`). | [token.txt](token.txt) (file present, ignored) | Security, Confidentiality | **High** | Small | Verify across **all** branches/forks that the file was never committed. Even if not committed, the convention of leaving auth tokens in the working tree increases the chance one ends up in a screenshot, debug archive, or copy-paste. Move to a non-source-controlled location (`%LOCALAPPDATA%/Aonik/dev-token`). |
| S-03 | Authorisation is enforced via service-layer thrown exceptions; the API surface relies on regex-matching `ex.Message.StartsWith("Permission ")` to map to 403 | [Program.cs:302-310](src/Aonik.Api/Program.cs:302). Service-layer pattern: `EnsurePermissionAsync(...)` throws `InvalidOperationException("Permission X is required.")` (e.g. `AdminServiceBase`, `FinanceServiceBase`, agent finding cited `AdminServiceBase.cs:23-36`). | Security | **Critical** | Medium | Replace with a typed `PermissionDeniedException` (or `ForbiddenException`); register an exception filter that maps it to `Results.Forbid()`. Better: enforce permissions declaratively — endpoints declare `RequiresPermission("Invoice.Create")` (a `RouteHandlerFilter` / `IEndpointFilter`) so missing the call cannot silently disable the check. |
| S-04 | 16 call sites bypass the global tenant query filter via `IgnoreQueryFilters()` | grep returned 16 files, including [src/Aonik.Platform/Endpoints/Registrations/SendRegistrationPhoneOtpEndpoint.cs](src/Aonik.Platform/Endpoints/Registrations/SendRegistrationPhoneOtpEndpoint.cs), [src/Aonik.Agents/Framework/AgentConfigurationService.cs](src/Aonik.Agents/Framework/AgentConfigurationService.cs), [src/Aonik.Finance/Services/PersonalFinance/TransactionClassificationService.cs](src/Aonik.Finance/Services/PersonalFinance/TransactionClassificationService.cs), [src/Aonik.Platform/Services/Identity/TenantService.cs](src/Aonik.Platform/Services/Identity/TenantService.cs), several seed services, and worker jobs. | Security, Confidentiality, Privacy | **High** | Medium | Each call site needs a documented justification. Common legitimate reasons: (a) needing soft-deleted rows for audit, (b) seeding global data, (c) reading another tenant's data on a documented platform-admin endpoint. Introduce two narrower extensions: `IncludeSoftDeleted()` (skips only the `IsDeleted == false` part of the filter) and `AcrossTenants()` (explicitly platform-admin scoped, requires `IsPlatformAdmin` claim). Make `IgnoreQueryFilters()` itself a banned API via Roslyn analyzer. |
| S-05 | Anonymous tenant resolution via unvalidated `X-Tenant-Id` header | [TenantContextMiddleware.cs:90-99](src/Aonik.Api/Middleware/TenantContextMiddleware.cs:90); `ResolveAnonymousTenantId` returns whatever GUID the client sends. | Security, Confidentiality | **High** | Small | Public endpoints (registration, public catalog, public payment intent) genuinely need a way to declare which tenant they belong to. Two safer designs: (a) tenant-scoped subdomain (`{tenantSlug}.api.aonik.com`) with the tenant id resolved server-side from the slug; (b) an opaque, server-issued tenant token rather than the bare GUID. At minimum, validate that the supplied tenant id corresponds to an `Active` tenant before letting the request proceed. |
| S-06 | Tenant-binding for **authenticated** routes uses `tenantResolver.ResolveTenantId() ?? tenantResolver.ResolveFromHttpContext()` | [TenantContextMiddleware.cs:59](src/Aonik.Api/Middleware/TenantContextMiddleware.cs:59). The fallback `ResolveFromHttpContext()` reads from header — meaning an authenticated user could potentially supply a different tenant id via header if the JWT-claim resolver returns null. | Security, Confidentiality | **High** | Small | Make claim-based resolution authoritative: if the user is authenticated, the tenant **must** come from a verified claim, never from a header. The header should be ignored for authenticated requests, and a mismatch (claim vs. header) should be a 403, not a fallback. |
| S-07 | No FastEndpoints / FluentValidation validators are wired up | Grep for `: Validator<`, `: AbstractValidator<`, `FluentValidation` in `src/` returned 0 production matches (only build-output `*.deps.json`). | Security, Processing Integrity | **High** | Medium | Add a FluentValidation validator per request DTO and register the assembly via `services.AddValidatorsFromAssembly(...)`. FastEndpoints supports per-endpoint `Validator<TRequest>`. Without this, malformed input reaches services and EF; the only barrier is database constraints, which produce 500-class errors instead of 400. |
| S-08 | Cache keys for chat-thread history don't include tenant id | [src/Aonik.Agents/Services/ChatThreadHistoryCache.cs:74-75](src/Aonik.Agents/Services/ChatThreadHistoryCache.cs:74) | Confidentiality | Low | Small | Add `:t-{tenantId:N}` to the key. Cross-tenant collision is improbable with random GUIDs, but defence in depth is cheap. |
| S-09 | No webhook endpoints / signature verification in the codebase | Grep for `Webhook` returns DTOs (`PlaidAccountWebhookRequest`) but no receiving endpoints in `src/Aonik.Finance/Endpoints/Public/`. | Security, Processing Integrity | **High** (when implemented) | Medium | Before any webhook receiver lands, define a signing convention (HMAC + timestamp + replay-protection nonce) and a `WebhookSignatureValidator` middleware. Note this as an open architectural question. |
| S-10 | No field-level encryption for PII / payment instrument data | No `EncryptedString` / `[Encrypted]` annotation found across entities. | Confidentiality, Privacy | Medium | Large | For a financial platform with KYC, SSN/ID numbers, bank account numbers, and PSP customer ids in the database, lack of field-level encryption is a SOC 2 finding. Roadmap: identify PII fields, apply EF Core value converters with Azure Key Vault wrapping keys. Disk-level encryption (TDE) is necessary but not sufficient. |
| S-11 | Error response leaks exception type, inner exception, stack trace fragments to any environment named `dev` | [Program.cs:317-333](src/Aonik.Api/Program.cs:317): `var includeDetails = IsDevelopment() || string.Equals(envName, "dev", ...)` | Security, Confidentiality | Medium | Small | Restrict to `IsDevelopment()` only; do not consume an environment-name string for the same purpose. Operators who need exception detail in `dev` should use App Insights / Langfuse, not the response body. |
| S-12 | No audit-log entity / interceptor for write-mutation history | `AuditableEntity` provides `CreatedBy / UpdatedBy / DeletedBy` on the row, but there is no append-only audit table or `SaveChangesInterceptor` capturing the diff. SOC 2 Processing Integrity expects "we can prove what changed and when". | Processing Integrity | **High** | Medium | Add a `SaveChangesInterceptor` that emits a row to an `AnkAuditEntries` append-only table per change (entity name, primary key, before/after JSON, actor, tenant, request id, timestamp). Index by tenant + entity + timestamp. |
| S-13 | Permission top-up logic in `Program.cs` mirrors the role→permissions dictionary in `TenantProvisioner` and is hand-kept in sync | [Program.cs:643-755](src/Aonik.Api/Program.cs:643) explicitly comments "Keep this dictionary in sync with TenantProvisioner.EnsureDefaultRolePermissionsAsync." | Security, Processing Integrity | **High** | Small | Extract to `RolePermissionsConfiguration` in SharedKernel; both `TenantProvisioner` and the startup top-up call the same source. The current shape will drift the first time someone forgets the comment. |
| S-14 | OpenAI/Langfuse keys committed at all means the development environment is the **same trust boundary** as production for those services | S-01 evidence + the comment "Sandbox credentials" in front of Plaid (line 152–154 are sandbox, but the OpenAI key looks production-grade by prefix) | Security | **High** | Small | Ensure development uses isolated, low-quota OpenAI org; production uses a different, vault-managed key. Treat the existing committed OpenAI key as compromised. |
| S-15 | Token validation is correctly configured (audience, issuer, lifetime, signature, signed-tokens) | [AonikAuthenticationSetup.cs:79-98](src/Aonik.Infrastructure/Authentication/AonikAuthenticationSetup.cs:79) | Security | — | — | Healthy. Worth keeping a regression test that asserts these settings are not weakened. |
| S-16 | Tenant-write enforcement at SaveChanges level | [AonikDbContextBase.cs:219-256](src/Aonik.SharedKernel/Persistence/AonikDbContextBase.cs:219) — refuses to save tenant-scoped entities without context, throws on tenant mismatch | Security, Confidentiality | — | — | Healthy. This is the strongest part of the tenancy story; protect it with explicit tests (one already exists per the testing review). |
| S-17 | `ApplyTenantQueryFilters` rule "no tenant context → show all rows" | [AonikDbContextBase.cs:120-126](src/Aonik.SharedKernel/Persistence/AonikDbContextBase.cs:120). The filter `OR-elses` `noTenantContext` so that without a provider, **all rows are visible**. | Confidentiality | **High** | Medium | This is intentional for design-time / migrations / seeders, but it is a footgun for any unauthenticated request path that ends up with a null `TenantProvider`. Consider: throw on read instead of silently returning all rows for tenant-scoped entities, mirroring the write-side behaviour. |
| S-18 | "Setup secret" present in plaintext | [appsettings.Development.json:80](src/Aonik.Api/appsettings.Development.json:80): `"SetupSecret": "dev-setup-2026"`. Used by Bootstrap endpoints to allow first-run tenant creation. | Security | Medium | Small | If `Bootstrap.Enabled = true` is ever true in any non-local environment, this becomes the master key for tenant provisioning. Confirm that `Bootstrap.Enabled` is `false` in production config and that the setup secret is rotated per environment. |
| S-19 | `Verification:HashKey = "dev-change-me"` | [appsettings.Development.json:103](src/Aonik.Api/appsettings.Development.json:103) | Security | Low | Small | Acceptable for local dev as long as it's truly only local; never deploy. |

### SOC 2 Readiness Gaps

| Gap | What's missing | What evidence SOC 2 will expect |
|---|---|---|
| **Secrets management** | No vault integration; secrets in `appsettings.Development.json` history. | A secrets manager (Azure Key Vault / Managed Identity) for every environment, with documented rotation and access-control evidence. |
| **Mutation audit trail** | `CreatedBy`/`UpdatedBy` on rows, but no append-only diff log. | Append-only audit table (or external log sink) capturing every write; query interface for evidence retrieval. |
| **Logical access management** | Permissions exist; assignment is dictionary-driven in code. | Documented role catalog, evidence of "who has what" per tenant, periodic review process. |
| **Tenant-isolation testing** | One positive test (`TenantSecurityTests.cs`); no negative-path matrix for `IgnoreQueryFilters` paths. | Comprehensive cross-tenant test suite that covers every `IgnoreQueryFilters` site; CI gate. |
| **Vendor / dependency hygiene** | No SBOM, no vulnerability scan in CI surfaced. | Automated SCA (GitHub Advanced Security / Snyk / Dependabot enabled with required reviewers). |
| **Change-management evidence** | git history exists but PR review enforcement is not visible from this repo alone. | Branch-protection rules (PR review, required CI, no force-push to main). |
| **Encryption-at-rest of PII** | No field-level encryption. | Demonstrate encryption for any field classified as PII. |
| **Webhook integrity** | No signature verification path. | Documented HMAC verification for every inbound webhook. |
| **Production logging guarantees** | Health checks return only "self healthy". | Liveness + readiness + dependency-probe checks in production with documented alerting. |
| **Runbook readiness** | None observed. | Runbooks for "PSP webhook failures", "Auth0 outage", "AI provider rate limit", "tenant isolation alarm". |

### Security Remediation Priorities

1. **Today:** Rotate the four committed third-party keys (OpenAI, Auth0 Management, Plaid, Langfuse). (S-01, S-14)
2. **This week:** Convert authorisation from string-matched exceptions to a typed exception + endpoint-filter (S-03). Add `RolePermissionsConfiguration` shared between `TenantProvisioner` and startup top-up (S-13).
3. **This week:** Tighten `TenantContextMiddleware` for authenticated routes — JWT claim is authoritative (S-06).
4. **This sprint:** Audit every `IgnoreQueryFilters()` site, document a justification, narrow to soft-delete-only where possible (S-04). Add a banned-API analyser.
5. **This sprint:** FastEndpoints/FluentValidation validators on every request DTO (S-07).
6. **Next quarter:** Append-only audit log via `SaveChangesInterceptor` (S-12). Field-level encryption for PII (S-10).

---

## 6. Code Overengineering and Simplification Analysis

### Findings

| ID | Finding | Evidence | Severity | Effort | Simplification Recommendation |
|---|---|---|---|---|---|
| O-01 | `Aonik.Application` is a façade with no logic; `AddApplication()` returns the service collection unchanged | [src/Aonik.Application/DependencyInjection.cs:12-15](src/Aonik.Application/DependencyInjection.cs:12) | High | Medium | Either give the layer real responsibility (cross-module orchestration, validators registered here, request pipeline middleware) or merge it into Infrastructure / SharedKernel and stop the impression that there's an Application layer. |
| O-02 | `IAonikDbContext` in the Application layer imports concrete entity types from Platform and Finance modules | [src/Aonik.Application/Abstractions/Persistence/IAonikDbContext.cs:1-14](src/Aonik.Application/Abstractions/Persistence/IAonikDbContext.cs:1) | High | Large | The "abstraction" doesn't abstract anything. Either remove `IAonikDbContext` (use the concrete `AonikDbContext` directly) or split it into module-specific interfaces (`IPlatformReadModel`, etc.) that don't depend on cross-module types. |
| O-03 | 760-line `Program.cs` with custom CORS + custom error middleware + role-permission top-up + seeding orchestration inline | [src/Aonik.Api/Program.cs](src/Aonik.Api/Program.cs) | High | Medium | Break into composition extensions: `app.UseAonikCors()`, `app.UseAonikExceptionHandler()`, plus a `IStartupTask` interface for the various seeding routines. Same outcome, ten times more reviewable. |
| O-04 | 147-line custom CORS middleware to work around a claim that "FastEndpoints registers CORS metadata too late" | [Program.cs:196-342](src/Aonik.Api/Program.cs:196) | Medium | Small | Validate the assumption against current FastEndpoints version. The library has had multiple releases since this comment was likely written; standard `UseCors()` may now work, in which case 147 lines disappear. |
| O-05 | Hard-coded role→permissions dictionary in two places (`Program.cs:650-689` and `TenantProvisioner.EnsureDefaultRolePermissionsAsync`) with a comment saying "keep these in sync" | [Program.cs:643-755](src/Aonik.Api/Program.cs:643) | High | Small | Extract `RolePermissionsConfiguration.cs` in SharedKernel; both call sites import the same constants. (Same as S-13 — security and overengineering both bite.) |
| O-06 | `CustomerInsightSnapshotGenerator.cs` is 2,331 lines | Glob | High | Large | Likely a god object. Split by responsibility (data assembly, scoring, AI prompt construction, output formatting). |
| O-07 | `DemoSeedService.cs` (1,917 lines) and `FinanceDemoSeedContributor.cs` (1,913 lines) are massive seed files with 80+ hard-coded GUIDs each | Glob | Medium | Medium | Decompose by domain (Catalog, Pricing, Partners, Accounts, etc.). Move static seed data into `*.json` resources read at runtime — same effect, much smaller surface. |
| O-08 | `AccessManagementService.cs` is 1,364 lines with 10+ tenant-id filter expressions | Glob | Medium | Medium | Likely combines invite, role assignment, and access enumeration into one service. Split by use-case. |
| O-09 | `AguiStreamingEndpoint.cs` is 853 lines, 12 injected dependencies, optional-nullable constructor params | [src/Aonik.Agents/Endpoints/AguiStreamingEndpoint.cs](src/Aonik.Agents/Endpoints/AguiStreamingEndpoint.cs) | Medium | Medium | Endpoints should orchestrate, not implement. Move thread management, agent resolution, and protocol translation into separate services. |
| O-10 | Tenant-id filter pattern repeated ~274 times across ~108 files (e.g. `.Where(x => x.TenantId == _tenantProvider.TenantId)`) | Grep across `src/` | Medium | Small | Add `IQueryable<T> ForCurrentTenant<T>(this IQueryable<T> q)` extension on `ITenantQueryable`. Reduces noise and allows central instrumentation/auditing. |
| O-11 | `AdminServiceBase` (36 lines) and `FinanceServiceBase` (36 lines) duplicate the same `EnsurePermissionAsync` pattern | Per agent finding | Low | Small | Promote to a single `PermissionedService` base class in SharedKernel. Better still: replace with the declarative endpoint filter from S-03. |
| O-12 | Single-implementation interfaces are common (≈48% of services have only one implementation, no test double, and no plausible second impl) | Per agent grep ratio (99 interfaces : 211 services) | Medium | Medium | Audit interface vs. concrete usage. If `IFooService` has one impl and never appears in a test mock, delete the interface and inject the concrete class. Some interfaces are necessary (e.g. `IUserMemoryService` has SQL + Qdrant impls); most are not. |
| O-13 | `Platform` and `Finance` directly reference `Aonik.Agents`, then register `IDomainAgentDescriptor` themselves | [src/Aonik.Platform/PlatformModule.cs:146](src/Aonik.Platform/PlatformModule.cs:146), [src/Aonik.Finance/FinanceModule.cs:177-181](src/Aonik.Finance/FinanceModule.cs:177) | Medium | Large | Domain modules should expose **agent-shaped data** (descriptor records, prompt resources) and `Aonik.Agents` should discover them via assembly scan or a contributor interface. The current direction makes the domain dependent on the agent runtime. |
| O-14 | `Aonik.Ai` references `Aonik.Finance` and `Aonik.Platform` | `Aonik.Ai.csproj` lines 23–24 (per agent finding) | Medium | Medium | Same kind of inversion. AI infrastructure should not know about Finance or Platform entities; if a service (`AiProviderSettings`) needs `ISettingProvider` from Platform, that contract belongs in SharedKernel. |
| O-15 | Five separate `DbContext` types over a single physical database, each registered in a different module's DI extension | Per architecture review | Medium | Medium | This is intentional for module DI scoping, but the cost is high: schema changes require touching `AonikDbContext` plus the relevant module context, and `OnModelCreating` is duplicated. Consider a single `AonikDbContext` per database with module-specific `IModelConfiguration` extensions discovered at startup. |
| O-16 | Inline 700-line seed + role-topup logic at startup blocks first request | [Program.cs:95-180, 643-755](src/Aonik.Api/Program.cs:95) | Medium | Small | Pull into `IStartupTask` (or `IHostedService`) implementations; explicit, testable, and parallelisable. |
| O-17 | Two MCP projects (`Aonik.Platform.Mcp`, `Aonik.Finance.Mcp`) with no clear consumers visible from this repo | Solution file | Low | Small | Confirm whether they're shipped surfaces or experimental scaffolding. If experimental, mark with `<IsPackable>false</IsPackable>` and document. |
| O-18 | Verbose CORS preflight logging and `Auth:Diagnostics:LogHeaderPresence` flag indicate ongoing CORS / auth-header debugging | [Program.cs:347-381](src/Aonik.Api/Program.cs:347) | Low | Small | Remove the diagnostics path once the underlying issue is fixed; today it's an extra middleware that always evaluates on every request. |
| O-19 | `appsettings.Development.json` has 167 lines and 10+ feature sections, but configuration option-classes (`IOptions<T>`) are not always used — many are read via raw `IConfiguration["Section:Key"]` | sample reads, [Program.cs:42, 90-93, 392-396](src/Aonik.Api/Program.cs:42) | Low | Small | Define typed `Options` classes per feature with `.Bind` + validation; saves runtime drift between key names. |

### Biggest Simplification Opportunities

1. **Decide the fate of `Aonik.Application`.** Either give it real responsibility (cross-module orchestration / use-cases / validation registration) or merge it away. Ambiguity here causes new code to land in the wrong place every time.
2. **Make permission enforcement declarative.** Endpoint-level filter + typed exception eliminates O-11, S-03, O-05/S-13, and shrinks `Program.cs` by ~50 lines.
3. **Break up `Program.cs`.** Move CORS, exception, seed orchestration, and `LogResolvedDatabaseConnection` into separate composition extensions / `IStartupTask`s. The file should be <100 lines.
4. **Replace bidirectional module references with discovery.** `Aonik.Agents` should scan for `IDomainAgentDescriptor` rather than have Platform/Finance register them. Same for AI's reach into Finance/Platform — push the contracts into SharedKernel.
5. **Audit all single-impl interfaces.** Probably 50+ interfaces can be deleted with no consumer impact, reducing onboarding cognitive cost.
6. **Decompose god-files.** `CustomerInsightSnapshotGenerator` (2,331 lines), `DemoSeedService` (1,917), `FinanceDemoSeedContributor` (1,913), `AccessManagementService` (1,364), `AguiStreamingEndpoint` (853). Each likely hides 3–6 distinct responsibilities.
7. **`ForCurrentTenant<T>()` extension** to replace ~274 inline `Where` clauses.
8. **JSON-resource seed data.** Move the GUID constants and dictionary literals out of code into versioned JSON resources.
9. **Drop the 147-line custom CORS middleware** if standard `UseCors` now works (it likely does).
10. **Single canonical DbContext.** Module-scoping via DI is cute; in practice every model change requires touching two contexts. Consolidate.

---

## 7. Clean Architecture Assessment

### Findings

| ID | Finding | Evidence | Principle Violated | Severity | Effort | Recommendation |
|---|---|---|---|---|---|---|
| C-01 | Application layer imports concrete module entity types | [src/Aonik.Application/Abstractions/Persistence/IAonikDbContext.cs:1-14](src/Aonik.Application/Abstractions/Persistence/IAonikDbContext.cs:1) — `using Aonik.Platform.Entities.*; using Aonik.Finance.Entities.*;` | Dependency Inversion (Application should not depend on concrete domain implementations) | High | Large | The interface either belongs in Infrastructure (alongside `AonikDbContext`) or should be split per module. There is no value in having an "abstract" DbContext that already knows every concrete entity. |
| C-02 | Application layer is empty | [src/Aonik.Application/DependencyInjection.cs:14](src/Aonik.Application/DependencyInjection.cs:14) | Layered architecture intent | High | Medium | Either populate the layer with use-case orchestration (validators, mediator handlers, multi-module workflows) or remove it. The current shape mis-signals architectural intent. |
| C-03 | Infrastructure references all four domain modules and instantiates their DI in `AddInfrastructure` | [src/Aonik.Infrastructure/Aonik.Infrastructure.csproj:10-14](src/Aonik.Infrastructure/Aonik.Infrastructure.csproj:10) | Dependency direction (Infrastructure should be a leaf, not a hub) | Medium | Large | The Infrastructure project is acting as the composition root. That's fine if it's named that — `Aonik.Composition` would be clearer. Currently the name promises "infrastructure adapters", and the size (1200+ lines of DI) reflects the mismatch. |
| C-04 | Domain modules reference `Aonik.Agents` and `Aonik.Ai` | `Aonik.Platform.csproj` (refs Agents), `Aonik.Finance.csproj` (refs Agents), `Aonik.Ai.csproj` (refs Finance + Platform) | Cross-cutting concern direction (agents should plug into domains, not the reverse) | High | Large | Move `IDomainAgentDescriptor` and prompt-store contracts into `Aonik.SharedKernel`. Have `Aonik.Agents` discover descriptors via assembly scan or a `IAgentDescriptorContributor` interface. |
| C-05 | Business logic (role→permission policy, exception→403 mapping) lives in `Program.cs` | [Program.cs:302-310, 643-755](src/Aonik.Api/Program.cs:302) | Layer responsibility (composition root should not contain domain logic) | High | Small | Extract to `RolePermissionsConfiguration` and a `PermissionDeniedExceptionHandler`. |
| C-06 | Permission enforcement is procedural (service throws), not declarative (endpoint metadata) | `EnsurePermissionAsync` calls in service base classes; mapping in [Program.cs:302-310](src/Aonik.Api/Program.cs:302) | Cross-cutting concerns (authorisation should be at the boundary) | High | Medium | Endpoint filter `[RequiresPermission("Invoice.Create")]` evaluated before the service is invoked. |
| C-07 | Seeding logic blocks Application startup; seed services are part of the API entry point | [Program.cs:131-173](src/Aonik.Api/Program.cs:131) | Layer responsibility (seeders are Infrastructure / dev-time, not Web) | Medium | Small | `IHostedService` per seed; `Program.cs` should not know which seeders exist. |
| C-08 | DTOs / response shapes live next to endpoints, but mapping is manual and inconsistent | Per agent finding (`ProviderTransactionMapper.cs`, `ClaimsRoleMapper.cs`, ad-hoc `ToDto()` extensions) | Mapping consistency | Low | Medium | Pick one pattern (manual `ToContract()` extension methods on the entity, OR a static mapper class per concept) and apply it. |
| C-09 | Five `DbContext`s, but `OnBeforeSave` / tenant-write enforcement is implemented in `AonikDbContextBase` (good) | [src/Aonik.SharedKernel/Persistence/AonikDbContextBase.cs](src/Aonik.SharedKernel/Persistence/AonikDbContextBase.cs) | Cohesion | — | — | Healthy. The base handles cross-cutting; module contexts only add module-specific config. |
| C-10 | Validators do not exist, so validation lives wherever the developer remembered to add it (often nowhere) | Grep: 0 production validators | Layer responsibility (validation should be in Application / Endpoint, not in services or absent) | High | Medium | FluentValidation per request DTO; FastEndpoints `Validator<TRequest>`. |

### Dependency Direction Assessment

The intended direction (`SharedKernel ← Domain Modules ← Application ← Infrastructure ← API`) is **partially honoured**:

- `SharedKernel` is genuinely dependency-free (good).
- `Domain modules` mostly stand alone, **except** they reference `Aonik.Agents` and (in `Aonik.Ai`'s case) `Aonik.Finance` + `Aonik.Platform`. This is the biggest violation.
- `Aonik.Application` has no real meaning today — it's a thin layer that imports module entities directly, so its position in the dependency graph is illusory.
- `Aonik.Infrastructure` references **every** module, and acts as the composition root.
- `Aonik.Api` references Infrastructure + every module + Application.

**Net:** The layering exists in the project list but is not actually inverted. Infrastructure is the apex of the dependency graph, not the leaf. Renaming Infrastructure to `Aonik.Composition` (or splitting it) would at least make the shape honest.

### Layer Responsibility Assessment

| Layer | Should do | Actually does |
|---|---|---|
| SharedKernel | Primitives, contracts, integration events | Does this. |
| Domain Modules | Entities, services, endpoints, persistence config | Does this — plus registers agent descriptors (cross-cutting bleed). |
| Application | Use-cases, orchestration, validation | **Empty.** |
| Infrastructure | External-system adapters, DI for adapters | This **plus** DI for every module **plus** owns `AonikDbContext` **plus** owns migrations. |
| API | HTTP composition, middleware | This **plus** seed orchestration, role-permission top-up, custom CORS, custom error mapping. |

Multiple layers have responsibilities that should belong elsewhere; this is the simplification thread that runs through O-01–O-19.

### Module Boundary Assessment

- **SharedKernel ↔ everyone:** clean.
- **Platform ↔ Finance:** clean (no direct refs).
- **Platform → Agents, Finance → Agents:** **violation** (back-pointing).
- **Ai → Platform, Ai → Finance:** **violation** (Ai pulls domain modules).
- **Aonik.Finance → InternalsVisibleTo "Aonik.Platform"** (per agent finding): the test case is documented but marks an additional permeable boundary.

A cleaner picture would be: `Aonik.Agents` (and `Aonik.Ai`) reference only `SharedKernel`; domain modules contribute to them via SharedKernel-defined contributor interfaces.

---

## 8. Maintainability and Code Quality

### Findings

| ID | Finding | Evidence | Severity | Effort | Recommendation |
|---|---|---|---|---|---|
| M-01 | Many files >1,000 lines (CustomerInsightSnapshotGenerator 2,331; DemoSeedService 1,917; FinanceDemoSeedContributor 1,913; AccessManagementService 1,364; AccountLinkService 1,215; AguiStreamingEndpoint 853) | Glob | High | Large | Split by responsibility. (Detailed under O-06–O-09.) |
| M-02 | `Program.cs` mixes 5+ concerns | [Program.cs](src/Aonik.Api/Program.cs) (760 lines) | High | Small | Extract composition extensions. |
| M-03 | "Keep this dictionary in sync" comments — explicit duplication | [Program.cs:646](src/Aonik.Api/Program.cs:646) | High | Small | One source of truth. |
| M-04 | Heavy reliance on stringly-typed permission keys (`"Invoice.Create"`) and role names (`"TenantAdmin"`, `"Operations"`) | [Program.cs:650-689](src/Aonik.Api/Program.cs:650), service base classes | Medium | Small | Promote to constants in `Permissions.Invoice.Create` / `Roles.TenantAdmin` static classes. Compile-time refactor safety. |
| M-05 | Inconsistent naming ("Service" / "Provider" / "Manager" / "Handler") | Per agent finding | Low | Medium | Pick one suffix per role (Service for use-cases, Provider for context resolvers, Repository where applicable). |
| M-06 | Tenant-id filter repeated ~274 times | Grep | Medium | Small | `ForCurrentTenant<T>()` extension. |
| M-07 | Diagnostics middleware (`Auth:Diagnostics:LogHeaderPresence`) and dev-only static-file paths in `Program.cs` | [Program.cs:347-442](src/Aonik.Api/Program.cs:347) | Low | Small | Move to a `Aonik.Api.Diagnostics` extension; only register when the flag is set. |
| M-08 | Folder depth: `src/Aonik.AdminUi/dist/content/setup-guides/...` 5+ levels (build artefacts; not source) | Glob | Low | — | If `dist/` is committed, gitignore it. (Build output should not be tracked.) |
| M-09 | Many magic strings in tenancy / config (`"PlatformAdmin"`, `"TenantAdmin"`, `"Auth:TenantRouting"`, `"Database:AutoMigrate"`) | grep across modules | Low | Small | Constants. |
| M-10 | XML doc comments are present on `AonikDbContextBase` and several base classes (good). Most service implementations lack any `<summary>` documentation. | Read of the file | Low | Medium | Document public APIs at module boundaries; internal helpers can stay undocumented. |
| M-11 | No `EditorConfig`-driven banned APIs; `IgnoreQueryFilters`, `Task.Result`, `.GetAwaiter().GetResult()` should all be banned via Roslyn analyzer | None | Medium | Small | Add `BannedSymbols.txt` with `M:System.Threading.Tasks.Task`1.get_Result;...` etc. Prevents regression of P-01–P-03 and S-04. |
| M-12 | Tests use unique in-memory database per test (`$"TestDb_{Guid.NewGuid()}"`) — good isolation but slow under parallel runs | Per testing review | Low | Small | Acceptable; consider SQLite-in-memory if InMemory limitations bite. |

---

## 9. Testing Analysis

### Findings

| ID | Finding | Evidence | Severity | Effort | Recommendation |
|---|---|---|---|---|---|
| T-01 | 5 test projects, ~143 test files, xUnit + FluentAssertions, in-memory EF | Per testing review | — | — | Healthy baseline. |
| T-02 | No architecture / contract tests (no `NetArchTest`, no `ArchUnitNET`) | Per testing review (search returned 0) | Medium | Small | Add `NetArchTest` rules: SharedKernel has no module references; Application has no Infrastructure reference; entities have no service references; endpoints belong to a vertical-slice folder; etc. |
| T-03 | No unit tests for `RoleService`, `WebhookService`, `AiRunService`, `AgentToolExecutionService`, `InvoiceService`, `FileBasedPromptStore`, `AiTraceExplorerService` | Per testing review (mapped service → has test) | High | Medium | Cover at least the high-value targets: AiRunService (audit trail of every AI call), AgentToolExecutionService (mutation gate), WebhookService (financial integration). |
| T-04 | `TenantSecurityTests.cs` covers tenant-isolation positive path | [tests/Aonik.Api.Tests/TenantSecurityTests.cs](tests/Aonik.Api.Tests/TenantSecurityTests.cs) | — | — | Good. Expand to cover every `IgnoreQueryFilters` site (T-08). |
| T-05 | `CustomWebApplicationFactory` substitutes auth, email/SMS/push senders, in-memory DB | Per testing review | — | — | Strong integration-test foundation. |
| T-06 | No load / performance tests | None observed | Medium | Medium | A k6 / NBomber script for the highest-traffic endpoints (`POST /payments/intents`, `GET /orders`, AI streaming) would catch regressions like P-01–P-03 before prod. |
| T-07 | No security tests (e.g. cross-tenant access via `IgnoreQueryFilters`, JWT-tampering, input-validation negative cases) | None observed | High | Medium | Add a `Aonik.Api.SecurityTests` project: cross-tenant data access, missing-permission denial, tampered JWT rejection, malformed request bodies. |
| T-08 | Negative-path tests for `IgnoreQueryFilters` use sites are absent | None observed | High | Medium | One test per site that exercises the cross-tenant scenario the bypass is meant to allow, plus one that verifies it doesn't accidentally leak. |
| T-09 | High setup duplication across tests (each test instantiates its own `DbContext`, `ITenantProvider`, `ICurrentUserProvider` test doubles) | Per testing review | Low | Small | A small `TestHarness` factory or `IClassFixture` reduces boilerplate; today's pattern works but is verbose. |
| T-10 | Health-check tests are minimal because the production health check is just `self` | Per testing review | Medium | Small | Once dependency probes exist (O-15-style), add tests that verify they fail correctly when a dependency is down. |

### Critical Test Gaps

1. **`AiRunService` and AI cost-guard.** Every LLM call should produce an `AiRun` row; the kill-switch test exists, but the recording flow is unverified end-to-end.
2. **`AgentToolExecutionService`.** Mutating tools must be wrapped with `ApprovalRequiredAIFunction` per CLAUDE.md. A test that asserts a sample mutating tool **cannot** execute without an approval is missing.
3. **Webhook signature verification** — once webhook receivers exist (S-09).
4. **Cross-tenant access via `IgnoreQueryFilters`** — one test per site (T-08).
5. **Missing-permission negative tests.** `TenantSecurityTests.cs` references `PermissionsDenied_ShouldReturnForbidden`; broaden to a parameterised test that hits every protected endpoint with an under-permissioned client.
6. **Architecture tests** — keep dependency direction honest (T-02).
7. **Validator-presence tests** — once validators exist (S-07), assert that every request DTO has a registered validator.

---

## 10. Observability and Operational Readiness

### Findings

| ID | Finding | Evidence | Severity | Effort | Recommendation |
|---|---|---|---|---|---|
| OB-01 | OpenTelemetry traces, metrics, logs configured via Aspire | [src/Aonik.ServiceDefaults/Extensions.cs](src/Aonik.ServiceDefaults/Extensions.cs) | — | — | Strong baseline. |
| OB-02 | AI-specific telemetry (TelemetryChatClient) emits per-call metrics: latency, TTFT, input/output tokens, estimated cost | [src/Aonik.Ai/Observability/TelemetryChatClient.cs](src/Aonik.Ai/Observability/TelemetryChatClient.cs) | — | — | Healthy. |
| OB-03 | Langfuse + App Insights + OTLP exporters | ServiceDefaults | — | — | Healthy. |
| OB-04 | Health checks return only `self` healthy | Per testing review | Medium | Small | Add probes for SQL Server, Auth0 token endpoint, Qdrant, OpenAI base URL, Application Insights ingestion endpoint. Tag dependency probes `ready`; keep `self` for `live`. |
| OB-05 | No domain-meaningful metrics (payments completed, invoices issued, ledger entries posted, agent successes/failures, AI tokens by tenant) | Per observability review | High | Medium | Add `Counter<long>` per domain event, `Histogram<double>` for latency. Expose under `aonik.domain.*` namespace. Without these, dashboards can only show HTTP / SQL latency, which is meaningless for "are we doing the business?". |
| OB-06 | No append-only audit log queryable for SOC 2 | Per observability review (no audit table found) | High | Medium | See S-12. |
| OB-07 | No alerting rules visible in repo (could exist in IaC / portal, not reviewed) | iac/ folder not reviewed | Medium | Medium | At minimum: alert on `aonik.unhandled_exception` rate, on tenant-isolation violations, on auth failures, on AI cost-guard kill-switch trips, on webhook failures. |
| OB-08 | Correlation: W3C Trace Context auto-propagated; baggage adds `aonik.use_case`, `aonik.ai_run_id`, `langfuse.*` | [src/Aonik.ServiceDefaults/Extensions.cs:81](src/Aonik.ServiceDefaults/Extensions.cs:81); [AguiStreamingEndpoint.cs](src/Aonik.Agents/Endpoints/AguiStreamingEndpoint.cs) | — | — | Good. |
| OB-09 | SQL command text capture is dev-only | ServiceDefaults | — | — | Correct (PII safety). |
| OB-10 | 6 `Console.WriteLine` calls in non-CLI code paths | Per testing review | Low | Small | Replace with `ILogger`; CLI / migrator console output is acceptable, in-memory background-job manager isn't. |
| OB-11 | Tenant-id is **not** stamped on every log scope | Not observed in scan | Medium | Small | Enrich `ILogger` scope with `TenantId`, `UserId`, `RequestId`, `CorrelationId` via middleware. Crucial for incident triage. |
| OB-12 | No runbooks observed in repo | docs/ folder not reviewed in depth | Medium | Medium | At minimum runbooks for: PSP webhook failure, Auth0 outage, AI provider rate limit, tenant isolation alarm, migration failure. |
| OB-13 | No structured exception classification — every unhandled exception is `Aonik.UnhandledException` | [Program.cs:243-271](src/Aonik.Api/Program.cs:243) | Low | Small | Tag with `error.kind = (ValidationError | AuthError | UpstreamError | InternalError)` to drive dashboards. |

---

## 11. Dependency and Package Review

I did not enumerate every NuGet `<PackageReference>` (would require reading 17 csproj files and reconciling versions). The salient observations I can support from what was reviewed:

| ID | Finding | Evidence | Severity | Effort | Recommendation |
|---|---|---|---|---|---|
| D-01 | `Directory.Build.props` exists at repo root, suggesting central package management | [Directory.Build.props](Directory.Build.props) | — | — | Keep central-package-management discipline; verify version pinning. |
| D-02 | Microsoft.EntityFrameworkCore.InMemory used in tests | Per testing review | Medium | Small | InMemory has known divergences from SQL Server (no transactions, different ordering, no real `RowVersion`). For new tests in financial-integrity paths, prefer SQLite-in-memory or Testcontainers SQL Server. |
| D-03 | FastEndpoints, Microsoft.Agent Framework, Quartz, FusionCache, OpenTelemetry, Aspire — all modern, healthy choices | DependencyInjection.cs reads | — | — | Reasonable. |
| D-04 | No GitHub Advanced Security / Dependabot configuration visible in `.github/` | (not reviewed in depth) | High | Small | Required for SOC 2: enable Dependabot security updates, branch-protection-required reviews, SBOM export. |
| D-05 | Speculative: large-feature packages may be unused (e.g. `Microsoft.Extensions.Compliance.*`, `Azure.Monitor.OpenTelemetry.*`) — confirm via `dotnet list package --include-transitive` | None | Low | Small | Periodic prune. |
| D-06 | `Moq 4.20.70` used in Infrastructure tests but not Application tests | Per testing review | Low | Small | Standardise on one mocking story (Moq, NSubstitute, hand-rolled doubles); current mix increases onboarding cost. |

---

## 12. Prioritised Remediation Roadmap

### Immediate Actions: 0 to 2 Weeks

| Priority | Action | Reason | Severity | Effort | Suggested Owner |
|---|---|---|---|---|---|
| 1 | Rotate OpenAI / Auth0 Management / Plaid / Langfuse keys; rewrite git history to remove `appsettings.Development.json` | S-01: real keys present in commit history | Critical | Medium | Security |
| 2 | Add a typed `PermissionDeniedException` and replace the `Program.cs` regex match with an exception filter | S-03: stringly-typed authorisation control | Critical | Small | Backend Engineering |
| 3 | Make JWT-claim tenant resolution authoritative; reject `X-Tenant-Id` on authenticated requests | S-06 | Critical | Small | Backend Engineering |
| 4 | Set explicit `HttpClient.Timeout` on AI provider clients | P-09: indefinite hangs possible | High | Small | Backend Engineering |
| 5 | Fix three sync-over-async sites (`DependencyInjection.cs:290`, `AppInsightsQueryService.cs:76`, `MySpaceSummaryService.cs:69`) | P-01, P-02, P-03 | High | Small | Backend Engineering |
| 6 | Move secrets out of `appsettings.Development.json` to `dotnet user-secrets`; add a pre-commit secrets scanner (gitleaks) | S-01 follow-on | Critical | Small | DevOps |
| 7 | Confirm `Bootstrap.Enabled = false` and `SetupSecret` absent in every non-local environment config | S-18 | High | Small | DevOps |
| 8 | Tighten error-disclosure logic: only `IsDevelopment()`, never `EnvironmentName == "dev"` | S-11 | Medium | Small | Backend Engineering |
| 9 | Add `BannedSymbols.txt` for `Task.Result`, `GetAwaiter().GetResult()`, `IgnoreQueryFilters` | M-11 | Medium | Small | Architecture |

### Near-Term Actions: 2 to 6 Weeks

| Priority | Action | Reason | Severity | Effort | Suggested Owner |
|---|---|---|---|---|---|
| 10 | Wire FluentValidation / FastEndpoints validators on every request DTO; turn validation-by-default on | S-07 | High | Medium | Backend Engineering |
| 11 | Audit and document every `IgnoreQueryFilters()` use site; replace with `IncludeSoftDeleted()` / `AcrossTenants()` narrower extensions; banned via analyzer | S-04 | High | Medium | Architecture |
| 12 | Extract `RolePermissionsConfiguration` shared between `TenantProvisioner` and the startup top-up | S-13, O-05 | High | Small | Backend Engineering |
| 13 | Add `NetArchTest` rules for module dependency direction, layer purity, endpoint placement | T-02 | Medium | Small | Architecture |
| 14 | Decompose `Program.cs` into composition extensions and `IStartupTask` seeders | O-03, C-07 | High | Medium | Backend Engineering |
| 15 | Add unit tests for `AiRunService`, `AgentToolExecutionService`, `WebhookService`, `RoleService` | T-03 | High | Medium | Backend Engineering / QA |
| 16 | Add domain-metric counters (payments / invoices / ledger / agent runs / AI tokens by tenant) | OB-05 | High | Medium | Platform Engineering |
| 17 | Add health-check probes for SQL, Auth0, Qdrant, OpenAI | OB-04 | Medium | Small | Platform Engineering |
| 18 | Add cross-tenant negative tests for every `IgnoreQueryFilters` site | T-08 | High | Medium | QA |
| 19 | Drop the 147-line custom CORS middleware after verifying standard `UseCors` works | O-04 | Medium | Small | Backend Engineering |

### Medium-Term Actions: 6 to 12 Weeks

| Priority | Action | Reason | Severity | Effort | Suggested Owner |
|---|---|---|---|---|---|
| 20 | Append-only audit log via `SaveChangesInterceptor` (entity name, PK, before/after JSON, actor, tenant, request id) | S-12 | High | Medium | Backend Engineering |
| 21 | Decide fate of `Aonik.Application` — populate or remove | O-01, C-02 | High | Medium | Architecture |
| 22 | Resolve domain → agents back-pointing dependency (move contributor contracts to SharedKernel; agents discover) | O-13, C-04 | High | Large | Architecture |
| 23 | Decompose god-files (`CustomerInsightSnapshotGenerator`, `DemoSeedService`, `FinanceDemoSeedContributor`, `AccessManagementService`, `AccountLinkService`, `AguiStreamingEndpoint`) | O-06–O-09, M-01 | High | Large | Backend Engineering |
| 24 | Field-level encryption for PII (KYC, SSN/ID, account numbers) using EF Core value converters + Key Vault wrapping keys | S-10 | High | Large | Security / Platform Engineering |
| 25 | Define webhook signing convention (HMAC + timestamp + nonce); implement before any webhook receiver lands | S-09 | High | Medium | Backend Engineering |
| 26 | Consolidate to a single canonical `DbContext` with module-specific `IModelConfiguration` extensions | O-15 | Medium | Large | Architecture |
| 27 | Replace `ChatThreadHistoryCache` key with tenant-prefixed key | S-08 | Low | Small | Backend Engineering |
| 28 | Add load tests for top-3 endpoints; gate releases on regression | T-06 | Medium | Medium | QA / Platform Engineering |
| 29 | Audit single-impl interfaces; delete those with no test or alternate implementation | O-12 | Medium | Medium | Backend Engineering |

---

## 13. Top 10 Recommended Engineering Actions

| Rank | Action | Why it matters | Expected benefit | Effort | Dependencies |
|---|---|---|---|---|---|
| 1 | **Rotate every secret in `appsettings.Development.json` and rewrite git history.** | Real keys for OpenAI, Auth0 Management, Plaid, Langfuse are in repo history. Anyone who has ever cloned can use them. | Closes the largest, most exploitable security hole. SOC 2 hard-blocker. | Medium | None — start today. |
| 2 | **Make authorisation declarative** via `RequiresPermission` endpoint filter and a typed `PermissionDeniedException`. | Today the 403 mapping is `ex.Message.StartsWith("Permission ")` in Program.cs. Forgetting `EnsurePermissionAsync` silently disables auth. | Eliminates a whole class of "forgot to check perms" defect; makes "this endpoint requires X" reviewable in the endpoint, not the service. | Medium | None. |
| 3 | **Make tenant-id binding authoritative from JWT claims and reject `X-Tenant-Id` on authenticated routes.** Validate the supplied tenant id on anonymous routes. | Today an authenticated user can fall back to header-supplied tenant id; an unauthenticated client can supply any GUID. | Closes tenant-confusion attack surface. | Small | None. |
| 4 | **Add validators (FluentValidation / FastEndpoints) on every request DTO; opt out is the exception, not the default.** | Today there is no input validation layer. Bad input reaches services and EF, producing 500s instead of 400s. | Cheapest depth-of-defence layer; replaces ad-hoc null checks. | Medium | None. |
| 5 | **Audit every `IgnoreQueryFilters()` use site. Replace with narrower extensions (`IncludeSoftDeleted`, `AcrossTenants`). Ban the broad form via Roslyn analyzer.** | 16 use sites; each is a potential cross-tenant leak. | Visible discipline; analyzer prevents regression. | Medium | Cross-tenant negative tests (T-08). |
| 6 | **Move startup seeds and role-permission top-up off the request path** (`IHostedService` / `IStartupTask`) and pull `RolePermissionsConfiguration` into a shared file. | Today seeds run synchronously on every API instance restart against every tenant; the role/permission dictionary is duplicated with a "keep in sync" comment. | 5–15s cold-start improvement; eliminates a documented sync-required-by-comment bug. | Small | None. |
| 7 | **Decompose `Program.cs` into composition extensions** (`UseAonikCors`, `UseAonikExceptionHandler`, `UseAonikDevelopmentStaticFiles`). Drop the custom 147-line CORS middleware after verifying standard `UseCors` works. | Today `Program.cs` is 760 lines mixing 5+ concerns. | Reviewability; drops a stale workaround. | Small | None. |
| 8 | **Append-only audit log via `SaveChangesInterceptor`.** | `CreatedBy/UpdatedBy` is on the row, but there's no diff history. SOC 2 expects evidence of "who changed what when". | Compliance evidence; incident triage; financial-integrity auditing. | Medium | DB schema change. |
| 9 | **Add `NetArchTest` rules** for layer purity (SharedKernel has no module deps; Application has no Infrastructure dep; domain modules don't reference Agents/AI). | Today the dependency direction is encoded only in csproj refs and one CLAUDE.md paragraph; new contributors can't tell why a reference is wrong. | Compile-time architectural enforcement. | Small | None. |
| 10 | **Resolve the back-pointing `Domain → Agents` and `Ai → Domain` dependencies** by moving contributor contracts to SharedKernel and having Agents/Ai discover descriptors. | Today the inversion is upside-down; the domain knows about the agent runtime. | Restores Clean Architecture intent; allows agent runtime to be swapped/unloaded without touching Platform/Finance. | Large | Some refactor scope. |

---

## 14. Appendix

### Files reviewed (sample, not exhaustive)

- [src/Aonik.Api/Program.cs](src/Aonik.Api/Program.cs) (full read; 760 lines)
- [src/Aonik.Application/DependencyInjection.cs](src/Aonik.Application/DependencyInjection.cs) (full read)
- [src/Aonik.Application/Abstractions/Persistence/IAonikDbContext.cs](src/Aonik.Application/Abstractions/Persistence/IAonikDbContext.cs) (head)
- [src/Aonik.SharedKernel/Persistence/AonikDbContextBase.cs](src/Aonik.SharedKernel/Persistence/AonikDbContextBase.cs) (full read; 302 lines)
- [src/Aonik.Api/Middleware/TenantContextMiddleware.cs](src/Aonik.Api/Middleware/TenantContextMiddleware.cs) (full read)
- [src/Aonik.Infrastructure/DependencyInjection.cs](src/Aonik.Infrastructure/DependencyInjection.cs) (sampled around `IUserMemoryService` factory)
- [src/Aonik.Api/appsettings.json](src/Aonik.Api/appsettings.json), [src/Aonik.Api/appsettings.Development.json](src/Aonik.Api/appsettings.Development.json) (full read)
- Solution file `Aonik.sln`, all `*.csproj` under `src/` (project graph)
- Migration folder structure under `src/Aonik.Infrastructure/Migrations/` (count: 50)

The five sub-investigations (architecture, security, performance, testing/observability, overengineering) were performed by parallel agents; their summaries informed this report. Where I could verify a claim directly, I did. Specifically verified by direct read:

- `appsettings.Development.json` does contain real third-party keys (S-01).
- `Aonik.Application/DependencyInjection.cs` is `return services;` (O-01).
- `IAonikDbContext` imports concrete module entity types (C-01).
- `IgnoreQueryFilters()` appears in 16 files (S-04).
- Sync-over-async at `DependencyInjection.cs:290-291` is real (P-01).
- Tenant resolution accepts `X-Tenant-Id` from anonymous requests (S-05).
- No FluentValidation / FastEndpoints validators in production code (S-07).
- JWT validation parameters are correctly configured (S-15).
- Single migration stream is enforced (50 migrations under Infrastructure; none under modules).
- Tenant write-side enforcement is implemented in `AonikDbContextBase` (S-16).

Claims I did **not** independently verify but reproduced from agent findings (these should be confirmed before action):

- The exact line numbers of the `Task.Result` blocks in `AppInsightsQueryService.cs` and `MySpaceSummaryService.cs`.
- The total of 99 interfaces vs. 211 services (the 48% figure).
- Specific test-coverage gaps for individual services not opened for direct read.
- Inter-module `<ProjectReference>` lines (e.g. `Aonik.Ai.csproj` referencing `Aonik.Finance` and `Aonik.Platform`) — agent reported, plausible given DI behaviour, not verified line-by-line in the csproj.

### Patterns observed

- **Healthy:** vertical-slice endpoint folders, anemic-entity discipline, audit-field stamping, single-migration-stream rule enforcement, tenant write-side guard, OpenTelemetry coverage, in-memory-DB integration tests.
- **Mixed:** module DI per module (good for cohesion, doubled by Infrastructure pulling everything anyway), single-impl interfaces (sometimes justified, often not), DTO mapping (manual, inconsistent shape), permission enforcement (procedural, dependent on developer discipline).
- **Unhealthy:** secrets in repo history, stringly-typed authorisation, header-driven anonymous tenant resolution, hollow Application layer, back-pointing domain→agents dependency, 700-line `Program.cs`, 1,500–2,300-line god services.

### Areas requiring deeper manual review

1. **Agent runtime workflow correctness.** I did not exercise `Aonik.Agents/Workflows/`; review by someone with agent-framework expertise is recommended.
2. **AI cost-guard correctness** under concurrent agent runs.
3. **Plaid integration error handling and idempotency** (`AccountLinkService.cs` is 1,215 lines).
4. **Migration content** (50 files; only structural compliance verified, not data-correctness).
5. **Frontend (Aonik.AdminUi, apps/Payabo) authentication and tenant handling.**
6. **Production environment configuration** (appsettings.Production.json, appsettings.Staging.json — not present in repo, presumably injected at deploy).

### Assumptions

- The repo is the source of truth for the deployed system (no significant logic lives only in IaC).
- "dev" environment refers to a deployed (cloud) environment, not local development; local development uses `IsDevelopment()`.
- The `IgnoreQueryFilters` use sites in seeds and worker jobs are intentional (they need cross-tenant data); the use sites in user-facing endpoints (registration, etc.) are the higher-risk ones.
- Microsoft Agent Framework's `ApprovalRequiredAIFunction` actually blocks execution until a human approval (per CLAUDE.md); the test for this remains to be added.

### Open questions for the engineering team

1. **What is the intended role of `Aonik.Application`?** If it's "a layer for cross-module use cases" then it should hold validators + workflow handlers. If it's not, it should be deleted.
2. **Is `Aonik.Infrastructure` truly an infrastructure layer, or is it the composition root?** The naming and the contents disagree.
3. **Why do `Platform` and `Finance` register agent descriptors directly?** Was there an attempted abstraction that didn't land, or is this a deliberate choice? If deliberate, the SharedKernel should formalise the contract.
4. **What's the production setting for `Bootstrap.Enabled` and `Auth:TenantRouting`?** Header-based tenancy in production needs the security treatment described in S-05/S-06.
5. **Has the `appsettings.Development.json` exposure been reported / acknowledged?** The fact that it's now `.gitignore`d suggests someone noticed; key rotation status is the question.
6. **What's the strategy for SOC 2 evidence retrieval?** Mutation history, access reviews, change reviews, dependency hygiene — all need a queryable interface and a documented control.
7. **Is the custom CORS middleware in `Program.cs` still load-bearing on the current FastEndpoints version, or can it be removed?**

---

*End of report — report-01.*
