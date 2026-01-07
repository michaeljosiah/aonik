# AONIK Architecture

**Version:** 0.1.0 (Early Development)  
**Last Updated:** January 2026

---

## Table of Contents

1. [Overview](#overview)
2. [Architectural Principles](#architectural-principles)
3. [System Architecture](#system-architecture)
4. [Project Structure](#project-structure)
5. [Core Layers](#core-layers)
6. [Domain Model](#domain-model)
7. [AI Integration Architecture](#ai-integration-architecture)
8. [Data Flow Patterns](#data-flow-patterns)
9. [API Design](#api-design)
10. [Testing Strategy](#testing-strategy)
11. [Technology Stack](#technology-stack)
12. [Extensibility & Future Directions](#extensibility--future-directions)

---

## Overview

AONIK is an **AI-native financial infrastructure platform** built using **Clean Architecture** principles with a **modular monolith** approach. The system is designed from the ground up to support intelligent financial operations while maintaining clear boundaries between core financial primitives and AI-driven capabilities.

### Key Characteristics

- **AI-First Design**: AI capabilities are not bolted on—they are integrated into the core architecture
- **Domain-Driven Design**: Business logic is organized around financial domain concepts
- **Clean Architecture**: Clear separation of concerns with dependency inversion
- **Modular Monolith**: Organized by business capabilities (Billing, Ledger, Payments, AI) with vertical slices
- **Explicit & Auditable**: All AI operations are traceable and explainable for financial compliance

---

## Architectural Principles

### 1. **Separation of Concerns**
Each layer has a distinct responsibility:
- **Domain**: Business rules and invariants
- **Application**: Use cases and workflows
- **Infrastructure**: Technical implementation details
- **API**: HTTP interface and presentation

### 2. **Dependency Inversion**
Dependencies flow inward:
```
API → Application → Domain
         ↓
   Infrastructure
```
Infrastructure and API depend on abstractions defined in Application/Domain.

### 3. **Explicitness Over Magic**
- AI operations are explicit workflows, not hidden black boxes
- Financial state changes are tracked and auditable
- Configuration and prompts are versioned and stored as code

### 4. **Testability First**
- Business logic is independent of infrastructure
- In-memory database support for fast integration tests
- AI providers are abstracted for testing with stubs

### 5. **Modularity by Domain**
Code is organized by business capability, not technical layer:
```
Billing/
  ├── Domain (Entities, Value Objects)
  ├── Application (Services, DTOs)
  └── API (Endpoints)
```

---

## System Architecture

### High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                        API Layer                             │
│                    (FastEndpoints)                           │
│   /billing  /ledger  /payments  /ai  /health                │
└────────────────┬────────────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────────────┐
│                   Application Layer                          │
│                                                              │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────┐     │
│  │   Billing   │  │    Ledger    │  │   Payments    │     │
│  │   Service   │  │   Service    │  │    Service    │     │
│  └─────────────┘  └──────────────┘  └───────────────┘     │
│                                                              │
│  ┌─────────────────────────────────────────────────┐       │
│  │         AI Insights Service                      │       │
│  │  ┌──────────────────┐  ┌─────────────────┐     │       │
│  │  │ Invoice Insight  │  │  Future: Cash   │     │       │
│  │  │    Workflow      │  │  Flow Workflow  │     │       │
│  │  └──────────────────┘  └─────────────────┘     │       │
│  └─────────────────────────────────────────────────┘       │
│                                                              │
│  Abstractions: IAonikDbContext, IModelProvider, etc.       │
└────────────────┬────────────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────────────┐
│                    Domain Layer                              │
│                                                              │
│  Billing/         Ledger/        Payments/      Ai/        │
│  ├── Invoice      ├── Ledger     ├── Payment   ├── Insight │
│  ├── LineItem     │   Account    │   Intent    ├── Signal  │
│  └── Enums        ├── Journal    └── Enums     └── Enums   │
│                   │   Entry                                 │
│                   └── Enums                                 │
└─────────────────────────────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────────────┐
│                 Infrastructure Layer                         │
│                                                              │
│  ┌──────────────────┐  ┌───────────────────────┐           │
│  │  Persistence     │  │    AI Providers       │           │
│  │  - EF Core       │  │  - StubModelProvider  │           │
│  │  - SQL Server    │  │  - PromptStore        │           │
│  │  - Migrations    │  │  - Future: OpenAI     │           │
│  └──────────────────┘  └───────────────────────┘           │
└─────────────────────────────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────────────┐
│                    SharedKernel                              │
│                                                              │
│     Entity, Money, Guard, Result<T>, Common Primitives     │
└─────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
Aonik.sln
│
├── src/
│   ├── Aonik.SharedKernel         # Common primitives
│   │   └── Primitives/
│   │       ├── Entity.cs          # Base entity with ID and equality
│   │       ├── Money.cs           # Value object for money
│   │       └── Guard.cs           # Input validation helpers
│   │
│   ├── Aonik.Domain               # Business entities and logic
│   │   ├── Billing/
│   │   │   └── Entities/
│   │   │       ├── Invoice.cs
│   │   │       ├── InvoiceLineItem.cs
│   │   │       └── InvoiceStatus.cs
│   │   ├── Ledger/
│   │   │   └── Entities/
│   │   │       ├── LedgerAccount.cs
│   │   │       └── JournalEntry.cs
│   │   ├── Payments/
│   │   │   └── Entities/
│   │   │       └── PaymentIntent.cs
│   │   └── Ai/
│   │       └── Entities/
│   │           ├── Insight.cs      # AI-generated insights
│   │           └── Signal.cs       # Anomaly/event signals
│   │
│   ├── Aonik.Application          # Use cases and orchestration
│   │   ├── Services/
│   │   │   ├── Billing/
│   │   │   │   ├── IBillingService.cs
│   │   │   │   └── BillingService.cs
│   │   │   ├── Ledger/
│   │   │   │   ├── ILedgerService.cs
│   │   │   │   └── LedgerService.cs
│   │   │   └── Ai/
│   │   │       ├── IAiInsightsService.cs
│   │   │       ├── AiInsightsService.cs
│   │   │       └── Workflows/
│   │   │           └── InvoiceInsightWorkflow.cs
│   │   ├── Models/                # DTOs/Request/Response objects
│   │   │   ├── Billing/
│   │   │   ├── Ledger/
│   │   │   └── Ai/
│   │   └── Abstractions/          # Interfaces for infrastructure
│   │       ├── Persistence/
│   │       │   └── IAonikDbContext.cs
│   │       └── Ai/
│   │           ├── IModelProvider.cs
│   │           ├── IPromptStore.cs
│   │           └── IAgentRuntime.cs
│   │
│   ├── Aonik.Infrastructure       # External concerns
│   │   ├── Persistence/
│   │   │   ├── AonikDbContext.cs
│   │   │   ├── Configurations/    # EF Core entity configs
│   │   │   └── Migrations/        # Database migrations
│   │   ├── Ai/
│   │   │   ├── Providers/
│   │   │   │   └── StubModelProvider.cs
│   │   │   └── Prompting/
│   │   │       ├── FileBasedPromptStore.cs
│   │   │       └── Templates/     # Prompt markdown files
│   │   └── DependencyInjection.cs
│   │
│   ├── Aonik.Api                  # HTTP interface
│   │   ├── Endpoints/
│   │   │   ├── Health/
│   │   │   ├── Billing/
│   │   │   │   ├── CreateInvoiceEndpoint.cs
│   │   │   │   └── GetInvoiceEndpoint.cs
│   │   │   ├── Ledger/
│   │   │   │   ├── CreateLedgerAccountEndpoint.cs
│   │   │   │   └── AddJournalEntryEndpoint.cs
│   │   │   └── Ai/
│   │   │       └── GenerateInvoiceInsightEndpoint.cs
│   │   ├── Contracts/             # API request/response contracts
│   │   └── Program.cs
│   │
│   └── Aonik.Worker               # Background jobs (future)
│
└── tests/
    ├── Aonik.Domain.Tests
    ├── Aonik.Application.Tests
    ├── Aonik.Infrastructure.Tests
    └── Aonik.Api.Tests            # Integration tests with TestServer
```

---

## Core Layers

### SharedKernel

**Purpose:** Common building blocks shared across all projects.

**Key Components:**
- `Entity`: Base class for all domain entities with GUID identity and value equality
- `Money`: Value object for representing monetary amounts with currency
- `Guard`: Static helper for input validation
- `Result<T>`: Discriminated union for operation outcomes (planned)

**Dependencies:** None (standalone)

---

### Domain Layer

**Purpose:** Encapsulate business rules and invariants. Pure business logic with no infrastructure dependencies.

**Characteristics:**
- Entities inherit from `Entity` base class
- Private setters on all properties
- Public behavior methods that enforce invariants
- Private parameterless constructor for EF Core
- Collections exposed as `IReadOnlyCollection<T>`

**Example: Invoice Entity**
```csharp
public class Invoice : Entity
{
    public Guid CustomerId { get; private set; }
    public string InvoiceNumber { get; private set; }
    public InvoiceStatus Status { get; private set; }
    
    private readonly List<InvoiceLineItem> _lineItems = new();
    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();
    
    public void MarkAsIssued()
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("Only draft invoices can be issued");
        
        Status = InvoiceStatus.Issued;
    }
}
```

**Domain Modules:**
- **Billing**: Invoice, InvoiceLineItem
- **Ledger**: LedgerAccount, JournalEntry
- **Payments**: PaymentIntent
- **AI**: Insight (AI-generated summaries), Signal (anomaly detection)

**Dependencies:** SharedKernel only

---

### Application Layer

**Purpose:** Orchestrate domain logic and coordinate infrastructure. Implement use cases and workflows.

**Characteristics:**
- Services implement interfaces (e.g., `IBillingService`)
- Use `IAonikDbContext` abstraction for persistence
- Return DTOs, never domain entities
- All async operations accept `CancellationToken`
- Private mapping methods convert entities to DTOs

**Service Pattern:**
```csharp
public class BillingService : IBillingService
{
    private readonly IAonikDbContext _dbContext;
    
    public BillingService(IAonikDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<InvoiceResponse> CreateInvoiceAsync(
        CreateInvoiceRequest request, 
        CancellationToken cancellationToken = default)
    {
        var invoice = new Invoice(...);
        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(invoice);
    }
}
```

**Key Abstractions:**
- `IAonikDbContext`: Database access
- `IModelProvider`: AI model inference
- `IPromptStore`: Versioned prompt templates
- `IAgentRuntime`: Future agent orchestration (planned)

**Dependencies:** Domain, SharedKernel

---

### Infrastructure Layer

**Purpose:** Implement technical details for persistence, external APIs, and AI providers.

**Key Implementations:**

#### Persistence
- **AonikDbContext**: EF Core implementation of `IAonikDbContext`
- **Entity Configurations**: Fluent API for table schema, indexes, constraints
- **Migrations**: Version-controlled database schema changes
- **Database Support**: SQL Server (production), InMemory (testing)

#### AI Providers
- **StubModelProvider**: Returns placeholder text for development/testing
- **FileBasedPromptStore**: Loads versioned prompt templates from markdown files
- **Future**: OpenAI, Anthropic, Azure OpenAI integrations

**Configuration-Based Database Selection:**
```csharp
// Infrastructure/DependencyInjection.cs
var useInMemory = configuration["UseInMemoryDatabase"];

if (useInMemory == "true")
{
    services.AddDbContext<AonikDbContext>(options =>
        options.UseInMemoryDatabase(configuration["InMemoryDatabaseName"] ?? "AonikDb"));
}
else
{
    services.AddDbContext<AonikDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
}
```

**Dependencies:** Application, Domain, SharedKernel

---

### API Layer

**Purpose:** Expose HTTP endpoints using FastEndpoints.

**Endpoint Pattern:**
```csharp
public class CreateInvoiceEndpoint : Endpoint<CreateInvoiceRequest, InvoiceResponse>
{
    private readonly IBillingService _billingService;
    
    public CreateInvoiceEndpoint(IBillingService billingService)
    {
        _billingService = billingService;
    }
    
    public override void Configure()
    {
        Post("/billing/invoices");
        AllowAnonymous();
    }
    
    public override async Task HandleAsync(
        CreateInvoiceRequest req, 
        CancellationToken ct)
    {
        var response = await _billingService.CreateInvoiceAsync(req, ct);
        await Send.CreatedAtAsync<GetInvoiceEndpoint>(
            routeValues: new { id = response.Id }, 
            responseBody: response, 
            cancellation: ct);
    }
}
```

**Response Methods:**
- `Send.OkAsync(response, ct)` → 200 OK
- `Send.CreatedAtAsync<T>(...)` → 201 Created with Location header
- `Send.NotFoundAsync(ct)` → 404 Not Found
- `Send.NoContentAsync(ct)` → 204 No Content

**Endpoints:**
- `GET /health` - Health check
- `POST /billing/invoices` - Create invoice
- `GET /billing/invoices/{id}` - Get invoice
- `POST /ledger/accounts` - Create ledger account
- `POST /ledger/entries` - Add journal entry
- `POST /ai/insights/invoice/{id}` - Generate AI insight

**Dependencies:** Application, Infrastructure

---

## Domain Model

### Billing Module

**Invoice**
- `Id`: Guid (primary key)
- `CustomerId`: Guid (customer reference)
- `InvoiceNumber`: string (unique identifier)
- `Currency`: string (ISO 4217 code, e.g., "USD")
- `TotalAmount`: decimal (precision 19,4)
- `Status`: InvoiceStatus enum (Draft, Issued, Paid, Cancelled)
- `IssuedUtc`: DateTime
- `DueUtc`: DateTime
- `LineItems`: Collection of InvoiceLineItem

**Business Rules:**
- Invoices start in Draft status
- Only Draft invoices can be issued
- Only Issued invoices can be marked as paid
- Paid invoices cannot be cancelled
- Total amount is automatically recalculated when line items change

**InvoiceLineItem**
- `Id`: Guid
- `InvoiceId`: Guid (foreign key)
- `Description`: string
- `Quantity`: int
- `UnitPrice`: decimal (precision 19,4)
- `LineTotal`: decimal (computed: Quantity × UnitPrice)

---

### Ledger Module

**LedgerAccount**
- `Id`: Guid
- `Name`: string (account name)
- `Currency`: string
- `CreatedUtc`: DateTime

**JournalEntry**
- `Id`: Guid
- `AccountId`: Guid (foreign key)
- `Amount`: decimal (precision 19,4, positive or negative)
- `Currency`: string
- `EntryUtc`: DateTime
- `Reference`: string? (optional reference number)
- `Description`: string? (optional description)

**Business Rules:**
- Journal entries are immutable once created
- Negative amounts represent debits, positive represent credits
- All entries must reference a valid ledger account

---

### Payments Module

**PaymentIntent** (basic implementation)
- `Id`: Guid
- `Amount`: decimal (precision 19,4)
- `Currency`: string
- `Status`: PaymentStatus enum (Pending, Succeeded, Failed, Cancelled)
- `Reference`: string? (external payment reference)
- `CreatedUtc`: DateTime

---

### AI Module

**Insight**
- `Id`: Guid
- `SubjectType`: string (e.g., "Invoice", "Customer")
- `SubjectId`: Guid (reference to subject entity)
- `Title`: string (insight title)
- `Summary`: string (AI-generated summary)
- `CreatedUtc`: DateTime

**Purpose:** Store AI-generated insights about financial entities.

**Signal** (future use)
- `Id`: Guid
- `Type`: string (e.g., "Anomaly", "Fraud", "CashFlowRisk")
- `Severity`: string (e.g., "Low", "Medium", "High")
- `Message`: string
- `CreatedUtc`: DateTime

**Purpose:** Capture AI-detected events, anomalies, or alerts.

---

## AI Integration Architecture

### Design Philosophy

AONIK's AI integration is **explicit, versioned, and auditable**:

1. **Workflows, Not Magic**: AI operations are explicit workflow classes
2. **Versioned Prompts**: Prompts are stored as versioned markdown files in source control
3. **Provider Abstraction**: AI models are abstracted behind `IModelProvider`
4. **Stored Insights**: All AI outputs are saved as domain entities for audit trails

### AI Workflow Pattern

**Example: InvoiceInsightWorkflow**

```csharp
public class InvoiceInsightWorkflow
{
    private readonly IAonikDbContext _dbContext;
    private readonly IPromptStore _promptStore;
    private readonly IModelProvider _modelProvider;
    
    public async Task<InsightResponse> ExecuteAsync(
        Guid invoiceId, 
        CancellationToken cancellationToken = default)
    {
        // Step 1: Load invoice data
        var invoice = await _dbContext.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        
        // Step 2: Load versioned prompts
        var systemPrompt = await _promptStore.LoadPromptAsync(
            PromptNames.InvoiceInsight, "v1", "system", cancellationToken);
        var userPromptTemplate = await _promptStore.LoadPromptAsync(
            PromptNames.InvoiceInsight, "v1", "user", cancellationToken);
        
        // Step 3: Inject data into prompt
        var userPrompt = userPromptTemplate.Replace("{{INVOICE_DATA}}", invoiceData);
        
        // Step 4: Call AI model
        var completion = await _modelProvider.GenerateCompletionAsync(
            systemPrompt, userPrompt, cancellationToken);
        
        // Step 5: Save insight as domain entity
        var insight = new Insight("Invoice", invoiceId, title, completion);
        _dbContext.Insights.Add(insight);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return MapToResponse(insight);
    }
}
```

### AI Abstractions

**IModelProvider**
```csharp
public interface IModelProvider
{
    Task<string> GenerateCompletionAsync(
        string systemPrompt, 
        string userPrompt, 
        CancellationToken cancellationToken = default);
}
```

**Implementations:**
- `StubModelProvider`: Returns "This is a stub AI insight" for development
- Future: `OpenAiModelProvider`, `AnthropicModelProvider`, `AzureOpenAiModelProvider`

**IPromptStore**
```csharp
public interface IPromptStore
{
    Task<string> LoadPromptAsync(
        string promptName, 
        string version, 
        string partName, 
        CancellationToken cancellationToken = default);
}
```

**Implementation:** `FileBasedPromptStore`
- Prompts stored at: `Infrastructure/Ai/Prompting/Templates/{PromptName}/{Version}/{PartName}.md`
- Example: `Templates/InvoiceInsight/v1/system.md`

### Future AI Capabilities

Planned workflows:
- **CashFlowForecastWorkflow**: Predict future cash flow based on historical data
- **AnomalyDetectionWorkflow**: Detect unusual transactions or patterns
- **ReconciliationWorkflow**: Match transactions across systems
- **BudgetRecommendationWorkflow**: Suggest budget adjustments
- **CustomerRiskWorkflow**: Assess customer payment risk

---

## Data Flow Patterns

### 1. Create Entity Flow

```
[Client] 
   ↓ POST /billing/invoices
[CreateInvoiceEndpoint] 
   ↓ CreateInvoiceRequest
[BillingService]
   ↓ new Invoice(...)
[Domain Entity]
   ↓ _dbContext.Invoices.Add(invoice)
[AonikDbContext]
   ↓ await SaveChangesAsync()
[SQL Server Database]
   ↑ InvoiceResponse
[Client]
```

### 2. Query Entity Flow

```
[Client]
   ↓ GET /billing/invoices/{id}
[GetInvoiceEndpoint]
   ↓ invoiceId
[BillingService]
   ↓ _dbContext.Invoices.Include(...).FirstOrDefaultAsync()
[AonikDbContext]
   ↓ SQL Query
[SQL Server Database]
   ↑ Invoice entity (or null)
[BillingService]
   ↓ MapToResponse(invoice)
[GetInvoiceEndpoint]
   ↑ InvoiceResponse (200 OK) or 404 Not Found
[Client]
```

### 3. AI Insight Generation Flow

```
[Client]
   ↓ POST /ai/insights/invoice/{id}
[GenerateInvoiceInsightEndpoint]
   ↓ invoiceId
[AiInsightsService]
   ↓ invoiceId
[InvoiceInsightWorkflow]
   ↓ Load invoice from DB
[AonikDbContext]
   ↓ Load versioned prompts
[FileBasedPromptStore]
   ↓ systemPrompt + userPrompt
[IModelProvider (Stub/OpenAI)]
   ↑ AI completion text
[InvoiceInsightWorkflow]
   ↓ new Insight(...), Save to DB
[AonikDbContext]
   ↑ InsightResponse
[Client]
```

---

## API Design

### Endpoint Conventions

**Naming:**
- Use plural nouns for collections: `/invoices`, `/accounts`
- Use singular for specific resources: `/invoices/{id}`
- Group by domain module: `/billing`, `/ledger`, `/payments`, `/ai`

**HTTP Methods:**
- `POST` for creating resources
- `GET` for reading resources
- `PUT` for full updates (future)
- `PATCH` for partial updates (future)
- `DELETE` for deletions (future)

**Status Codes:**
- `200 OK` - Successful read
- `201 Created` - Successful creation with Location header
- `204 No Content` - Successful operation with no response body
- `400 Bad Request` - Validation failure
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Unhandled exception

**Response Format:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "invoiceNumber": "INV-2024-001",
  "currency": "USD",
  "totalAmount": 1500.00,
  "status": "Issued",
  "issuedUtc": "2024-01-15T10:30:00Z",
  "dueUtc": "2024-02-15T10:30:00Z",
  "lineItems": [
    {
      "id": "8d3b2e1a-4c7f-4b9e-8a2d-3e5f6a7b8c9d",
      "description": "Consulting Services",
      "quantity": 10,
      "unitPrice": 150.00,
      "lineTotal": 1500.00
    }
  ]
}
```

---

## Testing Strategy

### Test Pyramid

```
         ╱╲
        ╱  ╲     E2E Tests (Future)
       ╱────╲
      ╱      ╲   Integration Tests (5 tests)
     ╱────────╲
    ╱          ╲ Unit Tests (6 tests)
   ╱────────────╲
```

### Unit Tests

**Target:** Domain entities, application services (with mocked dependencies)

**Framework:** xUnit + FluentAssertions

**Example:**
```csharp
[Fact]
public void MarkAsIssued_ShouldChangeStatusToIssued_WhenInvoiceIsDraft()
{
    // Arrange
    var invoice = new Invoice(Guid.NewGuid(), "INV-001", "USD", DateTime.UtcNow);
    
    // Act
    invoice.MarkAsIssued();
    
    // Assert
    invoice.Status.Should().Be(InvoiceStatus.Issued);
}
```

**Tests:**
- `Aonik.Domain.Tests` (1 test)
- `Aonik.Application.Tests` (4 tests)
- `Aonik.Infrastructure.Tests` (1 test)

### Integration Tests

**Target:** Full API endpoints with TestServer + InMemory database

**Framework:** xUnit + WebApplicationFactory + FluentAssertions

**Pattern:**
```csharp
public class InvoiceEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    
    [Fact]
    public async Task CreateInvoice_ReturnsCreated()
    {
        // Arrange
        var request = new CreateInvoiceRequest(...);
        
        // Act
        var response = await _client.PostAsJsonAsync("/billing/invoices", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponse>();
        invoice.Should().NotBeNull();
    }
}
```

**CustomWebApplicationFactory:**
- Configures InMemory database via `UseInMemoryDatabase=true` configuration
- Each test uses a unique database: `TestDb_{Guid.NewGuid()}`
- Ensures test isolation

**Tests:**
- `Aonik.Api.Tests` (5 tests)

### Test Database Strategy

**Production:** SQL Server (LocalDB for development)

**Testing:** EF Core InMemory database

**Configuration:**
```csharp
// In CustomWebApplicationFactory
builder.ConfigureAppConfiguration((context, config) =>
{
    config.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["UseInMemoryDatabase"] = "true",
        ["InMemoryDatabaseName"] = "TestDb_" + Guid.NewGuid()
    });
});
```

**Infrastructure Support:**
```csharp
// In Infrastructure/DependencyInjection.cs
var useInMemory = configuration["UseInMemoryDatabase"];

if (useInMemory == "true")
{
    services.AddDbContext<AonikDbContext>(options =>
        options.UseInMemoryDatabase(configuration["InMemoryDatabaseName"] ?? "AonikDb"));
}
```

**Current Test Coverage:**
- **Total Tests:** 11/11 passing (100%)
- Domain: 1/1 ✅
- Infrastructure: 1/1 ✅
- Application: 4/4 ✅
- API: 5/5 ✅

---

## Technology Stack

### Runtime & Language
- **.NET 10** (`net10.0`)
- **C# 13** (latest language features)
- **Nullable reference types** enabled globally

### Web Framework
- **ASP.NET Core 10.0**
- **FastEndpoints 7.1.1** (lightweight endpoint framework)
- **FastEndpoints.Swagger 7.1.1** (OpenAPI documentation)

### Data Access
- **Entity Framework Core 10.0.1** (ORM)
- **SQL Server** (production database)
- **EF Core InMemory** (testing database)
- **EF Core Migrations** (schema versioning)

### Testing
- **xUnit 2.5.3** (test framework)
- **FluentAssertions 8.8.0** (assertion library)
- **WebApplicationFactory** (integration testing)

### AI (Planned Integrations)
- **OpenAI SDK** (ChatGPT, GPT-4)
- **Anthropic SDK** (Claude)
- **Azure OpenAI** (enterprise AI)
- **Semantic Kernel** (agent orchestration - future)

### Development Tools
- **Swashbuckle/Swagger** (API documentation)
- **EF Core Design Tools** (migrations)

---

## Extensibility & Future Directions

### Planned Features

#### 1. Enhanced AI Capabilities
- **Multi-Model Support**: OpenAI, Anthropic, Azure OpenAI providers
- **Agent Runtime**: Orchestrate multi-step AI workflows with human-in-the-loop
- **Prompt Registry**: Centralized versioned prompt management
- **Feedback Loop**: Track AI prediction accuracy and improve over time

#### 2. Advanced Financial Features
- **Reconciliation Engine**: Match transactions across systems
- **Multi-Currency Support**: Currency conversion and FX rates
- **Tax Calculation**: Automated tax computation for invoices
- **Payment Gateway Integration**: Stripe, PayPal, local African providers

#### 3. Observability
- **Structured Logging**: OpenTelemetry integration
- **Distributed Tracing**: Track requests across services (if decomposed)
- **Metrics**: Prometheus/Grafana for monitoring
- **Audit Logs**: Track all state changes for compliance

#### 4. Worker Background Jobs
- **Scheduled Tasks**: Recurring billing, reminders, forecasts
- **Event Processing**: Async message handling
- **Job Queue**: Durable task execution with retry logic

#### 5. Microservices Evolution (Optional)
- **Service Decomposition**: Split modular monolith into independent services
- **Event-Driven Architecture**: Messaging between services (RabbitMQ, Kafka)
- **API Gateway**: Unified entry point with routing

### Extension Points

**Custom AI Providers:**
Implement `IModelProvider` to integrate any AI service:
```csharp
public class CustomAiProvider : IModelProvider
{
    public async Task<string> GenerateCompletionAsync(
        string systemPrompt, 
        string userPrompt, 
        CancellationToken cancellationToken = default)
    {
        // Your custom AI integration
    }
}
```

**Custom Workflows:**
Create new workflow classes that follow the established pattern:
```csharp
public class CustomWorkflow
{
    public async Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ct)
    {
        // 1. Load data
        // 2. Load prompts
        // 3. Call AI
        // 4. Save results
        // 5. Return response
    }
}
```

**Domain Extension:**
Add new modules by following the existing structure:
```
src/Aonik.Domain/NewModule/
  └── Entities/
      └── NewEntity.cs

src/Aonik.Application/Services/NewModule/
  ├── INewService.cs
  └── NewService.cs

src/Aonik.Api/Endpoints/NewModule/
  └── NewEndpoint.cs
```

---

## Conclusion

AONIK's architecture is designed to be:
- **Maintainable**: Clear separation of concerns and explicit dependencies
- **Testable**: All layers can be tested independently
- **Extensible**: Well-defined extension points for new features
- **AI-Ready**: AI is not an afterthought—it's built into the core design
- **Auditable**: All operations are traceable for financial compliance

As the project evolves, the architecture will mature to support more sophisticated financial operations and AI-driven intelligence while maintaining these core principles.

---

**Next Steps:**
- Review `AGENTS.md` for coding guidelines
- Explore `docs/decisions/` for architectural decision records (future)
- Check `README.md` for project vision and contributing guidelines
