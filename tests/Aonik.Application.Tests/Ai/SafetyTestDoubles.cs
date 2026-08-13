using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Safety;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Stands in for the Platform record the gate reads the band from. Returning null models a party with
/// no band, which must resolve to the strictest and never to adult.
/// </summary>
internal sealed class StubSafetyBandReader : ISafetyBandReader
{
    private readonly string? _band;
    private readonly Exception? _throws;

    public StubSafetyBandReader(string? band = null, Exception? throws = null)
    {
        _band = band;
        _throws = throws;
    }

    public Task<string?> GetSafetyBandAsync(Guid partyId, CancellationToken cancellationToken = default)
        => _throws is not null
            ? Task.FromException<string?>(_throws)
            : Task.FromResult(_band);
}

/// <summary>
/// Guardian authority as Spec 095 hands it to Spec 096: an explicit edge, or nothing. Kinship grants
/// no authority here and neither does sharing a tenant, which is exactly what these tests need to be
/// able to assert.
/// </summary>
internal sealed class StubGuardianship : IGuardianshipReader
{
    private readonly HashSet<(Guid Guardian, Guid Child)> _edges = [];

    public StubGuardianship Add(Guid guardian, Guid child)
    {
        _edges.Add((guardian, child));
        return this;
    }

    public Task<bool> HasAuthorityAsync(
        Guid tenantId, Guid guardianPartyId, Guid childPartyId, CancellationToken cancellationToken = default)
        => Task.FromResult(_edges.Contains((guardianPartyId, childPartyId)));

    public Task<IReadOnlyList<Guid>> GetGuardiansAsync(
        Guid tenantId, Guid childPartyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Guid>>(
            [.. _edges.Where(e => e.Child == childPartyId).Select(e => e.Guardian)]);

    public Task<IReadOnlyList<Guid>> GetWardsAsync(
        Guid tenantId, Guid guardianPartyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Guid>>(
            [.. _edges.Where(e => e.Guardian == guardianPartyId).Select(e => e.Child)]);
}
