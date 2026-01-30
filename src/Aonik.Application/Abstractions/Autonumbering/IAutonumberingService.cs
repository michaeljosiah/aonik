using Aonik.Application.Models.Autonumbering;

namespace Aonik.Application.Abstractions.Autonumbering;

public interface IAutonumberingService
{
    Task<AutonumberProfileSnapshot?> GetProfileAsync(
        string entityType,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<AutonumberProfileSnapshot> UpsertProfileAsync(
        AutonumberProfileUpsert request,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<AutonumberGenerateResult> GenerateAsync(
        AutonumberGenerateRequest request,
        CancellationToken cancellationToken = default);
}
