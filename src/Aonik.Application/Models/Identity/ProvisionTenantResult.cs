namespace Aonik.Application.Models.Identity;

public record ProvisionTenantResult(
    bool LedgerCreated,
    int ChartOfAccountsCount,
    int RolesCreated,
    int PoliciesCreated,
    List<string> ActionsPerformed
);
