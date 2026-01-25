namespace Aonik.Application.Models.Seeding;

public record DemoSeedResult(
    Guid TenantId,
    DateTime SeededAt,
    IReadOnlyList<string> Operations);
