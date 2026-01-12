using Aonik.Application.Models.Registration;

namespace Aonik.Application.Services.Registration;

public interface IRegistrationService
{
    Task<IndividualRegistrationResult> RegisterIndividualAsync(
        IndividualRegistrationRequest request,
        CancellationToken cancellationToken = default);
}
