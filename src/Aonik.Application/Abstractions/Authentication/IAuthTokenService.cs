using Aonik.Application.Models.Authentication;

namespace Aonik.Application.Abstractions.Authentication;

public interface IAuthTokenService
{
    Task<TokenResponse> ExchangeAsync(TokenRequest request, CancellationToken cancellationToken = default);
}
