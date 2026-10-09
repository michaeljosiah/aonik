using Aonik.Platform.Entities.Identity;

namespace Aonik.Platform.Contracts.Services.Authentication;

public interface IIdpAccountService
{
    Task ValidatePasswordAsync(User user, string password, CancellationToken cancellationToken = default);
    Task UpdateEmailAsync(User user, string newEmail, CancellationToken cancellationToken = default);
    /// <summary>Called only after the action service proves the target mailbox and claims its durable Applying state.
    /// Replays must reconcile the exact subject and target; a different current email must fail closed.</summary>
    Task ConfirmVerifiedEmailAsync(User user, string expectedCurrentEmail, string newEmail, CancellationToken cancellationToken = default);
    Task UpdatePasswordAsync(User user, string newPassword, CancellationToken cancellationToken = default);
}
