# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What AONIK Is

AONIK is an AI-native, modular financial platform (.NET 10) that powers multiple products: Payabo (B2C personal finance), MyBillAfrica (B2B billing), and RemitExchange (cross-border remittance). It is the foundational platform layer, not a single application.

## Build, Test & Run Commands

```bash
# Build
dotnet build Aonik.sln
dotnet clean Aonik.sln && dotnet build Aonik.sln

# Test
dotnet test Aonik.sln
dotnet test tests/Aonik.Application.Tests
dotnet test --filter "FullyQualifiedName~BillingServiceTests.CreateInvoiceAsync_ShouldCreateInvoiceWithLineItems"
dotnet test --filter "DisplayName~CreateInvoice"

# Run API (https://localhost:5001, Swagger at /swagger)
dotnet run --project src/Aonik.Api

# Run with Aspire orchestrator (starts API + Admin UI + Payabo + Worker)
dotnet run --project src/Aonik.AppHost

# Database migrations
dotnet ef migrations add <Name> --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
dotnet ef database update --project src/Aonik.Infrastructure --startup-project src/Aonik.Api

# Admin UI
cd src/Aonik.AdminUi && npm install && npm run dev   # port 5173

# Payabo app
cd apps/Payabo && npm install && npm run dev          # port 5174
```

## Architecture

**Module-first modular monolith** with anemic domain entities. Each module owns its vertical slice (entities, services, endpoints, persistence) with a module-scoped DbContext over a shared SQL Server database.

Key modules:
- **SharedKernel** — Cross-cutting primitives, interfaces, integration events
- **Platform** — Identity, tenancy, party/profile, compliance, notifications
- **Finance** — Ledger, payments, orders, billing, pricing, partners, personal finance
- **Ai** — LLM routing, prompts, AI execution records
- **Agents** — Domain agents (MAF-based), orchestration, proposals
- **Infrastructure** — EF Core migrations, external adapters
- **Api** — HTTP composition root (FastEndpoints)
- **Worker** — Background jobs (Quartz)
- **AdminUi** — React 19 + Vite + Tailwind + Dockview

Module organization follows vertical slices:
```
src/Aonik.Finance/Entities/Billing/Invoice.cs
src/Aonik.Finance/Services/Billing/BillingService.cs
src/Aonik.Finance/Endpoints/Billing/CreateInvoiceEndpoint.cs
src/Aonik.Finance/Persistence/Configurations/Billing/InvoiceConfiguration.cs
```

**No direct cross-module references.** Modules communicate via SharedKernel contracts (interfaces like `IPartyService`) and integration events (`TenantProvisionedEvent`, `OrderCreatedEvent`).

## Non-Negotiable Architectural Rules

1. **Ledger is the source of financial truth** (double-entry, immutable)
2. **Orders are the canonical record of a requested financial service** — not payments, not ledger entries — never collapse them
3. **Agents propose; systems execute** — never bypass human-in-the-loop for mutations
4. **Every AI action is auditable** (recorded as `AiRun`)
5. **Risk tier determines AI autonomy**
6. **Domain entities are anemic** — all business logic lives in services, not entities

## Orders

An Order is the canonical record of a customer's intent to use an AONIK-powered financial service. It captures the requested service, the parties involved, the amounts and currencies, and the lifecycle from funding through fulfilment. Orders capture the requested financial service and why money should move.

An Order records: what service was requested, who the relevant parties are, what amounts/currencies are involved, how the request is funded, and how it is fulfilled.

**Order vs Payment vs Ledger:**
- **Order** = the requested financial service (bill payment, money transfer, bill collection, payout, remittance)
- **Payment** = how the order is funded or executed
- **Ledger** = the financial truth proving what happened

**Not Orders:** a standalone imported bank transaction, a manual categorisation, or a ledger correction (unless it exists to fulfil a service request).

## Code Patterns

**Entities**: Simple data containers with `{ get; set; }` properties. Inherit from `Entity` or `AuditableEntity`. Implement `ITenantScoped`. No constructors, no methods, no business logic.

**Services**: All logic here. Interface + implementation (`IBillingService` / `BillingService`). Constructor-inject DbContext, `ITenantProvider`, etc. Return DTOs, not entities. Always accept `CancellationToken cancellationToken = default`.

**Endpoints** (FastEndpoints): Inherit `Endpoint<TRequest, TResponse>`. Use `Send.OkAsync()`, `Send.CreatedAtAsync<T>()`, `Send.NotFoundAsync()`. Never use `SendAsync()` or `ResponseAsync()` directly.

**DTOs**: Use records with positional parameters: `public record CreateInvoiceRequest(Guid CustomerId, string Currency, ...);`

**Agent tools**: Read tools execute directly. Mutating tools (`CreateInvoice`, `IssueInvoice`, `CreatePaymentIntent`, etc.) are wrapped with `ApprovalRequiredAIFunction`.

## Testing

- **Framework**: xUnit + FluentAssertions (use `.Should()`, never `Assert.*`)
- **Pattern**: AAA with `MethodName_Should_ExpectedBehavior_When_Condition` naming
- **Database**: InMemory with unique name `$"TestDb_{Guid.NewGuid()}"`
- **Required mocks**: `ITenantProvider` (use `TestTenantProvider`) and `ICurrentUserProvider`
- **API tests**: Use `CustomWebApplicationFactory` with `UseInMemoryDatabase=true` in config

## Naming Conventions

- Classes/Interfaces: PascalCase (`Invoice`, `IBillingService`)
- Async methods: PascalCase + `Async` suffix (`CreateInvoiceAsync`)
- Private fields: `_camelCase` (`_dbContext`)
- Parameters/locals: camelCase
- Nullable annotations enabled globally

## Tech Stack Quick Reference

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 (`net10.0`) |
| API | FastEndpoints |
| ORM | EF Core 10 / SQL Server |
| AI/Agents | Microsoft Agent Framework, Semantic Kernel |
| Background jobs | Quartz.NET |
| Admin UI | React 19, Vite, Tailwind, Dockview |
| Caching | FusionCache |
| Orchestration | .NET Aspire |
| Testing | xUnit, FluentAssertions |
