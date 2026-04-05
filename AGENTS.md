### You are an expert **AI engineering and financial systems architect** working on **AONIK**, an **AI-powered financial operating system**. You must internalise the following context and treat it as authoritative.

---

## 1. What AONIK Is

AONIK is **not a single application**. It is a **foundational, AI-native financial platform** designed to power multiple products:

- **Payabo** — B2C personal finance (budgets, bills, subscriptions, goals)
- **MyBillAfrica** — B2B billing & collections
- **RemitExchange** — cross-border / remittance capabilities

AONIK itself is the **platform / open-core foundation** beneath these products.

---

## 2. Core Architectural Philosophy (Non-Negotiable)

1. **Ledger is the source of financial truth** (double-entry, immutable)
2. **Orders represent business intent**, not payments
3. **Payments execute intent; ledger proves it**
4. **Agents propose; systems execute**
5. **Every AI action is auditable and policy-governed**
6. **Risk tier determines AI autonomy**
7. **Human approval is explicit for high-risk actions**

You must not suggest designs that violate these principles.

---

## 3. Canonical Platform Scope

### Platform Core Domains

- Identity & Access (Tenants, Users, Roles)
- Party & Profile (Person/Business, KYC/KYB, roles)
- Ledger (double-entry accounting)
- Payments & Money Movement
- Partner Network & Routing (correspondents, connectors)
- Pricing & Policy (fees, FX, limits)
- Compliance & Risk (screening, SAR, audit)
- Operations & Workflow (tasks, batch jobs)
- Notifications

### AI-Native Core

- AI Platform (LLM providers, models, routing, prompts, evals)
- Agent Framework (domain agents, orchestration, proposals)

### Product Domains

- Orders (business intent hub)
- Billing & Invoicing
- Personal Finance

---

## 4. The Role of Orders

An **Order** is the central orchestration object.

Orders:

- Capture *why* money is moving
- Link parties (sender, receiver, payee)
- Reference funding (PaymentIntents)
- Reference fulfilment (Payouts)
- Link to ledger entries, compliance cases, and partner transmissions

Do not collapse Orders into Payments or Invoices.

---

## 5. AI Platform Rules

### LLM Usage

- Multiple providers and models are supported
- No hard-coded model usage
- All calls resolve through **AiRoutePolicy**

### Prompts & Tools

- Prompts, tools, and policies are versioned
- Prompts are immutable once published
- Tools expose domain services safely

### AI Execution

- Every AI execution is recorded as an **AiRun**
- Financially material outputs must reference an AiRunId
- AI should prefer **IDs and references**, not raw PII

---

## 6. Agent Framework Rules

Agents are **constrained, domain-specific actors** built on the Microsoft Agent Framework (MAF).

Agents:

- Are registered via `IDomainAgentDescriptor` interface with `IEnumerable<IDomainAgentDescriptor>` multi-registration
- Reason and plan using `ChatClientAgent` with `AIFunctionFactory.Create()` tools
- Use tools to read and propose
- **Must not directly mutate financial state**

### Human-in-the-Loop (Mandatory for Mutations)

All mutating tools are wrapped with MAF's `ApprovalRequiredAIFunction`:

1. **Read tools** — execute directly (no gate)
2. **Mutating tools** — wrapped with `ApprovalRequiredAIFunction`, requiring human or policy approval

Current mutating tools: `CreateInvoice`, `IssueInvoice`, `CancelInvoice`, `MarkInvoicePaid`, `CreatePaymentIntent`, `CapturePayment`, `CancelPayment`, `CreateLedger`, `CreateAccount`.

Never bypass this flow.

### Orchestrator

`MasterOrchestratorService` composes domain agents as tools via `agent.AsAIFunction()` and uses MAF `AgentSession` for native conversation history tracking. MCP tools from `McpToolProvider` are integrated alongside domain agent tools.

### Audit

`AuditMiddleware` (in `Aonik.Ai.Middleware`) is wired into the `IChatClient` pipeline and records every LLM call as an `AiRun` via `IAiRunWriter`, including token usage from `response.Usage`.

---

## 📋 Quick Reference

- **Target Framework:** .NET 10 (`net10.0`)
- **Architecture:** Module-first modular monolith with anemic domain entities
- **Testing Framework:** xUnit with FluentAssertions
- **API Framework:** FastEndpoints
- **ORM:** Entity Framework Core 10
- **Database:** SQL Server (with InMemory support for testing)

**Important Links:**

- [Troubleshooting Guide](docs/Troubleshooting.md) - Common issues and solutions
- [Testing Guide](docs/Testing.md) - Testing patterns and best practices
- [CHANGELOG](CHANGELOG.md) - Recent changes and version history

---

## 🔧 Build, Test & Development Commands

### Build Commands

```bash
# Build entire solution
dotnet build Aonik.sln

# Build specific project
dotnet build src/Aonik.Api
dotnet build src/Aonik.Application

# Clean build
dotnet clean Aonik.sln && dotnet build Aonik.sln
```

### Test Commands

```bash
# Run all tests
dotnet test Aonik.sln

# Run tests for a specific project
dotnet test tests/Aonik.Application.Tests
dotnet test tests/Aonik.Api.Tests

# Run a single test by filter
dotnet test --filter "FullyQualifiedName~BillingServiceTests.CreateInvoiceAsync_ShouldCreateInvoiceWithLineItems"
dotnet test --filter "DisplayName~CreateInvoice"

# Run tests with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run tests without building
dotnet test --no-build
```

### Database Commands

```bash
# Create migration (ALWAYS against AonikDbContext — the only migration context)
dotnet ef migrations add <MigrationName> --project src/Aonik.Infrastructure --startup-project src/Aonik.Api

# Update database
dotnet ef database update --project src/Aonik.Infrastructure --startup-project src/Aonik.Api

# Remove last migration
dotnet ef migrations remove --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
```

> **⚠️ CRITICAL:** Never generate migrations against module-scoped DbContexts (PlatformDbContext, FinanceDbContext, AiDbContext, AgentsDbContext). Only `AonikDbContext` migrations are applied at startup. See the "Database & Migrations" section below for full rules.

### Run API

```bash
dotnet run --project src/Aonik.Api
# API runs on https://localhost:5001 with Swagger UI at /swagger
```

---

## 📐 Architecture & Project Structure

### Module-First Architecture

- **SharedKernel**: Cross-cutting primitives, interfaces, events, multi-tenancy abstractions
- **Platform Module** (`Aonik.Platform`): Identity, tenancy, party/profile, settings, reference data, compliance, notifications
- **Finance Module** (`Aonik.Finance`): Ledger, payments, orders, billing/invoicing, pricing, partners, personal finance
- **AI Module** (`Aonik.Ai`): AI routing/policies, prompt and model abstractions, AI execution records
- **Agents Module** (`Aonik.Agents`): Domain agent orchestration (`IDomainAgentDescriptor`, `MasterOrchestratorService`), keyed workflow factories, proposal entities
- **Infrastructure**: External adapters and composition support
- **Api / Worker / Migrator**: Composition roots and runtime hosts

### Module Organization

Code is organized by **module-owned vertical slices** (entity + service + endpoint + persistence in each module project):

```
src/Aonik.Finance/Entities/Billing/Invoice.cs
src/Aonik.Finance/Services/Billing/BillingService.cs
src/Aonik.Finance/Endpoints/Billing/CreateInvoiceEndpoint.cs
src/Aonik.Finance/Persistence/Configurations/Billing/InvoiceConfiguration.cs
```

---

## 🗄️ Database & Migrations (Critical — Read Before Touching EF)

### Single Migration Stream

All EF Core migrations go through **`AonikDbContext`** in `src/Aonik.Infrastructure/Migrations/`. This is the **only** context whose migrations are applied at startup (`Program.cs` → `GetRegisteredDbContextTypes` returns only `AonikDbContext`).

**Never generate migrations against module-scoped DbContexts** (`PlatformDbContext`, `FinanceDbContext`, `AiDbContext`, `AgentsDbContext`). They share the same physical database but do not maintain independent migration histories.

### DbContext Hierarchy

```
AonikDbContextBase (abstract — multi-tenancy filters, audit stamping, soft-delete)
├── AonikDbContext        — Canonical context. ALL entity DbSets. Only context with migrations.
├── PlatformDbContext      — Module-scoped (internal). Read/write for Platform services. No migrations.
├── FinanceDbContext        — Module-scoped (internal). Read/write for Finance services. No migrations.
├── AiDbContext            — Module-scoped (internal). Read/write for AI services. No migrations.
└── AgentsDbContext         — Module-scoped (internal). Read/write for Agent services. No migrations.
```

### Migration Rules

1. **Generate migrations only against AonikDbContext:**
   ```bash
   dotnet ef migrations add <Name> --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
   ```
2. **Entity configurations** go in the owning module's `Persistence/Configurations/` folder as `IEntityTypeConfiguration<T>` classes — never inline them in `AonikDbContext.cs`.
3. **New entities** require:
   - A `DbSet<T>` in `AonikDbContext` (and in the relevant module DbContext if needed at runtime)
   - An `IEntityTypeConfiguration<T>` in the module's Configurations folder
   - If the entity is global (no tenant scope, e.g. system jobs): register it in `IsGlobalEntity()` in `AonikDbContext`
4. **Table naming**: All tables use `Ank` prefix in `dbo` schema (e.g. `AnkInvoices`, `AnkUsers`). Use `MapPlatformTable`, `MapFinanceTable`, etc. helper methods.
5. **The `PlatformDbContext` migration folder** (`src/Aonik.Platform/Persistence/Migrations/`) is frozen legacy. Do not add to it.
6. **Design-time factory**: Only `AonikDbContext` has an `IDesignTimeDbContextFactory` (`AonikDbContextFactory`). This is why `dotnet ef` only works with `--project src/Aonik.Infrastructure`.

### Common Mistakes to Avoid

- ❌ Running `dotnet ef migrations add` against `PlatformDbContext` or other module contexts
- ❌ Putting entity Fluent API configuration directly in `AonikDbContext.OnModelCreating()`
- ❌ Adding a `DbSet<T>` to a module context but forgetting `AonikDbContext`
- ❌ Creating an entity without checking if it needs to be in `IsGlobalEntity()`

---

## 🎨 Code Style Guidelines

### General Principles

- **Target Framework**: .NET 10 (`net10.0`)
- **Nullable Reference Types**: Enabled globally (use `string?` for nullable)
- **Implicit Usings**: Enabled (common namespaces auto-imported)
- **Language Version**: Latest C# features

### Naming Conventions

- **Classes/Interfaces**: PascalCase (`Invoice`, `IBillingService`)
- **Methods**: PascalCase (`CreateInvoiceAsync`)
- **Properties**: PascalCase (`CustomerId`, `InvoiceNumber`)
- **Private fields**: `_camelCase` with underscore prefix (`_dbContext`, `_lineItems`)
- **Parameters/locals**: camelCase (`customerId`, `invoiceNumber`)
- **Constants**: PascalCase (`PromptNames.InvoiceInsight`)
- **Async methods**: Suffix with `Async` (`GetInvoiceAsync`)

### File Organization

- **Namespace per file folder**: Match namespace to directory structure
- **One class per file**: Exception for small DTOs/records grouped logically
- **File naming**: Match primary class name (`Invoice.cs`, `BillingService.cs`)

### Import Order

1. System namespaces (`using System;`, `using System.Linq;`)
2. Third-party packages (`using Microsoft.EntityFrameworkCore;`, `using FastEndpoints;`)
3. Project namespaces (`using Aonik.Domain.Billing.Entities;`)
4. Blank line between groups

### Type Usage

- Prefer **explicit types** for clarity: `var` is acceptable when type is obvious from right side
- Use **records** for immutable DTOs: `public record CreateInvoiceRequest(...);`
- Use **nullable annotations**: `Invoice?` for potentially null references
- Prefer **async/await** over `.Result` or `.Wait()`
- Use **CancellationToken** parameters with default value: `CancellationToken cancellationToken = default`

---

## 🏗️ Domain & Entity Patterns

### Domain Entities (Anemic Model)

This project uses **anemic domain entities** - entities are simple data containers without business logic.

- Inherit from `Entity` base class (provides `Guid Id` and equality)
- **Properties**: All properties use public `{ get; set; }`
- **Collections**: Simple `List<T>` properties with public get/set
- **NO constructors**: Rely on object initializers
- **NO methods**: NO business logic, NO state change methods, NO validation methods
- **NO private fields**: All data is exposed as properties

**Example:**

```csharp
public class Invoice : AuditableEntity, ITenantScoped
{
    public Guid InvoiceId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerAccountId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<InvoiceLine> Lines { get; set; } = new();
}
```

### Value Objects

- Use **records** for immutability: `public record Money(decimal Amount, string Currency);`
- Override equality if needed (records have value equality by default)

---

## 📦 Application Layer Patterns

### Services

ALL business logic resides in application services, NOT in entities.

- Interface + implementation: `IBillingService` / `BillingService`
- Constructor injection: Inject `IAonikDbContext` or abstractions
- Return **DTOs**, not domain entities
- Use **async Task<T>** for all I/O operations
- Private mapping methods: `private static InvoiceResponse MapToResponse(Invoice invoice)`
- **Business logic**: State transitions, calculations, validations all in services
- Services manipulate entity properties directly

**Example:**

```csharp
public async Task IssueInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
{
    var invoice = await _dbContext.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

    if (invoice == null)
        throw new InvalidOperationException($"Invoice {invoiceId} not found");

    if (invoice.Status != "Draft")
        throw new InvalidOperationException("Only draft invoices can be issued");

    invoice.Status = "Issued";
    await _dbContext.SaveChangesAsync(cancellationToken);
}
```

### DTOs & Models

- Use **records** for request/response: `public record CreateInvoiceRequest(...);`
- Use **positional parameters**: `new InvoiceResponse(id, customerId, ...)`
- Located in `Application/Models/{Module}/` folders

---

## 🌐 API Layer (FastEndpoints)

### Endpoint Structure

- Inherit from `Endpoint<TRequest, TResponse>` or `EndpointWithoutRequest<TResponse>`
- Override `Configure()`: Set route with `Post("/billing/invoices")`, `AllowAnonymous()`
- Override `HandleAsync()`: Business logic, use `Send.*Async()` methods

### Response Methods

```csharp
await Send.OkAsync(response, ct);                           // 200 OK
await Send.CreatedAtAsync<GetEndpoint>(                     // 201 Created
    routeValues: new { id = response.Id }, 
    responseBody: response, 
    cancellation: ct);
await Send.NotFoundAsync(ct);                               // 404 Not Found
```

- **Never** use `SendAsync()`, `SendCreatedAsync()`, `ResponseAsync()` directly
- Map API contracts to Application DTOs in endpoint handlers

---

## 🧪 Testing Standards

### Test Structure (AAA Pattern)

```csharp
[Fact]
public async Task MethodName_Should_ExpectedBehavior_When_Condition()
{
    // Arrange
    var service = CreateService();

    // Act
    var result = await service.DoSomethingAsync();

    // Assert
    result.Should().NotBeNull();
}
```

### Assertions

- Use **FluentAssertions**: `.Should().Be()`, `.Should().HaveCount()`, `.Should().NotBeNull()`
- Avoid `Assert.Equal()` / `Assert.True()` from xUnit

### Database Tests

- Use **InMemory database** with unique name: `$"TestDb_{Guid.NewGuid()}"`
- Create fresh context per test: `using var context = new AonikDbContext(options);`
- **Always mock ITenantProvider**: Services require tenant context

```csharp
private class TestTenantProvider : ITenantProvider
{
    private readonly Guid _tenantId;
    public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;
    public Guid GetCurrentTenantId() => _tenantId;
    public bool TryGetCurrentTenantId(out Guid tenantId)
    {
        tenantId = _tenantId;
        return true;
    }
}
```

### API Integration Tests

- Infrastructure supports **environment-based database configuration**:
  - Set `UseInMemoryDatabase=true` in configuration to use InMemory database
  - Set `InMemoryDatabaseName` for custom database name
- API tests use `CustomWebApplicationFactory` with `ConfigureAppConfiguration()` to inject test configuration
- Example:
  
  ```csharp
  builder.ConfigureAppConfiguration((context, config) =>
  {
    config.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["UseInMemoryDatabase"] = "true",
        ["InMemoryDatabaseName"] = "TestDb_" + Guid.NewGuid()
    });
  });
  ```

---

## 🚨 Error Handling

- Use **exceptions** for exceptional cases (domain invariant violations)
- Return **null** for "not found" scenarios in queries
- Use **Result<T>** pattern from SharedKernel for operation outcomes (when implemented)
- Throw descriptive exceptions: `throw new InvalidOperationException("Only draft invoices can be issued");`

---

## 📝 Comments & Documentation

- Write **self-documenting code** with clear names
- Add comments only for **non-obvious business logic**
- No commented-out code in commits
- Use XML docs (`///`) for public APIs when helpful

---

## ✅ Pre-Commit Checklist

Before committing code, ensure:

- [ ] `dotnet build Aonik.sln` succeeds with 0 errors
- [ ] `dotnet test Aonik.sln` passes (or document known test failures)
- [ ] No unused usings or variables
- [ ] Nullable annotations correct
- [ ] Async methods have `CancellationToken` parameter with default value
- [ ] FastEndpoints use `Send.*Async()` methods correctly (not `SendAsync`)
- [ ] Domain entities remain anemic (no business logic methods)
- [ ] Tests follow AAA pattern with FluentAssertions
- [ ] Service constructors include ITenantProvider parameter
- [ ] EF Core configurations match actual entity properties
- [ ] Update CHANGELOG.md with significant changes
- [ ] Update relevant documentation if behavior changes

---

## 🛠️ Known Build Issues & Fixes

### Flutter: `PathAccessException` / "Access is denied" during `flutter build apk`

**Symptom:** The build fails with an error like:

```
PathAccessException: Cannot copy file to '...\build\app\intermediates\flutter\debug\flutter_assets\assets/images/slider-img-01.png'
(OS Error: Access is denied, errno = 5)
```

**Cause:** A stale or locked file in the Gradle build cache on Windows. Typically caused by an emulator, IDE, antivirus, or another process holding a file handle in the `build/` directory.

**Fix:** Manually copy the blocked file into the target directory, then re-run the build:

```bash
# 1. Copy the file that failed (adjust the filename from the error message)
cp "apps/payabo_mobile/assets/images/<filename>.png" \
   "apps/payabo_mobile/build/app/intermediates/flutter/debug/flutter_assets/assets/images/<filename>.png"

# 2. Re-run the build
flutter build apk --debug
```

If multiple files are blocked, or the issue persists, try a full clean first:

```bash
flutter clean && flutter pub get && flutter build apk --debug
```

If the clean build still hits the same error, the manual copy step above resolves it.

---

## 📚 Additional Resources

- **[Testing Guide](docs/Testing.md)** - Comprehensive testing patterns and examples
- **[Troubleshooting Guide](docs/Troubleshooting.md)** - Common errors and solutions
- **[CHANGELOG](CHANGELOG.md)** - Version history and recent changes
- **[README](README.md)** - Project overview and getting started

---

## 🔄 Recent Updates (January 2025)

### Build System Fixes

- Resolved NuGet package version conflicts for .NET 10
- Fixed `AddHttpContextAccessor` dependency issues
- Updated all Microsoft.Extensions packages to 10.0.1

### Entity Framework Updates

- Corrected all EF Core configuration files to match actual entity properties
- Removed references to non-existent properties in configurations
- Updated collection mappings (e.g., `LineItems` → `Lines`)

### Testing Infrastructure

- Added `TestTenantProvider` pattern for service tests
- Removed obsolete domain tests (entities are anemic)
- Fixed all test constructor calls to include required dependencies

See [CHANGELOG.md](CHANGELOG.md) for complete details.
