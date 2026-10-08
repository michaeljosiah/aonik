using Aonik.Platform.Contracts.Models.Settings;

namespace Aonik.Platform.Contracts.Services.Settings;

public interface IPublicBusinessProfileService
{
    Task<PublicBusinessProfileDto?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
