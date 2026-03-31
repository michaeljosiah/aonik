namespace Aonik.SharedKernel.Abstractions.PersonalFinance;

/// <summary>
/// Cross-module interface allowing the Platform registration flow to ensure
/// a PersonalProfile record exists in the Finance module for a newly registered user.
/// </summary>
public interface IPersonalProfileProvisioner
{
    /// <summary>
    /// Ensures a PersonalProfile exists for the given user. Creates one if missing.
    /// </summary>
    Task EnsurePersonalProfileAsync(
        Guid tenantId,
        Guid userId,
        Guid partyId,
        CancellationToken cancellationToken = default);
}
