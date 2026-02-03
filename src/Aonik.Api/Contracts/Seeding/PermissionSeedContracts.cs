namespace Aonik.Api.Contracts.Seeding;

public record PermissionSeedResponse(
    Guid TenantId,
    DateTime SeededAt,
    IReadOnlyList<string> Operations);
