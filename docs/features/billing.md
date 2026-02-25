# Billing

The Billing subdomain (within the Finance module) provides invoicing primitives and related workflows.

## Scope

- Invoices and invoice line items
- Customer accounts
- Dunning plans and allocations (where implemented)

## Where to look

- Entities: `src/Aonik.Finance/Entities/Billing/`
- Services: `src/Aonik.Finance/Services/Billing/`
- Endpoints: `src/Aonik.Finance/Endpoints/Billing/`
- EF Configurations: `src/Aonik.Finance/Persistence/Configurations/Billing/`

## Testing

- Service tests: `tests/Aonik.Application.Tests`
- API integration tests: `tests/Aonik.Api.Tests`
