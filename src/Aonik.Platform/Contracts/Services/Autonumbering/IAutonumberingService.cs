using Aonik.Platform.Contracts.Models.Autonumbering;

namespace Aonik.Platform.Contracts.Services.Autonumbering;

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

    Task<AutonumberGenerateResult> PreviewAsync(
        AutonumberGenerateRequest request,
        CancellationToken cancellationToken = default);
}
