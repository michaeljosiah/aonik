using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Contracts.Services.Customers;

/// <summary>
/// Service for exporting and importing customer data bundles.
/// Used by admin endpoints to transfer customer data between environments.
/// </summary>
public interface ICustomerDataService
{
    /// <summary>
    /// Exports the full data graph for a customer (party) as a portable JSON bundle.
    /// </summary>
    Task<CustomerDataBundle?> ExportAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a customer data bundle into the current tenant.
    /// Generates new IDs for all entities and remaps FK references.
    /// </summary>
    Task<CustomerDataImportResult> ImportAsync(
        CustomerDataBundle bundle,
        string conflictMode = "fail",
        CancellationToken cancellationToken = default);
}
