namespace Aonik.Api.Contracts.Seeding;

public record DemoSeedRequest(
    string? SeedType);

public record DemoSeedResponse(
    Guid TenantId,
    string SeedType,
    DateTime SeededAt,
    IReadOnlyList<string> Operations);
