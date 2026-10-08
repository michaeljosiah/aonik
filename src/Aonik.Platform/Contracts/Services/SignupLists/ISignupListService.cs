using Aonik.Platform.Contracts.Models.SignupLists;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Contracts.Services.SignupLists;

public interface ISignupListService
{
    Task<SignupListsConfigurationDto> GetConfigurationAsync(CancellationToken cancellationToken = default);
    Task CaptureAsync(string listType, SignupCaptureRequest request, CancellationToken cancellationToken = default);
    Task<bool> UnsubscribeAsync(string listType, Guid subscriptionId, string? token, CancellationToken cancellationToken = default);
    Task<PagedResult<SignupSubscriptionDto>> ListAsync(string listType, bool includeUnsubscribed = false,
        int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<List<SignupAreaDemandDto>> GetAreaDemandAsync(CancellationToken cancellationToken = default);
}
