namespace Aonik.Platform.Contracts.Api.Seeding;

public record PermissionSeedResponse(
    Guid TenantId,
    DateTime SeededAt,
    IReadOnlyList<string> Operations);
