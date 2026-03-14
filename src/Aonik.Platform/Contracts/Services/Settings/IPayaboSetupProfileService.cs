using Aonik.Platform.Contracts.Models.Settings;

namespace Aonik.Platform.Contracts.Services.Settings;

public interface IPayaboSetupProfileService
{
    Task<PayaboSetupProfileSnapshot?> GetCurrentAsync(
        CancellationToken cancellationToken = default);

    Task<PayaboSetupProfileSnapshot> SaveCurrentAsync(
        PayaboSetupProfileSnapshot profile,
        CancellationToken cancellationToken = default);

    Task ClearCurrentAsync(CancellationToken cancellationToken = default);
}
