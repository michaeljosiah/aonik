namespace Aonik.Platform.Contracts.Models.Seeding;

public record DemoSeedResult(
    Guid TenantId,
    string SeedType,
    DateTime SeededAt,
    IReadOnlyList<string> Operations);
