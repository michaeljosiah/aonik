using Microsoft.EntityFrameworkCore;

using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;

namespace Aonik.Publishing.Services;

internal static class UploadReservations
{
    public static async Task ReserveAsync(IPublishingDataContext db, Guid tenant, Guid party, long bytes, PublishingIntakeOptions limits, CancellationToken ct)
    {
        foreach (var owner in new[] { Guid.Empty, party })
        {
            var row = await db.UploadQuotas.SingleOrDefaultAsync(q => q.TenantId == tenant && q.OwnerPartyId == owner, ct);
            if (row is null) { row = new PublicationUploadQuota { TenantId = tenant, OwnerPartyId = owner }; db.UploadQuotas.Add(row); }
            var ceiling = owner == Guid.Empty ? limits.MaxTenantRetainedBytes : limits.MaxCreatorRetainedBytes;
            var count = owner == Guid.Empty ? limits.MaxTenantPackages : limits.MaxCreatorPackages;
            if (bytes > ceiling - row.ReservedBytes || row.ReservedPackages >= count)
                throw new PublishingException(429, "upload-quota-exceeded", "The retained upload allowance is exhausted.");
            row.ReservedBytes += bytes; row.ReservedPackages++;
        }
    }
    public static void Retire(PublicationUploadSession session, string status, DateTime now)
    {
        // Retired bytes still occupy storage. Keep their reservation until deletion actually succeeds.
        session.Status = status; session.AssemblyToken = null; session.AssemblyExpiresAt = null; session.RetiredAt ??= now;
    }
    public static async Task ReleaseAsync(IPublishingDataContext db, PublicationUploadSession session, CancellationToken ct)
    {
        if (!session.ReservationReleased)
        {
            foreach (var owner in new[] { Guid.Empty, session.CreatorPartyId })
            {
                var row = await db.UploadQuotas.SingleAsync(q => q.TenantId == session.TenantId && q.OwnerPartyId == owner, ct);
                if (row.ReservedBytes < session.DeclaredBytes || row.ReservedPackages < 1) throw new InvalidOperationException("Upload reservation accounting is inconsistent.");
                row.ReservedBytes -= session.DeclaredBytes; row.ReservedPackages--;
            }
            session.ReservationReleased = true;
        }
    }
}
