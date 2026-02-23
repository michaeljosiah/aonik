using Aonik.Platform.Contracts.Models.Authentication;
using Aonik.Platform.Contracts.Models.Identity;

namespace Aonik.Platform.Contracts.Services.Identity;

public interface IIdentityService
{
    Task<TokenResponse> TokenAsync(TokenRequest request, CancellationToken cancellationToken = default);
    Task<UserInfoResponse> GetUserInfoAsync(CancellationToken cancellationToken = default);
    Task<ForgotPasswordResponse> SendPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
}
