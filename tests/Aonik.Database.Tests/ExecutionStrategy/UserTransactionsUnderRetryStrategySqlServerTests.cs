using Aonik.Groups.Services;
using Aonik.Database.Tests.Support;
using Aonik.IntegrationTests.Support;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.PersonalFinance.Services;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Finance;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace Aonik.Database.Tests.ExecutionStrategy;

/// <summary>
/// Every user-initiated transaction site in the codebase, executed against real
/// SQL Server under the production provider options (<c>EnableRetryOnFailure</c>).
///
/// EF rejects <c>BeginTransactionAsync</c> under a retrying execution strategy
/// unless the whole sequence runs through <c>Database.CreateExecutionStrategy()</c>:
/// "The configured execution strategy 'SqlServerRetryingExecutionStrategy' does
/// not support user-initiated transactions." The InMemory provider is
/// non-relational — <c>IsRelational()</c> is false and no transaction is ever
/// opened — so this failure mode is structurally unreachable from the InMemory
/// suites. Spec 066 (PR #257) shipped exactly this defect in
/// <c>SetRecommendedDefaultAsync</c>: a 100% failure on SQL Server behind 2,198
/// green tests.
///
/// One test per BeginTransaction site. Deleting the CreateExecutionStrategy
/// wrapper from any of them makes its test here throw that
/// InvalidOperationException on the first call.
/// </summary>
public class UserTransactionsUnderRetryStrategySqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;

    public UserTransactionsUnderRetryStrategySqlServerTests(SqlLocalDbFixture db)
    {
        _db = db;
    }

    // ─── Site 1: Commerce — ProductOptionService.SetRecommendedDefaultAsync ─────

    [SkippableFact]
    public async Task SetRecommendedDefault_Should_MoveTheDefaultTransactionally_When_RunOnSqlServerWithRetryStrategy()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var (groupId, _) = await CommerceSqlServerHarness.SeedPortionGroupAndProductAsync(_db, tenantId);

        await using var context = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var service = CommerceSqlServerHarness.CreateOptionService(context, tenantId);

        var result = await service.SetRecommendedDefaultAsync(groupId, "full");

        result.Group.Choices.Single(c => c.Key == "full").IsRecommendedDefault.Should().BeTrue();
        result.Group.Choices.Single(c => c.Key == "light").IsRecommendedDefault.Should().BeFalse();

        // Reload through a fresh context: the demote and the promote committed
        // together, leaving exactly one active default behind the filtered index.
        await using var verify = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var choices = await verify.OptionChoices.Where(c => c.OptionGroupId == groupId).ToListAsync();
        choices.Single(c => c.Key == "full").IsRecommendedDefault.Should().BeTrue();
        choices.Single(c => c.Key == "light").IsRecommendedDefault.Should().BeFalse();
    }

    // ─── Site 2: PersonalFinance — CommitmentService.MarkDoneAsync ──────────────

    [SkippableFact]
    public async Task MarkDone_Should_ResolveCycleAndWritePaymentLog_When_RunOnSqlServerWithRetryStrategy()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var context = CreatePersonalFinanceContext(tenantId);
        var tenantProvider = new TestTenantProvider(tenantId);
        var userProvider = new TestCurrentUserProvider(userId);
        var payments = new PaymentLogService(context, tenantProvider, userProvider);
        var commitments = new CommitmentService(
            context, tenantProvider, userProvider, payments, new FakeTaskService(), NullLogger<CommitmentService>.Instance);
        var care = new CareEntityService(
            context, tenantProvider, userProvider,
            new Mock<IDocumentReader> { DefaultValue = DefaultValue.Empty }.Object,
            NullLogger<CareEntityService>.Instance);

        var entity = await care.CreateAsync(new CreateCareEntityRequest("person", null, "Mum", "NG", null, null, null, null));
        var created = await commitments.CreateSupportAsync(new CreateSupportCommitmentRequest(
            entity.Id, "Mum — monthly allowance", 200m, "GBP", "Monthly", 1, 28,
            null, new DateTime(2026, 5, 28), 3, null, null));

        var result = await commitments.MarkDoneAsync(created.CommitmentId, new MarkCommitmentDoneRequest(
            200m, "GBP", null, new DateTime(2026, 5, 28), "bank", null, Guid.NewGuid()));

        result.Should().NotBeNull();
        result!.LastPaidAt.Should().Be(new DateTime(2026, 5, 28));
        result.DueDate.Should().Be(new DateTime(2026, 6, 28), "mark-done rolls the bill to the next monthly cycle");

        // The Serializable transaction committed the payment log and the cycle
        // resolution as one unit of work.
        await using var verify = CreatePersonalFinanceContext(tenantId);
        (await verify.PaymentLogs.CountAsync(p => p.CommitmentId == created.CommitmentId)).Should().Be(1);
        var cycles = await verify.CommitmentCycles.Where(c => c.CommitmentId == created.CommitmentId).ToListAsync();
        cycles.Should().HaveCount(2, "the paid cycle plus the newly opened one");
        cycles.Count(c => c.Status == "Paid").Should().Be(1);
    }

    // ─── Site 3: PersonalFinance — FinancialLifeGraphWriteService.CreateEdgeAsync ─

    [SkippableFact]
    public async Task CreateEdge_Should_PersistNodeAndEdge_When_RunOnSqlServerWithRetryStrategy()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var context = CreatePersonalFinanceContext(tenantId);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = Guid.NewGuid(),
        });
        await context.SaveChangesAsync();

        var tenantProvider = new TestTenantProvider(tenantId);
        var userProvider = new TestCurrentUserProvider(userId);
        var validation = new FinancialLifeGraphValidationService(
            context,
            tenantProvider,
            userProvider,
            new FinancialLifeGraphSchema(),
            new StubPartyReader(),
            new Mock<ICustomerOrderHistoryReader> { DefaultValue = DefaultValue.Empty }.Object,
            new Mock<ICustomerInvoiceHistoryReader> { DefaultValue = DefaultValue.Empty }.Object,
            new Mock<ICustomerPaymentHistoryReader> { DefaultValue = DefaultValue.Empty }.Object,
            new GroupService(
                context,
                tenantProvider,
                userProvider,
                Mock.Of<IUserPartyResolver>(),
                Mock.Of<IPartyReader>(),
                new FixedClock(),
                [],
                new PersonalFinancePartyResolver(context)));
        var writeService = new FinancialLifeGraphWriteService(
            context, tenantProvider, userProvider, validation, new NoOpGraphCacheInvalidator());

        var node = await writeService.CreateNodeAsync(new CreateFinancialLifeGraphNodeRequest(
            "NativeAnnotation", "Regular support", "{\"category\":\"family\"}",
            null, null, null, FinancialLifeGraphEntityStatus.Active, false, null));

        var edge = await writeService.CreateEdgeAsync(new CreateFinancialLifeGraphEdgeRequest(
            $"user:{userId:D}", "ANNOTATED_AS", node.NodeKey,
            "{}", null, FinancialLifeGraphEntityStatus.Active, false, null));

        edge.GraphEdgeId.Should().NotBeEmpty();

        await using var verify = CreatePersonalFinanceContext(tenantId);
        (await verify.FinancialLifeGraphNodes.CountAsync()).Should().Be(1);
        (await verify.FinancialLifeGraphEdges.CountAsync()).Should().Be(1);
    }

    // ─── Site 4: PersonalFinance — HouseholdService.AcceptInvitationAsync ────────

    [SkippableFact]
    public async Task AcceptInvitation_Should_AssignHousehold_When_RunOnSqlServerWithRetryStrategy()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var inviteeUserId = Guid.NewGuid();
        var clock = new FixedClock();
        var partyReader = new StubPartyReader();
        var directoryReader = new StubUserDirectoryReader();

        await using var context = CreatePersonalFinanceContext(tenantId);
        SeedProfile(context, partyReader, directoryReader, tenantId, ownerUserId, "owner@example.com", "Owner");
        SeedProfile(context, partyReader, directoryReader, tenantId, inviteeUserId, "invitee@example.com", "Invitee");

        var household = new Household { TenantId = tenantId, Name = "Family" };
        context.Households.Add(household);
        context.HouseholdMembers.AddRange(
            new HouseholdMember
            {
                TenantId = tenantId,
                HouseholdId = household.Id,
                UserId = ownerUserId,
                Role = HouseholdRoles.Owner,
                PermissionsJson = "[]",
                InvitationStatus = HouseholdInvitationStatuses.Accepted,
                InvitedAt = clock.UtcNow.AddDays(-10),
                RespondedAt = clock.UtcNow.AddDays(-10),
            },
            new HouseholdMember
            {
                TenantId = tenantId,
                HouseholdId = household.Id,
                UserId = inviteeUserId,
                Role = HouseholdRoles.Viewer,
                PermissionsJson = "[]",
                InvitationStatus = HouseholdInvitationStatuses.Pending,
                InvitedByUserId = ownerUserId,
                InvitedAt = clock.UtcNow.AddDays(-1),
                ExpiresAt = clock.UtcNow.AddDays(13),
            });
        await context.SaveChangesAsync();

        var service = new HouseholdService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(inviteeUserId),
            new NoOpGraphCacheInvalidator(),
            partyReader,
            directoryReader,
            clock,
            new NoOpNotificationWriter(),
            new MemberPartyResolver(context, Mock.Of<IUserPartyResolver>()),
            new GroupService(
                context,
                new TestTenantProvider(tenantId),
                new TestCurrentUserProvider(inviteeUserId),
                Mock.Of<IUserPartyResolver>(),
                partyReader,
                clock,
                [new PersonalFinanceGroupLifecycleContributor(context, new TestTenantProvider(tenantId), clock)],
                new PersonalFinancePartyResolver(context)));

        var response = await service.AcceptInvitationAsync(household.Id);

        response.InvitationStatus.Should().Be(HouseholdInvitationStatuses.Accepted);

        // The Serializable transaction committed the membership flip and the
        // profile's household assignment together.
        await using var verify = CreatePersonalFinanceContext(tenantId);
        var member = await verify.HouseholdMembers.SingleAsync(m => m.HouseholdId == household.Id && m.UserId == inviteeUserId);
        member.InvitationStatus.Should().Be(HouseholdInvitationStatuses.Accepted);
        var profile = await verify.PersonalProfiles.SingleAsync(p => p.UserId == inviteeUserId);
        profile.HouseholdId.Should().Be(household.Id);
    }

    // ─── Plumbing ────────────────────────────────────────────────────────────────

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");

    private PersonalFinanceDbContext CreatePersonalFinanceContext(Guid tenantId)
        => new(_db.CreateOptions<PersonalFinanceDbContext>(), new TestTenantProvider(tenantId));

    private static void SeedProfile(
        PersonalFinanceDbContext context,
        StubPartyReader partyReader,
        StubUserDirectoryReader directoryReader,
        Guid tenantId,
        Guid userId,
        string email,
        string displayName)
    {
        var partyId = Guid.NewGuid();
        directoryReader.Users[userId] = new UserDirectoryItem(userId, email, "Active");
        partyReader.Parties[partyId] = new PartyHistoryItem(partyId, displayName, "Active", null);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId,
        });
    }
}
