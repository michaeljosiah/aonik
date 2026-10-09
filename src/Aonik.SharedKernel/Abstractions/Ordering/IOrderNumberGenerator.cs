namespace Aonik.SharedKernel.Abstractions.Ordering;

/// <summary>Allocates an order reference using the current tenant's numbering profile.</summary>
public interface IOrderNumberGenerator
{
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}
