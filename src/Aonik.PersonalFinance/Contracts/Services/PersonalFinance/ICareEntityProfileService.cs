using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

/// <summary>
/// Composes the one-call CareEntity profile projection (Spec 043 §8): the
/// entity plus per-currency totals, commitments, recent payment logs, and
/// linked document refs. Dependent arrays fill in as Specs 044/045/046 land.
/// </summary>
public interface ICareEntityProfileService
{
    /// <summary>Returns <c>null</c> when the entity is not owned by the current user.</summary>
    Task<CareEntityProfileResponse?> GetProfileAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
