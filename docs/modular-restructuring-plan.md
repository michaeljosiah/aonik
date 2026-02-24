# AONIK Modular Restructuring Plan

**Status**: Complete  
**Date**: 2026-02-22  
**Completed**: 2026-02-24  
**Supersedes**: `docs/architecture/module-organization.md`  
**Related ADRs**: [001 (superseded)](decisions/001-custom-ai-implementation-vs-maf.md), [004](decisions/004-adopt-microsoft-agent-framework.md), [005](decisions/005-adopt-module-first-modular-monolith.md)

---

## Progress Checklist

### Phase 0: Foundation
- [x] **PR 0.1** — Event Bus & Module Contracts Infrastructure
- [x] **PR 0.2** — Multi-DbContext Migration Infrastructure

### Phase 1: Extract Platform Module
- [x] **PR 1.1** — Scaffold Aonik.Platform Project
- [x] **PR 1.2** — Move Identity & Tenancy Entities
- [x] **PR 1.3** — Move Party & Profile Entities
- [x] **PR 1.4** — Move Compliance, Notifications, Operations
- [x] **PR 1.5** — Move Platform Services & Clean Up

### Phase 2: Extract Finance Module
- [x] **PR 2.1** — Scaffold Aonik.Finance Project
- [x] **PR 2.2** — Move Ledger Sub-Domain
- [x] **PR 2.3** — Move Payments Sub-Domain
- [x] **PR 2.4** — Move Billing, Orders, Pricing, Partners
- [x] **PR 2.5** — Finance Module Clean-Up & Integration Events

### Phase 3: AI & Agent Platform with MAF
- [x] **PR 3.1** — Scaffold Aonik.Ai Module + MAF Integration
- [x] **PR 3.2** — Scaffold Aonik.Agents Module + MAF Agent Base
- [x] **PR 3.3** — Finance Domain Agent + Tools
- [x] **PR 3.4** — Platform Domain Agent + AI Provider Wrappers

### Phase 4: MCP Server Infrastructure
- [x] **PR 4.1** — Finance MCP Server
- [x] **PR 4.2** — Platform MCP Server + MCP Client in Master Agent

### Phase 5: Master Orchestrator & Admin UI
- [x] **PR 5.1** — Master Orchestrator Agent
- [x] **PR 5.2** — MAF Workflows for Multi-Step Operations
- [x] **PR 5.3** — Admin UI Module Extension System

### Phase 6: Clean-Up & Finalization
- [x] **PR 6.1** — Delete Legacy Layers
- [x] **PR 6.2** — Documentation & ADR Updates

---

## 1. Vision

Transform AONIK from a Clean Architecture monolith into a **modular monolith** where each business domain is a self-contained module that:

- Owns its data (module-scoped DbContext)
- Exposes AI agents and MCP servers (using Microsoft Agent Framework)
- Extends the Admin UI with its own pages and navigation
- Communicates with other modules through contracts and events
- Enables a master orchestrator agent to seamlessly route across all domains

---

## 2. Architectural Decisions

| # | Decision | Choice |
|---|----------|--------|
| AD-1 | Module granularity | One `.csproj` per module (entities, services, endpoints, persistence in one project) |
| AD-2 | Inter-module communication | In-process event bus + contract interfaces (integration events, read models, service contracts) |
| AD-3 | Database strategy | Module-scoped DbContexts sharing one physical SQL Server database |
| AD-4 | Boundary enforcement | `internal` types + public `Contracts/` folder; host/test projects use `InternalsVisibleTo` |
| AD-5 | Finance sub-domains | Single `Aonik.Finance` project + `FinanceDbContext`; sub-domains as folders |
| AD-6 | MCP servers | One MCP server per domain module (e.g., `Aonik.Finance.Mcp`) |
| AD-7 | Admin UI extensions | Hybrid: build-time module packages (React components, type-safe) + runtime API manifests (visibility per tenant/user/feature-flag) |
| AD-8 | Old code | Delete as we go (no `[Obsolete]` markers, no parallel codebases) |
| AD-9 | AI framework | **Microsoft Agent Framework (MAF)** for agents, tools, MCP (see ADR-004) |
| AD-10 | Restructuring priority | No significant parallel feature work until restructuring is complete |

---

## 3. Target Module Map

### Platform Core (shared infrastructure)

| Module Project | DbContext | Purpose |
|---------------|-----------|---------|
| `Aonik.Platform` | `PlatformDbContext` | Identity, Tenancy, Party/Profile, Notifications, Operations, Compliance |
| `Aonik.SharedKernel` | _(none)_ | Primitives: `Entity`, `AuditableEntity`, `ITenantScoped`, `Money`, `Result<T>`, `Guard` |

### Domain Modules

| Module Project | DbContext | Purpose |
|---------------|-----------|---------|
| `Aonik.Finance` | `FinanceDbContext` | Ledger, Payments, Billing, Orders, Pricing, Partner Network |
| `Aonik.Ai` | `AiDbContext` | AI Platform (providers, models, routing, prompts, evals, runs) |
| `Aonik.Agents` | `AgentsDbContext` | Agent Framework (agent definitions, proposals, approvals, execution history) |

### Host / Composition

| Project | Purpose |
|---------|---------|
| `Aonik.Api` | Composition root — references all modules, registers endpoints, middleware |
| `Aonik.Worker` | Background jobs (Quartz), references modules as needed |
| `Aonik.AppHost` | .NET Aspire orchestration |
| `Aonik.ServiceDefaults` | Aspire service defaults |
| `Aonik.Migrator` | EF Core migrations for all DbContexts |

### MCP Servers (Phase 4)

| Project | Purpose |
|---------|---------|
| `Aonik.Finance.Mcp` | Finance domain MCP server — exposes finance tools and agents |
| `Aonik.Platform.Mcp` | Platform domain MCP server — exposes platform tools and agents |

### Admin UI

| Project | Purpose |
|---------|---------|
| `Aonik.AdminUi` | React SPA — module extension system for nav, routes, panels |

---

## 4. Target Project Structure

```
src/
├── Aonik.SharedKernel/              # Primitives (unchanged)
│   ├── Entity.cs
│   ├── AuditableEntity.cs
│   ├── ITenantScoped.cs
│   ├── Money.cs
│   └── Result.cs
│
├── Aonik.Platform/                  # Platform module
│   ├── Contracts/                   # PUBLIC: interfaces, events, DTOs
│   │   ├── Events/
│   │   ├── Services/
│   │   └── Models/
│   ├── Entities/
│   │   ├── Identity/
│   │   ├── Party/
│   │   ├── Compliance/
│   │   ├── Notifications/
│   │   └── Operations/
│   ├── Services/
│   ├── Endpoints/
│   ├── Persistence/
│   │   ├── PlatformDbContext.cs
│   │   └── Configurations/
│   └── PlatformModule.cs            # IServiceCollection extension
│
├── Aonik.Finance/                   # Finance module
│   ├── Contracts/                   # PUBLIC
│   │   ├── Events/
│   │   ├── Services/
│   │   └── Models/
│   ├── Entities/
│   │   ├── Ledger/
│   │   ├── Payments/
│   │   ├── Billing/
│   │   ├── Orders/
│   │   ├── Pricing/
│   │   └── Partners/
│   ├── Services/
│   │   ├── Ledger/
│   │   ├── Payments/
│   │   ├── Billing/
│   │   ├── Orders/
│   │   └── Pricing/
│   ├── Endpoints/
│   │   ├── Ledger/
│   │   ├── Billing/
│   │   ├── Payments/
│   │   └── Orders/
│   ├── Persistence/
│   │   ├── FinanceDbContext.cs
│   │   └── Configurations/
│   ├── Agents/                      # MAF agents for finance domain
│   │   ├── Tools/                   # AIFunction tools
│   │   └── FinanceAgent.cs
│   └── FinanceModule.cs
│
├── Aonik.Ai/                        # AI Platform module
│   ├── Contracts/
│   ├── Entities/
│   ├── Services/
│   ├── Endpoints/
│   ├── Persistence/
│   │   ├── AiDbContext.cs
│   │   └── Configurations/
│   ├── Providers/                   # MAF IChatClient provider wrappers
│   └── AiModule.cs
│
├── Aonik.Agents/                    # Agent Framework module
│   ├── Contracts/
│   ├── Entities/
│   ├── Services/
│   ├── Endpoints/
│   ├── Persistence/
│   │   ├── AgentsDbContext.cs
│   │   └── Configurations/
│   ├── Orchestration/               # Master orchestrator, MAF workflows
│   └── AgentsModule.cs
│
├── Aonik.Finance.Mcp/               # Finance MCP server (Phase 4)
│   └── Program.cs
│
├── Aonik.Platform.Mcp/              # Platform MCP server (Phase 4)
│   └── Program.cs
│
├── Aonik.Api/                       # Composition root (slimmed down)
│   ├── Program.cs
│   └── Middleware/
│
├── Aonik.Worker/                    # Background jobs
├── Aonik.AppHost/                   # Aspire orchestration
├── Aonik.ServiceDefaults/           # Aspire defaults
└── Aonik.Migrator/                  # All DbContext migrations

tests/
├── Aonik.Platform.Tests/
├── Aonik.Finance.Tests/
├── Aonik.Ai.Tests/
├── Aonik.Agents.Tests/
└── Aonik.Api.Tests/                 # Integration tests
```

---

## 5. Module Anatomy

Every module follows the same internal structure:

```
Aonik.{Module}/
├── Contracts/                   # PUBLIC surface
│   ├── Events/                  # Integration events (records)
│   ├── Services/                # Service interfaces consumed by other modules
│   └── Models/                  # Shared DTOs / read models
├── Entities/                    # internal — anemic EF entities
├── Services/                    # internal — business logic
├── Endpoints/                   # internal — FastEndpoints
├── Persistence/
│   ├── {Module}DbContext.cs     # internal — module-scoped DbContext
│   └── Configurations/          # internal — EF configurations
├── Agents/                      # internal — MAF agents & tools (if applicable)
│   ├── Tools/                   # AIFunction definitions
│   └── {Module}Agent.cs
└── {Module}Module.cs            # PUBLIC — IServiceCollection.Add{Module}() extension
```

### Boundary Rules

- Types in `Contracts/` are `public` — this is the module's API
- Everything else is `internal` by default
- `Aonik.Api` and test projects use `[InternalsVisibleTo]` to access internals
- Modules reference each other **only through Contracts** (no direct entity access)
- Cross-module data access uses integration events or contract service interfaces

---

## 6. Inter-Module Communication

### In-Process Event Bus

```csharp
// SharedKernel
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IIntegrationEvent;
}

public interface IEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}

// Finance module publishes
public record InvoiceIssuedEvent(Guid InvoiceId, Guid TenantId, decimal Amount, string Currency)
    : IIntegrationEvent;

// Platform module handles
internal class InvoiceIssuedHandler : IEventHandler<InvoiceIssuedEvent>
{
    public async Task HandleAsync(InvoiceIssuedEvent @event, CancellationToken ct)
    {
        // Send notification, update compliance, etc.
    }
}
```

### Contract Service Interfaces

```csharp
// Aonik.Platform/Contracts/Services/ITenantService.cs (public)
public interface ITenantService
{
    Task<TenantInfo?> GetTenantAsync(Guid tenantId, CancellationToken ct = default);
}

// Aonik.Finance references Platform contracts to resolve tenant info
// Implementation is internal to Platform module
```

---

## 7. Database Strategy

### Module-Scoped DbContexts

Each module owns a DbContext that maps only its tables:

```csharp
// Aonik.Finance/Persistence/FinanceDbContext.cs
internal class FinanceDbContext : DbContext
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    // ... finance entities only

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("finance");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);
    }
}
```

### Schema Separation

| Module | Schema |
|--------|--------|
| Platform | `platform` |
| Finance | `finance` |
| Ai | `ai` |
| Agents | `agents` |

All schemas live in the same physical database. Migrations are managed per DbContext via `Aonik.Migrator`.

### Multi-Tenancy

The existing `ITenantScoped` + global query filter pattern continues within each module's DbContext. The `TenantContextMiddleware` remains in `Aonik.Api` and injects `ITenantProvider` for all modules.

---

## 8. Admin UI Extension System

### Hybrid Approach

**Build-time**: Each module publishes a React package with components, routes, and nav items. The Admin UI imports these at build time for full type safety.

**Runtime**: An API manifest endpoint (`/api/admin/manifest`) returns the set of modules, nav sections, and feature flags for the current user/tenant. The UI uses this to control visibility.

### Module Package Structure

```
packages/
├── @aonik/admin-finance/
│   ├── src/
│   │   ├── routes.tsx          # Route definitions
│   │   ├── navigation.ts       # Nav items
│   │   ├── panels.ts           # Workspace panel registry entries
│   │   └── pages/              # Page components
│   ├── package.json
│   └── tsconfig.json
├── @aonik/admin-platform/
│   └── ...
└── @aonik/admin-core/           # Shared types, hooks, utilities
    ├── src/
    │   ├── types.ts             # ModuleManifest, NavItem, RouteConfig
    │   ├── useModules.ts        # Hook to query runtime manifest
    │   └── ModuleRouter.tsx     # Dynamic route registration
    └── package.json
```

### Runtime Manifest API

```json
GET /api/admin/manifest
{
  "modules": [
    {
      "id": "finance",
      "label": "Finance",
      "enabled": true,
      "navSections": [
        {
          "title": "Billing",
          "items": [
            { "label": "Invoices", "path": "/billing/invoices", "icon": "FileText" }
          ]
        }
      ],
      "features": {
        "ai-insights": true,
        "bulk-operations": false
      }
    }
  ]
}
```

---

## 9. AI & Agent Architecture (MAF-Based)

> See [ADR-004: Adopt Microsoft Agent Framework](decisions/004-adopt-microsoft-agent-framework.md) for the full decision record.

### AI Platform Module (`Aonik.Ai`)

Manages LLM providers, model routing, prompt storage, and execution auditing.

```csharp
// Provider registration using MAF's IChatClient
internal class AiProviderService : IAiProviderService
{
    public IChatClient ResolveChatClient(AiRoutePolicy policy)
    {
        // Look up provider config from DB, resolve IChatClient
        // Supports: Azure OpenAI, OpenAI, Anthropic, Ollama, etc.
    }
}

// Stub provider for local dev (no API keys)
internal class StubChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken ct = default)
    {
        return Task.FromResult(new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "Stub AI response — configure a real provider.")));
    }
    // ... other IChatClient members
}
```

### Agent Framework Module (`Aonik.Agents`)

Manages agent definitions, the proposal/approval workflow, and the master orchestrator.

```csharp
// Domain agent using MAF's ChatClientAgent
internal class FinanceDomainAgent
{
    private readonly IChatClient _chatClient;
    private readonly IEnumerable<AITool> _tools;

    public AIAgent CreateAgent()
    {
        return new ChatClientAgent(
            chatClient: _chatClient,
            name: "FinanceAgent",
            instructions: "You are the finance domain agent for AONIK...",
            tools: _tools.ToList());
    }
}

// Tools created via MAF's AIFunctionFactory
public static class FinanceTools
{
    [Description("Get invoice details by ID")]
    public static async Task<InvoiceDto?> GetInvoice(
        Guid invoiceId,
        IBillingService billingService,
        CancellationToken ct = default)
    {
        return await billingService.GetInvoiceAsync(invoiceId, ct);
    }

    public static AIFunction Create(IServiceProvider sp)
    {
        return AIFunctionFactory.Create(
            (Guid invoiceId, CancellationToken ct) =>
                GetInvoice(invoiceId, sp.GetRequiredService<IBillingService>(), ct),
            name: "get_invoice",
            description: "Get invoice details by ID");
    }
}
```

### Proposal Pattern via MAF Middleware

```csharp
// MAF function-calling middleware enforces proposal pattern
public class ProposalMiddleware : IFunctionInvocationMiddleware
{
    public async Task InvokeAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        var riskTier = GetRiskTier(context.Function);
        if (riskTier == RiskTier.High)
        {
            // Create proposal instead of executing directly
            var proposal = await _proposalService.CreateAsync(
                agentRunId: context.GetAgentRunId(),
                action: context.Function.Name,
                parameters: context.Arguments);

            context.Result = new FunctionResult(
                $"Proposal {proposal.Id} created. Awaiting approval.");
            return; // Do NOT call next — blocks execution
        }

        await next(context); // Low/medium risk: execute directly
    }
}
```

### Master Orchestrator (Phase 5)

```csharp
// Master agent composes domain agents as tools
internal class MasterOrchestratorService
{
    public AIAgent CreateMasterAgent(IServiceProvider sp)
    {
        var financeAgent = sp.GetRequiredService<FinanceDomainAgent>().CreateAgent();
        var platformAgent = sp.GetRequiredService<PlatformDomainAgent>().CreateAgent();

        var tools = new List<AITool>
        {
            financeAgent.AsAIFunction("finance", "Handle finance operations"),
            platformAgent.AsAIFunction("platform", "Handle platform operations"),
        };

        return new ChatClientAgent(
            chatClient: _chatClient,
            name: "MasterOrchestrator",
            instructions: "You are the master orchestrator for AONIK...",
            tools: tools);
    }
}
```

---

## 10. MCP Server Architecture (Phase 4)

Each domain module can expose its agents and tools as an MCP server using the official MCP C# SDK.

```csharp
// Aonik.Finance.Mcp/Program.cs
var builder = Host.CreateApplicationBuilder(args);

// Create the finance agent and wrap as MCP tool
var financeAgent = /* resolve from DI */;
var mcpTool = McpServerTool.Create(
    financeAgent.AsAIFunction("finance_agent", "Finance domain agent"));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools([mcpTool]);

var app = builder.Build();
await app.RunAsync();
```

MCP servers can be consumed by:
- External AI tools (Copilot, Claude, etc.)
- The master orchestrator (via `McpClientFactory.CreateAsync()`)
- Other AONIK modules that need to call across domain boundaries via MCP protocol

---

## 11. Phased Migration Plan

### Phase 0: Foundation (2 PRs, ~3-4 days)

Establish the infrastructure for modular architecture without moving business code.

#### PR 0.1 — Event Bus & Module Contracts Infrastructure

**Goal**: Create the shared abstractions that all modules will use.

**Changes**:
- Add to `Aonik.SharedKernel`:
  - `IIntegrationEvent` marker interface
  - `IEventBus` interface
  - `IEventHandler<TEvent>` interface
  - `IModule` interface (with `ConfigureServices` extension method pattern)
- Add `InProcessEventBus` implementation (can live in SharedKernel or a new `Aonik.Infrastructure.Core` if preferred)
- Add DI extension: `services.AddEventBus()` that scans assemblies for `IEventHandler<>` implementations

**Files created/modified**:
```
src/Aonik.SharedKernel/
  Events/IIntegrationEvent.cs
  Events/IEventBus.cs
  Events/IEventHandler.cs
  Events/InProcessEventBus.cs
  Modules/IModule.cs
```

**Validation**: `dotnet build Aonik.sln` passes. Unit tests for `InProcessEventBus`.

**Size**: ~1 day

---

#### PR 0.2 — Multi-DbContext Migration Infrastructure

**Goal**: Establish the pattern for module-scoped DbContexts coexisting with the monolithic one during migration.

**Changes**:
- Create `AonikDbContextBase` abstract class with shared multi-tenancy logic (global query filters, `ITenantScoped` stamping on `SaveChanges`)
- Update `AonikDbContext` to inherit from `AonikDbContextBase`
- Add schema constants: `SchemaNames.Platform`, `SchemaNames.Finance`, `SchemaNames.Ai`, `SchemaNames.Agents`
- Update `Aonik.Migrator` to support multiple DbContext migrations
- Verify existing migrations still apply cleanly

**Files created/modified**:
```
src/Aonik.Infrastructure/Persistence/AonikDbContextBase.cs  (new)
src/Aonik.Infrastructure/Persistence/AonikDbContext.cs       (modified — inherit from base)
src/Aonik.SharedKernel/Persistence/SchemaNames.cs            (new)
src/Aonik.Migrator/Program.cs                                (modified)
```

**Validation**: `dotnet build` + `dotnet test` pass. Database migrations apply without errors.

**Size**: ~1-2 days

---

### Phase 1: Extract Platform Module (5 PRs, ~8-10 days)

Move identity, tenancy, party, compliance, notifications, and operations into `Aonik.Platform`.

#### PR 1.1 — Scaffold Aonik.Platform Project

**Goal**: Create the project, set up `PlatformDbContext`, establish the module pattern.

**Changes**:
- Create `src/Aonik.Platform/Aonik.Platform.csproj` referencing `Aonik.SharedKernel`
- Create `PlatformModule.cs` with `services.AddPlatformModule()` extension
- Create empty `PlatformDbContext` inheriting `AonikDbContextBase` with `platform` schema
- Create `Contracts/` folder with placeholder interfaces
- Add `[InternalsVisibleTo("Aonik.Api")]` and `[InternalsVisibleTo("Aonik.Platform.Tests")]`
- Reference `Aonik.Platform` from `Aonik.Api`
- Call `services.AddPlatformModule()` from `Program.cs`

**Files created**:
```
src/Aonik.Platform/
  Aonik.Platform.csproj
  PlatformModule.cs
  Contracts/  (empty, placeholder README)
  Persistence/PlatformDbContext.cs
```

**Validation**: Solution builds. API starts. No runtime errors.

**Size**: ~1 day

---

#### PR 1.2 — Move Identity & Tenancy Entities

**Goal**: Move `Tenant`, `User`, `Role`, `Permission`, `UserRole` and related entities + EF configs into Platform module.

**Changes**:
- Move entities from `Aonik.Domain/Identity/` and `Aonik.Domain/Tenancy/` to `Aonik.Platform/Entities/Identity/`
- Move EF configurations from `Aonik.Infrastructure/Persistence/Configurations/Identity/` to `Aonik.Platform/Persistence/Configurations/`
- Add DbSets to `PlatformDbContext`
- Remove moved DbSets from `AonikDbContext` (delete as we go)
- Update all service imports to reference new namespace
- Extract `ITenantService` and `IUserService` contract interfaces to `Contracts/Services/`

**Validation**: `dotnet build` + `dotnet test` pass. API starts and identity endpoints work.

**Size**: ~2 days

---

#### PR 1.3 — Move Party & Profile Entities

**Goal**: Move `Person`, `Business`, `KycCase`, `KybCase`, `PartyRole`, `Address`, `Contact` entities.

**Changes**:
- Move from `Aonik.Domain/Party/` to `Aonik.Platform/Entities/Party/`
- Move EF configurations
- Add DbSets to `PlatformDbContext`
- Remove from `AonikDbContext`
- Move related services from `Aonik.Application/Services/Party/` to `Aonik.Platform/Services/`
- Move endpoints from `Aonik.Api/Endpoints/Party/` to `Aonik.Platform/Endpoints/`
- Extract party contract interfaces

**Validation**: Build + test pass. Party API endpoints respond correctly.

**Size**: ~2 days

---

#### PR 1.4 — Move Compliance, Notifications, Operations

**Goal**: Move remaining platform-owned domains.

**Changes**:
- Move Compliance entities (`ComplianceCase`, `SarReport`, `ScreeningResult`, etc.)
- Move Notification entities (`NotificationTemplate`, `NotificationLog`, etc.)
- Move Operations entities (`Task`, `BatchJob`, etc.)
- Move corresponding services, endpoints, EF configs
- Add DbSets to `PlatformDbContext`, remove from `AonikDbContext`

**Validation**: Build + test pass. All moved endpoints respond correctly.

**Size**: ~2 days

---

#### PR 1.5 — Move Platform Services & Clean Up

**Goal**: Move all remaining platform services, update DI, remove dead code from old layers.

**Changes**:
- Move remaining services from `Aonik.Application/Services/{Platform domains}` to `Aonik.Platform/Services/`
- Move remaining endpoints from `Aonik.Api/Endpoints/{Platform domains}` to `Aonik.Platform/Endpoints/`
- Update `PlatformModule.cs` to register all platform services
- Remove platform registrations from `Aonik.Application/DependencyInjection.cs`
- Remove platform registrations from `Aonik.Infrastructure/DependencyInjection.cs`
- Delete empty folders in Domain/Application/Infrastructure/Api
- Publish integration events: `TenantCreatedEvent`, `UserCreatedEvent`, etc.

**Validation**: Build + test pass. No references to moved types remain in old layers.

**Size**: ~1-2 days

---

### Phase 2: Extract Finance Module (5 PRs, ~8-10 days)

Move ledger, payments, billing, orders, pricing, and partner network into `Aonik.Finance`.

#### PR 2.1 — Scaffold Aonik.Finance Project

**Goal**: Create project, `FinanceDbContext`, module registration.

**Changes**:
- Create `src/Aonik.Finance/Aonik.Finance.csproj` referencing `Aonik.SharedKernel` + `Aonik.Platform` (contracts only)
- Create `FinanceModule.cs`, `FinanceDbContext` with `finance` schema
- Add `[InternalsVisibleTo]` attributes
- Wire into `Aonik.Api/Program.cs`

**Validation**: Solution builds. API starts.

**Size**: ~1 day

---

#### PR 2.2 — Move Ledger Sub-Domain

**Goal**: Move `LedgerAccount`, `LedgerEntry`, `LedgerTransaction`, `JournalEntry` and related entities.

**Changes**:
- Move entities to `Aonik.Finance/Entities/Ledger/`
- Move EF configs to `Aonik.Finance/Persistence/Configurations/`
- Move services to `Aonik.Finance/Services/Ledger/`
- Move endpoints to `Aonik.Finance/Endpoints/Ledger/`
- Add DbSets to `FinanceDbContext`, remove from `AonikDbContext`
- Extract `ILedgerService` contract

**Validation**: Build + test pass. Ledger API works.

**Size**: ~2 days

---

#### PR 2.3 — Move Payments Sub-Domain

**Goal**: Move `PaymentIntent`, `PaymentMethod`, `Payout`, `PaymentItem`, `PaymentRail` and related entities.

**Changes**:
- Move entities, services, endpoints, EF configs
- Move to `Aonik.Finance/Entities/Payments/`, `Services/Payments/`, `Endpoints/Payments/`
- Add DbSets, remove from monolith
- Extract `IPaymentService` contract
- Publish `PaymentCapturedEvent`, `PayoutCompletedEvent`

**Validation**: Build + test pass.

**Size**: ~2 days

---

#### PR 2.4 — Move Billing, Orders, Pricing, Partners

**Goal**: Move remaining finance sub-domains.

**Changes**:
- Move Billing entities (`Invoice`, `InvoiceLine`, `RecurringBilling`, etc.)
- Move Orders entities (`Order`, `OrderLine`, `OrderFunding`, etc.)
- Move Pricing entities (`FeeRule`, `FxRate`, `PricingTier`, etc.)
- Move Partner entities (`Correspondent`, `Connector`, `Route`, etc.)
- Move all corresponding services, endpoints, EF configs
- Add DbSets to `FinanceDbContext`, remove from `AonikDbContext`

**Validation**: Build + test pass.

**Size**: ~2 days

---

#### PR 2.5 — Finance Module Clean-Up & Integration Events

**Goal**: Finalize finance module, remove dead code, publish events.

**Changes**:
- Update `FinanceModule.cs` with all service registrations
- Remove finance registrations from old `DependencyInjection.cs` files
- Delete empty domain/application/infrastructure folders
- Add integration events: `InvoiceIssuedEvent`, `PaymentCapturedEvent`, `OrderCreatedEvent`
- Wire event handlers in Platform module (notifications for financial events)
- If `AonikDbContext` is now empty, **delete it** along with `IAonikDbContext`

**Validation**: Build + test pass. No orphaned references. Cross-module events fire correctly.

**Size**: ~1-2 days

---

### Phase 3: AI & Agent Platform with MAF (4 PRs, ~6-8 days)

Extract AI and Agent modules, replacing custom abstractions with Microsoft Agent Framework.

#### PR 3.1 — Scaffold Aonik.Ai Module + MAF Integration

**Goal**: Create AI platform module using MAF's `IChatClient` abstraction instead of custom `IModelProvider`.

**Changes**:
- Create `src/Aonik.Ai/Aonik.Ai.csproj`
  - References: `Aonik.SharedKernel`, `Microsoft.Agents.AI`, `Microsoft.Extensions.AI`
- Move AI entities from `Aonik.Domain/Ai/` to `Aonik.Ai/Entities/`
  - `AiProvider`, `AiModel`, `AiRoutePolicy`, `AiPromptTemplate`, `AiPromptVersion`, `AiRun`, `AiEvaluation`, etc.
- Create `AiDbContext` with `ai` schema, move EF configs
- Replace `IModelProvider` interface with MAF's `IChatClient` resolution:
  ```csharp
  // Aonik.Ai/Contracts/Services/IAiProviderService.cs
  public interface IAiProviderService
  {
      IChatClient ResolveChatClient(string providerKey, CancellationToken ct = default);
      IChatClient ResolveByPolicy(string policyName, CancellationToken ct = default);
  }
  ```
- Create `StubChatClient : IChatClient` for local dev (replaces `StubModelProvider`)
- Move `IPromptStore` / `FileBasedPromptStore` to `Aonik.Ai/Services/`
- Delete old `IModelProvider`, `StubModelProvider`, `IAgentRuntime` interfaces
- Create `AiModule.cs` with `services.AddAiModule()` registration

**NuGet packages added**:
```xml
<PackageReference Include="Microsoft.Agents.AI" Version="*-*" />
<PackageReference Include="Microsoft.Extensions.AI" Version="*" />
```

**Validation**: Build + test pass. `StubChatClient` works without API keys.

**Size**: ~2 days

---

#### PR 3.2 — Scaffold Aonik.Agents Module + MAF Agent Base

**Goal**: Create agent framework module using MAF's `ChatClientAgent` / `AIAgent`.

**Changes**:
- Create `src/Aonik.Agents/Aonik.Agents.csproj`
  - References: `Aonik.SharedKernel`, `Aonik.Ai` (contracts), `Aonik.Platform` (contracts), `Microsoft.Agents.AI`
- Move Agent entities from `Aonik.Domain/Agents/` to `Aonik.Agents/Entities/`
  - `AgentDefinition`, `AgentTool`, `Proposal`, `ProposalApproval`
- Create `AgentsDbContext` with `agents` schema
- Create base agent infrastructure using MAF:
  ```csharp
  // Base class for AONIK domain agents
  internal abstract class AonikDomainAgent
  {
      protected abstract string Name { get; }
      protected abstract string Instructions { get; }
      protected abstract IEnumerable<AITool> GetTools(IServiceProvider sp);

      public AIAgent Build(IChatClient chatClient, IServiceProvider sp)
      {
          return new ChatClientAgent(
              chatClient: chatClient,
              name: Name,
              instructions: Instructions,
              tools: GetTools(sp).ToList());
      }
  }
  ```
- Create `ProposalMiddleware : IFunctionInvocationMiddleware` for the proposal pattern
- Create `AuditMiddleware` for recording all AI tool invocations to `AiRun`
- Create `TenantContextMiddleware` (MAF middleware) to inject tenant context into agent runs
- Move proposal/approval services from Application layer
- Create `AgentsModule.cs`

**Validation**: Build + test pass. Proposal pattern works through MAF middleware.

**Size**: ~2 days

---

#### PR 3.3 — Finance Domain Agent + Tools

**Goal**: Create the Finance domain agent with tools using MAF's `AIFunctionFactory`.

**Changes**:
- Add `Agents/` folder to `Aonik.Finance`:
  ```
  Aonik.Finance/Agents/
    Tools/
      InvoiceTools.cs      # GetInvoice, ListInvoices, CreateInvoice
      LedgerTools.cs       # GetBalance, ListEntries
      PaymentTools.cs      # GetPaymentStatus, InitiatePayment
    FinanceDomainAgent.cs  # Extends AonikDomainAgent
  ```
- Create tools using `AIFunctionFactory.Create()`:
  ```csharp
  internal static class InvoiceTools
  {
      public static AIFunction GetInvoice(IServiceProvider sp) =>
          AIFunctionFactory.Create(
              async (Guid invoiceId, CancellationToken ct) =>
                  await sp.GetRequiredService<IBillingService>().GetInvoiceAsync(invoiceId, ct),
              name: "get_invoice",
              description: "Retrieve invoice details by ID");
  }
  ```
- Register `FinanceDomainAgent` in `FinanceModule.cs`
- Migrate `InvoiceInsightWorkflow` to use MAF agent execution instead of direct `IModelProvider` calls
- Add `[Description]` attributes on all tool parameters for LLM guidance

**Validation**: Build + test pass. Finance agent can be instantiated and tools are callable.

**Size**: ~2 days

---

#### PR 3.4 — Platform Domain Agent + AI Provider Wrappers

**Goal**: Create Platform domain agent, add real LLM provider wrappers alongside stub.

**Changes**:
- Add `Agents/` folder to `Aonik.Platform`:
  ```
  Aonik.Platform/Agents/
    Tools/
      TenantTools.cs
      UserTools.cs
      ComplianceTools.cs
    PlatformDomainAgent.cs
  ```
- Add real `IChatClient` provider implementations in `Aonik.Ai/Providers/`:
  - `OpenAiChatClientProvider.cs` (wraps `OpenAI` NuGet)
  - `AzureOpenAiChatClientProvider.cs` (wraps `Azure.AI.OpenAI`)
  - Provider selection based on `AiRoutePolicy` from DB
- Configuration: providers selected via `appsettings.json` / env vars — stub remains default
- Register all providers in `AiModule.cs`

**NuGet packages added** (to `Aonik.Ai`):
```xml
<PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="*-*" />
<PackageReference Include="Azure.AI.OpenAI" Version="*" />
```

**Validation**: Build + test pass. Stub works by default. Real providers work when configured.

**Size**: ~2 days

---

### Phase 4: MCP Server Infrastructure (2 PRs, ~3-4 days)

Expose domain agents as MCP servers for external consumption.

#### PR 4.1 — Finance MCP Server

**Goal**: Create an MCP server that exposes Finance domain tools and agent.

**Changes**:
- Create `src/Aonik.Finance.Mcp/Aonik.Finance.Mcp.csproj`
  - References: `Aonik.Finance`, `Aonik.Ai`, `ModelContextProtocol`
  - Project type: console application (stdio transport)
- Implement `Program.cs`:
  ```csharp
  var builder = Host.CreateApplicationBuilder(args);

  // Register finance module services
  builder.Services.AddFinanceModule(builder.Configuration);
  builder.Services.AddAiModule(builder.Configuration);

  // Build finance agent and expose as MCP tool
  builder.Services.AddMcpServer()
      .WithStdioServerTransport()
      .WithToolsFromAgent<FinanceDomainAgent>();

  var app = builder.Build();
  await app.RunAsync();
  ```
- Add custom extension `WithToolsFromAgent<T>()` that:
  1. Resolves the domain agent
  2. Calls `agent.AsAIFunction()` to get an `AIFunction`
  3. Wraps with `McpServerTool.Create()` and registers
  4. Also exposes individual tools for granular access
- Add to Aspire `AppHost` orchestration

**NuGet packages**:
```xml
<PackageReference Include="ModelContextProtocol" Version="*" />
```

**Validation**: MCP server starts. Can be connected to via MCP client. Tools are discoverable.

**Size**: ~2 days

---

#### PR 4.2 — Platform MCP Server + MCP Client in Master Agent

**Goal**: Create Platform MCP server. Enable master agent to consume MCP servers.

**Changes**:
- Create `src/Aonik.Platform.Mcp/` (same pattern as Finance MCP)
- Add MCP client capability to `Aonik.Agents`:
  ```csharp
  // Connect to module MCP servers and pull tools
  var mcpClient = await McpClientFactory.CreateAsync(
      new McpClientOptions { ... },
      new StdioClientTransport(new() { Command = "dotnet", Arguments = ["run", "--project", "src/Aonik.Finance.Mcp"] }));
  var tools = await mcpClient.ListToolsAsync();
  // Cast to AITool and pass to master agent
  ```
- This enables the master orchestrator to dynamically discover and call module tools via MCP protocol (not just in-process)
- Add to Aspire `AppHost`

**Validation**: Both MCP servers run. Master agent can discover tools from both servers.

**Size**: ~1-2 days

---

### Phase 5: Master Orchestrator & Admin UI (3 PRs, ~5-7 days)

#### PR 5.1 — Master Orchestrator Agent

**Goal**: Create the master orchestrator that routes across all domain agents.

**Changes**:
- Add `Aonik.Agents/Orchestration/MasterOrchestratorService.cs`:
  ```csharp
  internal class MasterOrchestratorService
  {
      public AIAgent CreateMasterAgent(IServiceProvider sp)
      {
          var financeAgent = sp.GetRequiredService<FinanceDomainAgent>();
          var platformAgent = sp.GetRequiredService<PlatformDomainAgent>();

          var tools = new List<AITool>
          {
              financeAgent.Build(_chatClient, sp).AsAIFunction(
                  "finance", "Delegate to finance domain for billing, payments, ledger, orders"),
              platformAgent.Build(_chatClient, sp).AsAIFunction(
                  "platform", "Delegate to platform domain for users, tenants, compliance"),
          };

          return new ChatClientAgent(
              chatClient: _chatClient,
              name: "MasterOrchestrator",
              instructions: _instructions,
              tools: tools);
      }
  }
  ```
- Add orchestrator endpoint: `POST /api/agents/orchestrator/chat`
- Apply MAF middleware chain: `TenantContext → Audit → Proposal`
- Register in `AgentsModule.cs`

**Validation**: Build + test pass. Orchestrator correctly routes queries to domain agents.

**Size**: ~2 days

---

#### PR 5.2 — MAF Workflows for Multi-Step Operations

**Goal**: Implement complex multi-agent workflows using MAF's graph-based workflow engine.

**Changes**:
- Add `Aonik.Agents/Orchestration/Workflows/`:
  ```
  InvoiceProcessingWorkflow.cs    # Invoice → Compliance check → Payment → Ledger
  OnboardingWorkflow.cs           # KYC → Account setup → Welcome notification
  ReconciliationWorkflow.cs       # Ledger reconciliation across accounts
  ```
- Use MAF's workflow graph with executors, edges, events:
  ```csharp
  var workflow = new AgentWorkflow("invoice_processing")
      .AddAgent(complianceAgent, "compliance_check")
      .AddAgent(paymentAgent, "initiate_payment")
      .AddAgent(ledgerAgent, "record_entry")
      .AddEdge("compliance_check", "initiate_payment", condition: approved)
      .AddEdge("initiate_payment", "record_entry")
      .WithCheckpointing(checkpointStore)
      .WithHumanInTheLoop("compliance_check");
  ```
- Integrate with proposal pattern for human-in-the-loop steps
- Add workflow execution endpoints

**Validation**: Build + test pass. Workflows execute with proper sequencing and checkpointing.

**Size**: ~2 days

---

#### PR 5.3 — Admin UI Module Extension System

**Goal**: Implement the hybrid build-time + runtime extension system for the Admin UI.

**Changes**:
- Create `packages/@aonik/admin-core/`:
  - `types.ts` — `ModuleManifest`, `ModuleRouteConfig`, `ModuleNavConfig`, `ModulePanelConfig`
  - `useModules.ts` — Hook to fetch `/api/admin/manifest` and merge with build-time config
  - `ModuleRouter.tsx` — Dynamic route registration from module configs
  - `ModuleNavigation.tsx` — Dynamic sidebar generation from module nav configs
- Create `packages/@aonik/admin-finance/`:
  - Extract finance-related pages, routes, and nav from current monolithic Admin UI
  - Export `routes`, `navigation`, `panels`
- Create `packages/@aonik/admin-platform/`:
  - Extract platform-related pages, routes, and nav
- Refactor `App.tsx`:
  - Replace hard-coded routes with `<ModuleRouter modules={[financeModule, platformModule]} />`
  - Replace hard-coded nav with `<ModuleNavigation />`
- Refactor `mockData.ts`:
  - Split `navigationSections` into per-module exports
  - Delete `mockData.ts` navigation sections (moved to module packages)
- Add manifest endpoint: `GET /api/admin/manifest` in `Aonik.Api`
- Refactor workspace `registry.ts`:
  - Split panel registrations into per-module exports
  - Use dynamic registration from module packages

**Validation**: Admin UI builds. Navigation renders from module packages. Routes work. Feature flags control visibility.

**Size**: ~3 days

---

### Phase 6: Clean-Up & Finalization (2 PRs, ~2-3 days)

#### PR 6.1 — Delete Legacy Layers

**Goal**: Remove the now-empty old projects.

**Changes**:
- Delete `src/Aonik.Domain/` (all entities moved to modules)
- Delete `src/Aonik.Application/` (all services moved to modules)
- Delete `src/Aonik.Infrastructure/` (persistence, providers moved to modules)
- If any shared infrastructure remains (e.g., caching, external HTTP clients), move to `Aonik.SharedKernel` or a new `Aonik.Infrastructure.Core`
- Update `Aonik.sln` to remove deleted projects
- Update all project references
- Update `Aonik.Api/Program.cs` — should only call module registration methods

**Validation**: Build + test pass. No dead projects in solution. Solution is clean.

**Size**: ~1-2 days

---

#### PR 6.2 — Documentation & ADR Updates

**Goal**: Update all documentation to reflect the new architecture.

**Changes**:
- Update `AGENTS.md` to reflect modular structure
- Update `docs/architecture/module-organization.md` (or replace with this plan as the authoritative doc)
- Update `docs/Testing.md` with new test project locations
- Update `docs/Troubleshooting.md`
- Update `CHANGELOG.md`
- Mark ADR-001 as **Superseded** by ADR-004
- Verify all ADRs are current

**Validation**: All docs are consistent with the new structure.

**Size**: ~1 day

---

## 12. Migration Summary

| Phase | PRs | Est. Days | Description |
|-------|-----|-----------|-------------|
| 0 | 2 | 3-4 | Foundation (event bus, multi-DbContext infra) |
| 1 | 5 | 8-10 | Extract Platform module |
| 2 | 5 | 8-10 | Extract Finance module |
| 3 | 4 | 6-8 | AI & Agent platform with MAF |
| 4 | 2 | 3-4 | MCP server infrastructure |
| 5 | 3 | 5-7 | Master orchestrator & Admin UI extensions |
| 6 | 2 | 2-3 | Clean-up & documentation |
| **Total** | **23** | **~36-46** | |

### Rules for Every PR

1. Solution **must build** after the PR
2. All tests **must pass** (or document intentional test removals)
3. Old code is **deleted**, not deprecated
4. Each PR is **independently reviewable** and mergeable
5. No PR should take more than **3 days**

---

## 13. Risk Mitigation

| Risk | Mitigation |
|------|------------|
| MAF packages are prerelease | Stub provider ensures app runs without real LLM. Pin package versions. Monitor for breaking changes. |
| Cross-module coupling discovered late | Phases 1-2 will surface coupling. Use integration events and contract interfaces to decouple. |
| DbContext migration breaks existing queries | Test all endpoints after each entity move. Use EF schema prefix to avoid table conflicts. |
| Admin UI refactoring breaks UX | Build-time module packages ensure type safety. Runtime manifest is additive (feature flags default to enabled). |
| Team velocity slows | Each PR is small (1-3 days). Restructuring is the sole priority — no parallel feature work. |

---

## 14. Success Criteria

The restructuring is complete when:

1. `Aonik.Domain`, `Aonik.Application`, `Aonik.Infrastructure` projects are **deleted**
2. All business code lives in module projects (`Aonik.Platform`, `Aonik.Finance`, `Aonik.Ai`, `Aonik.Agents`)
3. Each module has its own DbContext with schema isolation
4. Modules communicate only through `Contracts/` and integration events
5. AI agents use MAF (`ChatClientAgent`, `AIFunctionFactory`, middleware)
6. MCP servers expose domain tools for external consumption
7. Master orchestrator routes across all domain agents
8. Admin UI loads navigation, routes, and panels from module packages
9. All tests pass
10. Documentation reflects the new architecture
