using Aonik.Platform.Contracts.Models.ReferenceData;

namespace Aonik.Platform.Contracts.Services.ReferenceData;

public interface IReferenceDataService
{
    Task<IReadOnlyList<ReferenceDataItemSnapshot>> GetAsync(
        string type,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<ReferenceDataItemSnapshot> UpsertAsync(
        ReferenceDataItemUpsert request,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);
}
