using Aonik.Application.Models.Identity;

namespace Aonik.Application.Services.Identity;

public interface IVerificationService
{
    Task<VerificationChallengeResult> StartEmailVerificationAsync(
        Guid userId,
        string email,
        CancellationToken cancellationToken = default);

    Task<bool> ConfirmEmailVerificationAsync(
        Guid userId,
        string email,
        string code,
        CancellationToken cancellationToken = default);

    Task<VerificationChallengeResult> StartPhoneVerificationAsync(
        Guid userId,
        string phone,
        CancellationToken cancellationToken = default);

    Task<bool> ConfirmPhoneVerificationAsync(
        Guid userId,
        string phone,
        string code,
        CancellationToken cancellationToken = default);
}
