using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Ai.Services.Safety;

/// <summary>
/// Spec 096 §12 — the path that has to exist before it is needed.
///
/// <para>
/// If child sexual abuse material is ever generated or detected here, the obligations to preserve and
/// report are not discretionary and not something engineering decides in the moment. <strong>Discovering
/// this obligation during an incident is the worst possible time to learn it</strong>, so the mechanism
/// exists now, unused, rather than being written under pressure by whoever is on call.
/// </para>
///
/// <para>
/// The custodian list is <em>named individuals</em>, not a role. A role would grant access to anyone who
/// later acquires it, which is precisely the property §12 rules out. It is empty by default — F7 has not
/// been resolved — so nobody can reach preserved material today. That is the safe state, not a bug: the
/// material is unreachable rather than reachable by whoever holds an admin claim.
/// </para>
/// </summary>
public interface IPreservedMaterialService
{
    /// <summary>
    /// Request preserved material. <strong>Every attempt is logged, granted or denied</strong>, before
    /// the answer is returned.
    /// </summary>
    Task<PreservedAccessOutcome> AccessAsync(
        Guid actorPartyId,
        Guid incidentId,
        string purpose,
        CancellationToken cancellationToken = default);

    /// <summary>Open escalations — the queryable form of "nobody has looked at this yet".</summary>
    Task<IReadOnlyList<OpenEscalation>> ListOpenEscalationsAsync(
        Guid actorPartyId, CancellationToken cancellationToken = default);

    Task<bool> AcknowledgeAsync(
        Guid actorPartyId, Guid escalationId, string notes, CancellationToken cancellationToken = default);
}

/// <param name="Reference">The storage key, only when granted.</param>
public sealed record PreservedAccessOutcome(bool Granted, string? Reference, string? Reason);

/// <param name="MaterialPreserved">
/// Whether there is evidence behind this escalation. <strong>Null means not recorded</strong> — not
/// "no". Reaching the custodian matters more than the flag existing: a responsible person who cannot
/// tell an escalation with evidence from one without it receives the same record either way, which is
/// the whole reason the field was added.
/// </param>
public sealed record OpenEscalation(
    Guid EscalationId,
    Guid SafetyIncidentId,
    string Category,
    DateTime RaisedAt,
    bool? MaterialPreserved);

/// <summary>
/// Whether a party has material under a §12 hold, for any future erasure path.
///
/// <para>
/// <strong>No subject-access erasure path exists in this codebase today.</strong> This contract exists
/// so that the one somebody writes later cannot be written without encountering it — the same argument
/// §12 makes about the procedure itself. Preservation overrides deletion requests, and a deletion that
/// destroys evidence is not a privacy right being exercised.
/// </para>
/// </summary>
public interface ILegalHoldReader
{
    Task<bool> HasLegalHoldAsync(
        Guid subjectPartyId, CancellationToken cancellationToken = default);
}

internal sealed class PreservedMaterialService : IPreservedMaterialService, ILegalHoldReader
{
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IOptions<SafetyOptions> _options;
    private readonly ILogger<PreservedMaterialService> _logger;

    public PreservedMaterialService(
        AiDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        IOptions<SafetyOptions> options,
        ILogger<PreservedMaterialService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    public async Task<PreservedAccessOutcome> AccessAsync(
        Guid actorPartyId, Guid incidentId, string purpose, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var incident = await _dbContext.SafetyIncidents
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == incidentId, cancellationToken);

        if (incident is null)
        {
            return await LogAsync(
                tenantId, incidentId, actorPartyId, purpose, now,
                granted: false, reason: "No such incident.", cancellationToken);
        }

        if (!IsCustodian(actorPartyId))
        {
            // Logged as a refusal, which is the record most worth having. Somebody reaching for this
            // material and being turned away is exactly what a later review needs to see, and it is
            // invisible if the log is only written once the check has passed.
            _logger.LogWarning(
                "Party {ActorId} attempted to access preserved material for incident {IncidentId} "
                + "and is not a named custodian.", actorPartyId, incidentId);

            return await LogAsync(
                tenantId, incidentId, actorPartyId, purpose, now,
                granted: false, reason: "Not a named custodian.", cancellationToken);
        }

        var artefact = await _dbContext.SafetyArtefacts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.TenantId == tenantId && a.SafetyIncidentId == incidentId, cancellationToken);

        if (artefact is null)
        {
            return await LogAsync(
                tenantId, incidentId, actorPartyId, purpose, now,
                granted: false, reason: "No preserved artefact.", cancellationToken);
        }

        var outcome = await LogAsync(
            tenantId, incidentId, actorPartyId, purpose, now,
            granted: true, reason: null, cancellationToken);

        _logger.LogWarning(
            "Custodian {ActorId} accessed preserved material for incident {IncidentId}. Purpose: {Purpose}",
            actorPartyId, incidentId, purpose);

        return outcome with { Reference = artefact.Reference };
    }

    public async Task<IReadOnlyList<OpenEscalation>> ListOpenEscalationsAsync(
        Guid actorPartyId, CancellationToken cancellationToken = default)
    {
        if (!IsCustodian(actorPartyId))
        {
            return [];
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var open = await _dbContext.SafetyEscalations
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.AcknowledgedAt == null)
            .OrderBy(e => e.RaisedAt)
            .ToListAsync(cancellationToken);

        return
        [
            .. open.Select(e => new OpenEscalation(
                e.Id, e.SafetyIncidentId, e.Category, e.RaisedAt, e.MaterialPreserved))
        ];
    }

    public async Task<bool> AcknowledgeAsync(
        Guid actorPartyId, Guid escalationId, string notes, CancellationToken cancellationToken = default)
    {
        if (!IsCustodian(actorPartyId))
        {
            return false;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        var escalation = await _dbContext.SafetyEscalations
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId && e.Id == escalationId, cancellationToken);

        if (escalation is null || escalation.AcknowledgedAt is not null)
        {
            return false;
        }

        escalation.AcknowledgedAt = _clock.UtcNow;
        escalation.AcknowledgedByPartyId = actorPartyId;
        escalation.Notes = notes;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> HasLegalHoldAsync(Guid subjectPartyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return _dbContext.SafetyIncidents
            .AsNoTracking()
            .AnyAsync(i => i.TenantId == tenantId
                && i.SubjectPartyId == subjectPartyId
                && i.IsUnderLegalHold, cancellationToken);
    }

    /// <summary>
    /// Named individuals, by party id. Empty means nobody, which is the correct answer while F7 is
    /// unresolved — not a lockout to work around.
    /// </summary>
    private bool IsCustodian(Guid actorPartyId)
        => _options.Value.PreservedMaterialCustodians
            .Any(id => Guid.TryParse(id, out var custodian) && custodian == actorPartyId);

    private async Task<PreservedAccessOutcome> LogAsync(
        Guid tenantId,
        Guid incidentId,
        Guid actorPartyId,
        string purpose,
        DateTime now,
        bool granted,
        string? reason,
        CancellationToken cancellationToken)
    {
        _dbContext.PreservedMaterialAccesses.Add(new PreservedMaterialAccess
        {
            TenantId = tenantId,
            SafetyIncidentId = incidentId,
            ActorPartyId = actorPartyId,
            RequestedAt = now,
            WasGranted = granted,
            Purpose = purpose,
            DenialReason = reason,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PreservedAccessOutcome(granted, null, reason);
    }
}
