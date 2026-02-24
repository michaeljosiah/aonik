namespace Aonik.Platform.Contracts.Api.Seeding;

public record DemoSeedRequest(
    string? SeedType);

public record DemoSeedResponse(
    Guid TenantId,
    string SeedType,
    DateTime SeededAt,
    IReadOnlyList<string> Operations);
