# Module Organization

AONIK is organized by business module with a vertical-slice feel.

## Example

- `src/Aonik.Domain/Billing/Entities/Invoice.cs`
- `src/Aonik.Application/Services/Billing/BillingService.cs`
- `src/Aonik.Api/Endpoints/Billing/CreateInvoiceEndpoint.cs`

## Guidance

- Keep DTOs in the Application layer (typically under `Application/Models/{Module}/`).
- Keep API request/response contracts in the API layer.
- Avoid placing business rules in Domain entities (anemic model).
