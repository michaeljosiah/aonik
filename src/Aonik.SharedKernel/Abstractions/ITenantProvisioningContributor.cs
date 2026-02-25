namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Cross-module contract for contributing to tenant provisioning.
/// Each module (Finance, AI, etc.) implements this to provision
/// its own resources when a new tenant is created.
/// 
/// Resolved as <c>IEnumerable&lt;ITenantProvisioningContributor&gt;</c>
/// by TenantProvisioner.
/// </summary>
public interface ITenantProvisioningContributor
{
    /// <summary>
    /// Module name for logging/diagnostics (e.g. "Finance", "Ai").
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Provisions module-specific resources for a new tenant.
    /// </summary>
    Task<TenantProvisioningContribution> ContributeProvisioningAsync(
        TenantProvisioningContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks module-specific health for a tenant.
    /// Adds issues to the provided list if anything is missing.
    /// </summary>
    Task ContributeHealthCheckAsync(
        Guid tenantId,
        List<string> issues,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context passed to provisioning contributors.
/// </summary>
public record TenantProvisioningContext(
    Guid TenantId,
    string DefaultCurrency,
    Guid? UserId,
    DateTime Now
);

/// <summary>
/// Result returned from a provisioning contributor, merged into the overall result.
/// </summary>
public record TenantProvisioningContribution(
    List<string> ActionsPerformed,
    bool LedgerCreated = false,
    int ChartOfAccountsCount = 0,
    int PoliciesCreated = 0
);
