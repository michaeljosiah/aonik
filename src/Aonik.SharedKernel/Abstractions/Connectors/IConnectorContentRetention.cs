namespace Aonik.SharedKernel.Abstractions.Connectors;

/// <summary>Reclaims expired temporary uploads from the dedicated private connector store.</summary>
public interface IConnectorContentRetention
{
    Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default);
}
