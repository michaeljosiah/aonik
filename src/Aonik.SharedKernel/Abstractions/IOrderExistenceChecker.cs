namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Cross-module contract for checking whether an Order exists.
/// Implemented by the Finance module; consumed by Platform services
/// (e.g., ComplianceService) that need to validate order references
/// without taking a direct dependency on Finance entities.
/// </summary>
public interface IOrderExistenceChecker
{
    Task<bool> OrderExistsAsync(Guid orderId, CancellationToken cancellationToken = default);
}
