namespace Aonik.Application.Models.Seeding;

public record PermissionSeedResult(
    Guid TenantId,
    DateTime SeededAt,
    IReadOnlyList<string> Operations);
