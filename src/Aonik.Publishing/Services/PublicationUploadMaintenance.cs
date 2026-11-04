using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Publishing.Services;

public interface IPublicationUploadMaintenance
{
    Task<IReadOnlyList<Guid>> FindTenantsAsync(CancellationToken cancellationToken = default);
    Task<int> SweepAsync(CancellationToken cancellationToken = default);
}

internal sealed class PublicationUploadMaintenance(IPublishingDataContext db, IPublishingObjectStore objects,
    ITenantProvider tenantProvider, IClock clock, IOptions<PublishingOptions> options) : IPublicationUploadMaintenance
{
    // Deliberately system-scoped: this only discovers owners of already committed cleanup work.
    public async Task<IReadOnlyList<Guid>> FindTenantsAsync(CancellationToken cancellationToken = default)
        => await db.UploadSessions.AsNoTracking().AcrossTenants().Where(s => !s.IsDeleted && s.CleanedAt == null
            && (s.RetiredAt != null || (s.ExpiresAt <= clock.UtcNow && s.Status != "uploaded")))
            .Select(s => s.TenantId).Distinct().Take(1000).ToListAsync(cancellationToken);

    public async Task<int> SweepAsync(CancellationToken cancellationToken = default)
    {
        var tenant = tenantProvider.GetCurrentTenantId();
        var ids = await db.UploadSessions.AsNoTracking().Where(s => s.TenantId == tenant && s.CleanedAt == null
            && (s.RetiredAt != null || (s.ExpiresAt <= clock.UtcNow && s.Status != "uploaded")))
            .OrderBy(s => s.CreatedAt).Select(s => s.Id).Take(100).ToListAsync(cancellationToken);
        var deleted = 0;
        foreach (var id in ids)
        {
            var erase = await db.InTransactionAsync(async ct =>
            {
                var session = await db.UploadSessions.SingleAsync(s => s.TenantId == tenant && s.Id == id, ct);
                if (session.Status == "uploaded" || session.CleanedAt.HasValue || session.AssemblyExpiresAt > clock.UtcNow) return false;
                if (session.StorageLocationId != objects.LocationId) return false;
                if (await db.Revisions.AnyAsync(r => r.TenantId == tenant && r.CurrentUploadSessionId == id && r.UploadedAt != null, ct)) return false;
                if (!session.RetiredAt.HasValue)
                    UploadReservations.Retire(session, "expired", clock.UtcNow);
                if (session.RetiredAt > clock.UtcNow.AddHours(-Math.Clamp(options.Value.Intake.CleanupGraceHours, 1, 168)))
                { await db.SaveChangesAsync(ct); return false; }
                session.Status = "deleting";
                await db.SaveChangesAsync(ct); return true;
            }, cancellationToken);
            if (!erase) continue;
            try { await objects.DeleteSessionAsync(tenant, id, cancellationToken); }
            catch (IOException) { continue; } // Keep the durable deletion claim for the next sweep.
            await db.InTransactionAsync(async ct =>
            {
                var session = await db.UploadSessions.SingleAsync(s => s.TenantId == tenant && s.Id == id, ct);
                if (session.Status != "deleting") throw new InvalidOperationException("Cleanup authority changed.");
                await UploadReservations.ReleaseAsync(db, session, ct);
                session.Status = "cleaned"; session.CleanedAt = clock.UtcNow;
                await db.SaveChangesAsync(ct); return true;
            }, cancellationToken);
            deleted++;
        }
        return deleted;
    }
}
