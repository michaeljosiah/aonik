# Module Organization

AONIK uses a **module-first modular monolith** architecture. Each business domain is a self-contained .NET project that owns its entities, services, endpoints, and persistence.

## Module Anatomy

Every module follows this internal structure:

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
│   └── {Module}AgentRegistration.cs  # IDomainAgentDescriptor implementations
└── {Module}Module.cs            # PUBLIC — IServiceCollection.Add{Module}() extension
```

## Concrete Example (Finance)

```
src/Aonik.Finance/
├── Contracts/
│   ├── Events/
│   │   └── InvoiceIssuedEvent.cs
│   ├── Services/
│   │   └── IBillingService.cs
│   └── Models/
│       └── PartyReadModel.cs
├── Entities/
│   ├── Billing/
│   │   ├── Invoice.cs
│   │   └── InvoiceLine.cs
│   ├── Ledger/
│   │   ├── LedgerAccount.cs
│   │   └── JournalEntry.cs
│   ├── Payments/
│   │   └── PaymentIntent.cs
│   ├── Orders/
│   │   └── Order.cs
│   └── ...
├── Services/
│   ├── Billing/BillingService.cs
│   ├── Ledger/LedgerService.cs
│   └── ...
├── Endpoints/
│   ├── Billing/CreateInvoiceEndpoint.cs
│   ├── Ledger/CreateLedgerAccountEndpoint.cs
│   └── ...
├── Persistence/
│   ├── FinanceDbContext.cs
│   └── Configurations/
│       ├── Billing/InvoiceConfiguration.cs
│       └── ...
├── Agents/
│   ├── Tools/InvoiceTools.cs
│   └── FinanceAgentRegistration.cs
└── FinanceModule.cs
```

## Boundary Rules

1. **Types in `Contracts/` are `public`** — this is the module's API surface
2. **Everything else is `internal`** by default
3. **`Aonik.Api` and test projects** use `[InternalsVisibleTo]` to access internals
4. **Modules reference each other only through Contracts** — no direct entity access across modules
5. **Cross-module data access** uses integration events or contract service interfaces

## Module-Scoped DbContexts

Each module owns a DbContext that maps only its tables:

```csharp
internal class FinanceDbContext : DbContext
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    // ... finance entities only
}
```

All module DbContexts share the same physical SQL Server database. Table schemas use the `dbo` schema (with `SchemaNames.Default` override pattern).

| Module | DbContext |
|--------|-----------|
| Platform | `PlatformDbContext` |
| Finance | `FinanceDbContext` |
| AI | `AiDbContext` |
| Agents | `AgentsDbContext` |

The legacy `AonikDbContext` still exists in Infrastructure for EF migrations compatibility.

## Multi-Tenancy

Each module DbContext applies row-level tenant isolation via the `ITenantScoped` interface and global query filters. The `TenantContextMiddleware` in `Aonik.Api` resolves tenant context from JWT claims or HTTP headers and injects `ITenantProvider` for all modules.

## Inter-Module Communication

### Integration Events

```csharp
// SharedKernel defines the contracts
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IIntegrationEvent;
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

// Finance references Platform contracts to resolve tenant info
// Implementation is internal to Platform module
```

## Module Registration

Each module exposes a public `Add{Module}Module()` extension method called from `Aonik.Api/Program.cs`:

```csharp
// In Program.cs (composition root)
builder.Services.AddPlatformModule(builder.Configuration);
builder.Services.AddFinanceModule(builder.Configuration);
builder.Services.AddAiModule(builder.Configuration);
builder.Services.AddAgentsModule(builder.Configuration);
```
