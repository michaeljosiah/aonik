namespace Aonik.Platform.Contracts.Models.Identity;

public record TenantHealthResult(
    bool IsHealthy,
    bool HasLedger,
    bool HasRoles,
    bool HasChartOfAccounts,
    List<string> Issues
);
