using Aonik.Platform.Contracts.Services.Authentication;

namespace Aonik.Platform.Contracts.Services.Identity;

public interface IAccountAccessService
{
    Task<bool> ResolveAsync(string? token, CancellationToken cancellationToken = default);
    Task ResendAsync(string? token, CancellationToken cancellationToken = default);
    Task<bool> CompleteAsync(string? token, AccountAccessIdentityProof proof, string purpose, CancellationToken cancellationToken = default);
    Task RequestEmailChangeAsync(string? newEmail, AccountAccessIdentityProof proof, CancellationToken cancellationToken = default);
    Task DeliverAsync(Guid actionId, Guid generation, CancellationToken cancellationToken = default);
}
