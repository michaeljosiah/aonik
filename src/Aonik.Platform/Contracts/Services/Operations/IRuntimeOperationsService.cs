using Aonik.Platform.Contracts.Api.Observability;

namespace Aonik.Platform.Contracts.Services.Operations;

public interface IRuntimeOperationsService
{
    Task<IReadOnlyList<RuntimeServiceStatus>> ListRuntimeServicesAsync(CancellationToken cancellationToken = default);
    Task<RuntimeServiceActionResponse> StartServiceAsync(string serviceName, CancellationToken cancellationToken = default);
}
