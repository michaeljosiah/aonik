using Aonik.Commerce.Contracts.Models.Fulfilment;

namespace Aonik.Commerce.Services.Fulfilment;

public interface IDeliveryCoverageService
{
    Task<DeliveryCoverageDto> CheckAsync(string? postcode, CancellationToken cancellationToken = default);
    Task<DeliveryCoverageConfigDto?> GetConfigurationAsync(CancellationToken cancellationToken = default);
    Task<DeliveryCoverageConfigDto> UpdateConfigurationAsync(DeliveryCoverageConfigDto configuration,
        CancellationToken cancellationToken = default);
}
