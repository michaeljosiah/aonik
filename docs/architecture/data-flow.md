# Data Flow

This describes the typical request/response path for API calls in the modular monolith.

## Request Path

1. **HTTP request** arrives at `Aonik.Api` (composition root).
2. **Middleware** runs: authentication, tenant resolution (`TenantContextMiddleware`).
3. **FastEndpoints endpoint** (in the owning module, e.g., `Aonik.Finance/Endpoints/`) validates and maps the request.
4. **Module service** performs business logic using the module's DbContext.
5. **Module DbContext** persists changes (e.g., `FinanceDbContext`).

## Response Path

1. Service returns a DTO.
2. Endpoint maps/returns DTO using `Send.*Async()` helpers.

## Cross-Cutting

- Authentication/authorization runs before endpoints.
- Tenant validation middleware runs after authorization and injects `ITenantProvider`.
- Audit fields (`CreatedAt`, `UpdatedAt`, `CreatedBy`, etc.) are applied in each module's DbContext `SaveChangesAsync()` override.

## Example: Create Invoice

```
[Client]
   ↓ POST /billing/invoices
[Aonik.Api] → middleware (auth, tenant)
   ↓
[Aonik.Finance/Endpoints/Billing/CreateInvoiceEndpoint]
   ↓ CreateInvoiceRequest
[Aonik.Finance/Services/Billing/BillingService]
   ↓ new Invoice { ... }
[FinanceDbContext]
   ↓ await SaveChangesAsync()
[SQL Server Database]
   ↑ InvoiceResponse (201 Created)
[Client]
```

## Example: AI Agent Execution

```
[Client]
   ↓ POST /api/agents/orchestrator/chat
[Aonik.Agents/Endpoints/AgentChatEndpoint]
   ↓
[MasterOrchestratorService]
   ↓ Routes to domain agent (e.g., FinanceDomainAgent)
[FinanceDomainAgent] → uses AIFunction tools
   ↓ Tool calls resolve through module services
[BillingService / LedgerService]
   ↓ Results returned to agent
[MasterOrchestratorService]
   ↑ Chat response
[Client]
```

## Pricing Quote Flow

The pricing quote endpoint returns an FX rate, fees, and totals without mutating financial state.

```mermaid
sequenceDiagram
    autonumber
    participant Client
    participant Api as PricingQuoteEndpoint
    participant Pricing as PricingService
    participant Policy as PricingPolicyService
    participant Fx as FxRateService
    participant Audit as AuditLogWriter

    Client->>Api: POST /pricing/quote
    Api->>Pricing: GetBillPaymentQuoteAsync
    Pricing->>Policy: Resolve policy + limits
    Policy-->>Pricing: FeePolicy + version
    Pricing->>Fx: Get FX rate
    Fx-->>Pricing: FxQuote + timestamp
    Pricing->>Pricing: Calculate amounts + fees
    Pricing->>Audit: Log PricingQuoteCreated
    Pricing-->>Api: PricingQuoteResponse
    Api-->>Client: 200 OK
```
