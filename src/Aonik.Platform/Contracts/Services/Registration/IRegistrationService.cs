using Aonik.Platform.Contracts.Models.Registration;

namespace Aonik.Platform.Contracts.Services.Registration;

public interface IRegistrationService
{
    Task<IndividualRegistrationResult> RegisterIndividualAsync(
        IndividualRegistrationRequest request,
        CancellationToken cancellationToken = default);
}
