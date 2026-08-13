using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// Records verification attempts (Spec 095 §12.2, §13).
///
/// <para>
/// <strong>Writes on its own connection, deliberately.</strong> A verification row must survive a
/// failed enrolment — §13 keeps one per attempt <em>including failures</em>, precisely so repeated
/// failures are visible, and rolling it back with the transaction destroys that signal in exactly
/// the case it exists for.
/// </para>
///
/// <para>
/// It is keyed on the guardian plus an attempt id rather than on the child, because verification
/// happens before the child exists and, for a failed attempt, the child never will. That makes
/// "repeated failed attempts" a pattern <em>per guardian</em> — the better unit anyway: a guardian
/// failing against several children is more interesting than one failing repeatedly against one.
/// </para>
/// </summary>
internal interface IGuardianVerificationRecorder
{
    Task RecordAsync(
        Guid guardianPartyId,
        Guid enrolmentAttemptId,
        GuardianVerificationResult result,
        CancellationToken cancellationToken = default);

    /// <summary>How many attempts this guardian has failed since <paramref name="since"/>.</summary>
    Task<int> CountRecentFailuresAsync(
        Guid guardianPartyId,
        DateTime since,
        CancellationToken cancellationToken = default);
}

internal sealed class GuardianVerificationRecorder : IGuardianVerificationRecorder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public GuardianVerificationRecorder(
        IServiceScopeFactory scopeFactory,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _scopeFactory = scopeFactory;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task RecordAsync(
        Guid guardianPartyId,
        Guid enrolmentAttemptId,
        GuardianVerificationResult result,
        CancellationToken cancellationToken = default)
    {
        // A separate scope, so this commit is independent of whatever transaction the caller is
        // running on the ambient context. If the enrolment that follows rolls back, this row stays.
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        context.ConsentVerifications.Add(new ConsentVerification
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantProvider.GetCurrentTenantId(),
            GuardianPartyId = guardianPartyId,
            EnrolmentAttemptId = enrolmentAttemptId,
            Method = result.Method,
            Succeeded = result.Succeeded,
            OutcomeRef = result.OutcomeRef,
            FailureReason = Truncate(result.FailureReason, 256),
            AttemptedAt = _clock.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountRecentFailuresAsync(
        Guid guardianPartyId,
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await context.ConsentVerifications
            .AsNoTracking()
            .CountAsync(
                v => v.TenantId == tenantId
                    && v.GuardianPartyId == guardianPartyId
                    && !v.Succeeded
                    && v.AttemptedAt >= since,
                cancellationToken);
    }

    /// <summary>
    /// Failure reasons are for support, and must never carry the supplied credential or document.
    /// Truncating here is a backstop against a verifier returning a provider message that happens to
    /// echo the input back.
    /// </summary>
    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
