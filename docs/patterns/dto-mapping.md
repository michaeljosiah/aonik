# DTO Mapping

AONIK keeps transport models and persistence models separate.

## Guidelines

- API request/response contracts are co-located with endpoints in each module project (e.g., `src/Aonik.Finance/Endpoints/Billing/`).
- Application-level DTOs live near their services in the module project.
- Domain entities live in the module's `Entities/` directory.

Endpoints map API contracts to internal DTOs and return DTOs. Services map entities to DTOs in private static methods.
