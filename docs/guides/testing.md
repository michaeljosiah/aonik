# Testing

See the canonical testing documentation: [Testing Guide](../Testing.md).

## Quick reference

- API integration tests use `CustomWebApplicationFactory` with InMemory database configuration.
- Service tests use EF Core InMemory with unique database names per test.
- All tests use xUnit with FluentAssertions.
- Run all tests: `dotnet test Aonik.sln`
