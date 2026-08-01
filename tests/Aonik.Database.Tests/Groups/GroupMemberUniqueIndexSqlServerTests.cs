using Aonik.Groups.Persistence;
using Aonik.IntegrationTests.Support;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Database.Tests.Groups;

/// <summary>
/// Spec 086 §10.2 — the one non-additive schema change in the Groups extraction, and the reason it
/// had to be made.
///
/// Before this migration the uniqueness of a membership was an UNFILTERED unique index over
/// (TenantId, HouseholdId, UserId). SQL Server treats NULL as a value in a unique index, so the
/// moment <c>UserId</c> became nullable that index would have permitted exactly ONE member without
/// a login per group — which is precisely the thing the whole spec exists to enable (a family with
/// two children, neither of whom has an account). The replacement is two filtered unique indexes:
/// one over the users who have one, one over parties.
///
/// This lane is the only place any of that is observable. The InMemory provider does not enforce
/// unique indexes at all, so the InMemory suite would pass identically against the broken schema.
/// </summary>
public class GroupMemberUniqueIndexSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private const string UserIndexName = "IX_AnkHouseholdMembers_TenantId_HouseholdId_UserId";
    private const string PartyIndexName = "IX_AnkHouseholdMembers_TenantId_HouseholdId_PartyId";

    private readonly SqlLocalDbFixture _db;

    public GroupMemberUniqueIndexSqlServerTests(SqlLocalDbFixture db) => _db = db;

    private GroupsDbContext CreateContext(Guid tenantId)
        => new(_db.CreateOptions<GroupsDbContext>(), new TestTenantProvider(tenantId), new TestCurrentUserProvider());

    /// <summary>A member row needs its group to exist — HouseholdId is a real foreign key.</summary>
    private static async Task<Guid> SeedGroupAsync(GroupsDbContext ctx, Guid tenantId)
    {
        var group = new Household
        {
            TenantId = tenantId,
            Kind = GroupKinds.Household,
            Name = "Test group"
        };

        ctx.Groups.Add(group);
        await ctx.SaveChangesAsync();
        return group.Id;
    }

    private static HouseholdMember NewMember(Guid tenantId, Guid groupId, Guid? userId, Guid? partyId) => new()
    {
        TenantId = tenantId,
        HouseholdId = groupId,
        UserId = userId,
        PartyId = partyId,
        Role = HouseholdRoles.Viewer,
        PermissionsJson = "[]",
        InvitationStatus = HouseholdInvitationStatuses.Accepted
    };

    [SkippableFact]
    public async Task TwoMembersWithoutAUser_InOneGroup_Should_BothInsert()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        var groupId = await SeedGroupAsync(ctx, tenantId);

        // Two children in one family. Under the old unfiltered index the second insert failed —
        // this single assertion is the whole justification for touching the index.
        ctx.GroupMembers.Add(NewMember(tenantId, groupId, userId: null, partyId: Guid.NewGuid()));
        ctx.GroupMembers.Add(NewMember(tenantId, groupId, userId: null, partyId: Guid.NewGuid()));

        var act = () => ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    [SkippableFact]
    public async Task TheSameUser_TwiceInOneGroup_Should_StillBeRejected()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        var groupId = await SeedGroupAsync(ctx, tenantId);
        ctx.GroupMembers.Add(NewMember(tenantId, groupId, userId, partyId: Guid.NewGuid()));
        await ctx.SaveChangesAsync();

        // Filtering must not weaken the invariant it filters: a duplicate membership is still a
        // duplicate membership.
        ctx.GroupMembers.Add(NewMember(tenantId, groupId, userId, partyId: Guid.NewGuid()));

        var act = () => ctx.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        var sql = thrown.Which.InnerException.Should().BeOfType<SqlException>().Subject;
        sql.Number.Should().BeOneOf([2601, 2627]);
        sql.Message.Should().Contain(UserIndexName);
    }

    [SkippableFact]
    public async Task TheSameParty_TwiceInOneGroup_Should_BeRejected()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        var groupId = await SeedGroupAsync(ctx, tenantId);
        ctx.GroupMembers.Add(NewMember(tenantId, groupId, userId: null, partyId: partyId));
        await ctx.SaveChangesAsync();

        // The party index is the one that will carry the invariant once P5 drops UserId, so it has
        // to hold now, while both are populated — not only afterwards.
        ctx.GroupMembers.Add(NewMember(tenantId, groupId, userId: null, partyId: partyId));

        var act = () => ctx.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        var sql = thrown.Which.InnerException.Should().BeOfType<SqlException>().Subject;
        sql.Message.Should().Contain(PartyIndexName);
    }

    [SkippableFact]
    public async Task OneParty_InTwoDifferentGroups_Should_BeAllowed()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await using var ctx = CreateContext(tenantId);
        ctx.GroupMembers.Add(NewMember(tenantId, await SeedGroupAsync(ctx, tenantId), userId: null, partyId: partyId));
        ctx.GroupMembers.Add(NewMember(tenantId, await SeedGroupAsync(ctx, tenantId), userId: null, partyId: partyId));

        // A child in a separated family belongs to two households. Uniqueness is per group, not
        // per party.
        var act = () => ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    // Deliberately no cross-tenant case here, unlike the subscription index tests. HouseholdId is a
    // foreign key to a globally-unique primary key, so two tenants cannot share one — the leading
    // TenantId column cannot be the thing keeping them apart, and a test asserting otherwise would
    // pass without proving anything.

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
