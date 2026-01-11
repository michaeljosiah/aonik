# API Endpoints

AONIK uses **FastEndpoints** for HTTP endpoints.

## Structure

- Endpoints live under `src/Aonik.Api/Endpoints/{Module}`.
- Endpoints call Application services and return DTOs.

## Conventions

- Inherit from `Endpoint<TRequest, TResponse>`.
- Override `Configure()` to set route and auth.
- Override `HandleAsync()` and use `Send.*Async()` helpers.

Examples of response helpers:

- `await Send.OkAsync(response, ct)`
- `await Send.CreatedAtAsync<GetEndpoint>(new { id = response.Id }, response, ct)`
- `await Send.NotFoundAsync(ct)`

Avoid using `SendAsync()` directly.

## Mapping

- Map API contracts (API layer) to DTOs (Application layer) inside the endpoint.
