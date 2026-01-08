namespace Aonik.Application.Models.Identity;

public record TenantHealthResult(
    bool IsHealthy,
    bool HasLedger,
    bool HasRoles,
    bool HasChartOfAccounts,
    List<string> Issues
);
