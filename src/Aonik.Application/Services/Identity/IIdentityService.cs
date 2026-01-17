using Aonik.Application.Models.Authentication;
using Aonik.Application.Models.Identity;

namespace Aonik.Application.Services.Identity;

public interface IIdentityService
{
    Task<TokenResponse> TokenAsync(TokenRequest request, CancellationToken cancellationToken = default);
    Task<UserInfoResponse> GetUserInfoAsync(CancellationToken cancellationToken = default);
    Task<ForgotPasswordResponse> SendPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
}
