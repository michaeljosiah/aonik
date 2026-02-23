using Aonik.Platform.Contracts.Models.Authentication;

namespace Aonik.Platform.Contracts.Services.Authentication;

public interface IAuthTokenService
{
    Task<TokenResponse> ExchangeAsync(TokenRequest request, CancellationToken cancellationToken = default);
}
