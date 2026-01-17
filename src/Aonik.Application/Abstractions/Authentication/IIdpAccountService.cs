using Aonik.Domain.Identity.Entities;

namespace Aonik.Application.Abstractions.Authentication;

public interface IIdpAccountService
{
    Task ValidatePasswordAsync(User user, string password, CancellationToken cancellationToken = default);
    Task UpdateEmailAsync(User user, string newEmail, CancellationToken cancellationToken = default);
    Task UpdatePasswordAsync(User user, string newPassword, CancellationToken cancellationToken = default);
}
