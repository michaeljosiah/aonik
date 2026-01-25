namespace Aonik.Api.Contracts.Seeding;

public record DemoSeedResponse(
    Guid TenantId,
    DateTime SeededAt,
    IReadOnlyList<string> Operations);
