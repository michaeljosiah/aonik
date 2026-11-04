namespace Aonik.SharedKernel.Abstractions.Platform;

/// <summary>All active party links for separation-of-duties checks, including non-primary parties.</summary>
public interface IUserPartyMembershipReader
{
    Task<IReadOnlyList<Guid>> GetPartyIdsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}
