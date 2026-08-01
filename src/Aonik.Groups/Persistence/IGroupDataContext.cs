using Aonik.PersonalFinance.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Aonik.Groups.Persistence;

/// <summary>
/// The unit of work <c>GroupService</c> writes through (Spec 086 §7.1).
/// </summary>
/// <remarks>
/// <para>
/// This interface exists for exactly one reason: <see cref="Aonik.SharedKernel.Abstractions.Groups.IGroupLifecycleContributor"/>
/// promises that a contributor's reaction runs <b>inside the same transaction</b> as the membership
/// write. Removing a member today clears the member's personal-finance profile link, unshares every
/// account they own and emits an integration event — all in one <c>SaveChangesAsync</c>. Split those
/// across two DbContexts and they become two transactions, because every module context in this
/// solution is registered with its own connection; a crash between them would leave a member removed
/// but still linked, or still linked but removed.
/// </para>
/// <para>
/// Rather than plumb a shared connection through ten module registrations — a platform-wide change
/// that deserves its own ADR — the group service writes through whichever context the composing
/// module already uses. PersonalFinance registers <c>PersonalFinanceDbContext</c> here, which
/// <a href="../../../docs/specifications/086.extract-groups-and-sharing-to-platform.html">§10.3</a>
/// keeps the four group <c>DbSet</c>s on precisely through P2–P6; the contributor writes through the
/// same instance, so one change tracker and one save cover the whole transition.
/// </para>
/// <para>
/// <c>GroupsDbContext</c> implements it too, for a consumer that has no other context of its own.
/// Which implementation is registered is the composing module's decision, and P7 is where it is
/// revisited — once PersonalFinance's <c>DbSet</c>s go, so does its claim on this interface.
/// </para>
/// </remarks>
public interface IGroupDataContext
{
    DbSet<Household> Groups { get; }

    DbSet<HouseholdMember> GroupMembers { get; }

    DbSet<CircleGrant> ShareGrants { get; }

    DbSet<CircleInvite> ShareInvites { get; }

    /// <summary>
    /// The transaction and retry surface. Not incidental: accepting an invitation has to hold a
    /// serializable transaction across the veto, the write and the contributors' reaction, or two
    /// concurrent accepts each pass their veto and the member ends up in two exclusive groups.
    /// </summary>
    DatabaseFacade Database { get; }

    /// <summary>
    /// Needed to discard a rolled-back unit after a concurrency conflict: two overlapping accepts of
    /// one invite race on its <c>RowVersion</c>, and the loser has to drop its edits before resolving
    /// against the committed winner.
    /// </summary>
    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
