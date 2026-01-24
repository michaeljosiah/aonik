using Aonik.Application.Models.ReferenceData;

namespace Aonik.Application.Abstractions.ReferenceData;

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
