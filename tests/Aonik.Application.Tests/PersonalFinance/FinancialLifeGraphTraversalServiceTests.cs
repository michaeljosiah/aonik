using Aonik.PersonalFinance.Contracts.Models;
using Aonik.Finance.Entities;
using Aonik.PersonalFinance.Entities;
using Aonik.Finance.Persistence;
using Aonik.PersonalFinance.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Caching;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.PersonalFinance;

public class FinancialLifeGraphTraversalServiceTests
{
    private sealed class TestPartyReader : IPartyReader
    {
        private readonly FinanceDbContext _db;
        public TestPartyReader(FinanceDbContext db) => _db = db;

        public async Task<IReadOnlyList<PartyHistoryItem>> GetByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> partyIds, CancellationToken ct = default)
            => partyIds.Count == 0
                ? []
                : await _db.Parties.AsNoTracking()
                    .Where(p => p.TenantId == tenantId && partyIds.Contains(p.Id))
                    .Select(p => new PartyHistoryItem(p.Id, p.DisplayName, p.Status, p.CustomerTierCode))
                    .ToListAsync(ct);

        public async Task<IReadOnlyList<PartyRelationshipHistoryItem>> GetRelationshipsForPartyAsync(
            Guid tenantId, Guid partyId, CancellationToken ct = default)
            => await _db.PartyRelationships.AsNoTracking()
                .Where(r => r.TenantId == tenantId && r.IsActive
                    && (r.FromPartyId == partyId || r.ToPartyId == partyId))
                .OrderBy(r => r.RelationshipTypeCode)
                .Select(r => new PartyRelationshipHistoryItem(
                    r.Id, r.FromPartyId, r.ToPartyId, r.RelationshipTypeCode, r.IsActive, r.Notes))
                .ToListAsync(ct);

        public Task<bool> ExistsAsync(Guid tenantId, Guid partyId, CancellationToken ct = default)
            => _db.Parties.AsNoTracking()
                .AnyAsync(p => p.TenantId == tenantId && p.Id == partyId, ct);

        public Task<bool> HasActiveRelationshipBetweenAsync(
            Guid tenantId, Guid partyAId, Guid partyBId, CancellationToken ct = default)
            => _db.PartyRelationships.AsNoTracking()
                .AnyAsync(r => r.TenantId == tenantId && r.IsActive
                    && ((r.FromPartyId == partyAId && r.ToPartyId == partyBId)
                        || (r.ToPartyId == partyAId && r.FromPartyId == partyBId)), ct);

        public async Task<Guid?> GetTenantPartyIdAsync(Guid tenantId, CancellationToken ct = default)
            => await _db.Parties.AsNoTracking()
                .Where(p => p.TenantId == tenantId)
                .OrderBy(p => p.Id)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);
    }

    private sealed class TestUserDirectoryReader : IUserDirectoryReader
    {
        private readonly FinanceDbContext _db;
        public TestUserDirectoryReader(FinanceDbContext db) => _db = db;

        public async Task<IReadOnlyList<UserDirectoryItem>> GetByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            => userIds.Count == 0
                ? []
                : await _db.Users.AsNoTracking()
                    .Where(u => u.TenantId == tenantId && userIds.Contains(u.Id))
                    .Select(u => new UserDirectoryItem(u.Id, u.Email, u.Status))
                    .ToListAsync(ct);
    }

    private sealed class TestCacheStore : ICacheStore
    {
        private readonly Dictionary<string, object?> _items = new(StringComparer.Ordinal);

        public async Task<T?> GetOrSetAsync<T>(string key, CachePolicy policy, Func<CancellationToken, Task<T?>> factory, string cacheSet, CancellationToken cancellationToken = default)
        {
            if (_items.TryGetValue(key, out var value))
            {
                return (T?)value;
            }

            var created = await factory(cancellationToken);
            _items[key] = created;
            return created;
        }

        public void Remove(string key)
        {
            _items.Remove(key);
        }
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetCurrentTenantId() => _tenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId;
        public TestCurrentUserProvider(Guid userId) => _userId = userId;
        public Guid? GetCurrentUserId() => _userId;
        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }

    private static string _lastDbName = string.Empty;

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        _lastDbName = $"GraphTraversal_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase(_lastDbName)
            .Options;
        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static Aonik.PersonalFinance.Persistence.PersonalFinanceDbContext CreatePersonalFinanceDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<Aonik.PersonalFinance.Persistence.PersonalFinanceDbContext>()
            .UseInMemoryDatabase(_lastDbName)
            .Options;
        return new Aonik.PersonalFinance.Persistence.PersonalFinanceDbContext(
            options, new TestTenantProvider(tenantId));
    }

    private static FinancialLifeGraphTraversalService CreateTraversalService(
        FinanceDbContext context,
        Guid tenantId,
        Guid userId)
    {
        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var cacheStore = new TestCacheStore();
        var pfContext = CreatePersonalFinanceDbContext(tenantId);
        var loader = new FinancialLifeGraphLoader(
            pfContext,
            new Aonik.Finance.Services.Finance.Readers.CustomerOrderHistoryReader(context),
            new Aonik.Finance.Services.Finance.Readers.CustomerInvoiceHistoryReader(context),
            new Aonik.Finance.Services.Finance.Readers.CustomerPaymentHistoryReader(context),
            new Aonik.Finance.Services.Finance.Readers.FxQuoteReader(context),
            new TestPartyReader(context),
            new TestUserDirectoryReader(context));
        var metrics = new FinancialLifeGraphSnapshotMetrics(NullLogger<FinancialLifeGraphSnapshotMetrics>.Instance);
        var hydrationService = new FinancialLifeGraphHydrationService(
            tenantProvider, currentUserProvider, cacheStore, loader, metrics);

        return new FinancialLifeGraphTraversalService(hydrationService);
    }

    private static async Task SeedStandardGraph(
        FinanceDbContext context,
        Guid tenantId,
        Guid userId)
    {
        var accountId = Guid.NewGuid();
        var account2Id = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var txnId = Guid.NewGuid();
        var txn2Id = Guid.NewGuid();

        await using var pfSeedContext = CreatePersonalFinanceDbContext(tenantId);

        pfSeedContext.PersonalAccounts.AddRange(
            new PersonalAccount
            {
                Id = accountId, TenantId = tenantId, UserId = userId,
                Name = "Main Checking", AccountType = "Checking", Currency = "USD",
                Status = "Active", CurrentBalance = 5000m, CreatedAt = DateTime.UtcNow
            },
            new PersonalAccount
            {
                Id = account2Id, TenantId = tenantId, UserId = userId,
                Name = "Savings", AccountType = "Savings", Currency = "USD",
                Status = "Active", CurrentBalance = 10000m, CreatedAt = DateTime.UtcNow
            });

        pfSeedContext.Bills.Add(new Bill
        {
            Id = billId, TenantId = tenantId, UserId = userId,
            Payee = "Electric Co", Frequency = "Monthly",
            NextDueDate = DateTime.UtcNow.AddDays(15),
            ExpectedAmount = 120m, Currency = "USD",
            PaidFromAccountId = accountId, Status = "Active",
            CreatedAt = DateTime.UtcNow
        });

        pfSeedContext.Goals.Add(new Goal
        {
            Id = goalId, TenantId = tenantId, UserId = userId,
            Name = "Emergency Fund", TargetAmount = 10000m, Currency = "USD",
            ProgressAmount = 3000m, Status = "Active",
            FundingAccountId = account2Id, CreatedAt = DateTime.UtcNow
        });

        pfSeedContext.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                Id = txnId, TenantId = tenantId, UserId = userId,
                PersonalAccountId = accountId, Amount = -50m, Currency = "USD",
                Merchant = "Grocery Store", Description = "Weekly groceries",
                OccurredAt = DateTime.UtcNow.AddDays(-5),
                SourceType = "Manual", TransactionType = "Expense",
                CreatedAt = DateTime.UtcNow
            },
            new PersonalTransaction
            {
                Id = txn2Id, TenantId = tenantId, UserId = userId,
                PersonalAccountId = accountId, Amount = -120m, Currency = "USD",
                Merchant = "Electric Co", Description = "Monthly bill",
                OccurredAt = DateTime.UtcNow.AddDays(-10),
                SourceType = "Manual", TransactionType = "Expense",
                CreatedAt = DateTime.UtcNow
            });

        await pfSeedContext.SaveChangesAsync();
    }

    [Fact]
    public async Task GetNeighboursAsync_ShouldReturnNeighbours_ForUserRootNode()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        var userRootKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.User, userId);

        // Act
        var result = await service.GetNeighboursAsync(userRootKey);

        // Assert
        result.Should().NotBeNull();
        result.AnchorNodeKey.Should().Be(userRootKey);
        result.NeighbourCount.Should().BeGreaterThan(0, "UserRoot should have neighbours (accounts, bills, goals)");
        result.Neighbours.Should().NotBeEmpty();
        result.Edges.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetNeighboursAsync_ShouldFilterByPredicate_WhenPredicateProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        var userRootKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.User, userId);

        // Act
        var result = await service.GetNeighboursAsync(userRootKey, predicate: FinancialLifeGraphPredicates.OwnsAccount);

        // Assert
        result.Edges.Should().OnlyContain(e => e.Predicate == FinancialLifeGraphPredicates.OwnsAccount);
        result.Neighbours.Should().OnlyContain(n => n.NodeType == FinancialLifeGraphNodeTypes.PersonalAccount);
    }

    [Fact]
    public async Task GetNeighboursAsync_ShouldFilterByDirection_WhenDirectionProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        var userRootKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.User, userId);

        // Act
        var outboundResult = await service.GetNeighboursAsync(userRootKey, direction: "OUTBOUND");

        // Assert — all edges should have the user root as the "from" node
        outboundResult.Edges.Should().OnlyContain(e =>
            e.FromNodeKey.Equals(userRootKey, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetNeighboursAsync_ShouldReturnEmpty_WhenNodeKeyDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        // Act
        var result = await service.GetNeighboursAsync("nonexistent:00000000-0000-0000-0000-000000000000");

        // Assert
        result.NeighbourCount.Should().Be(0);
        result.Neighbours.Should().BeEmpty();
        result.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task ExpandSubgraphAsync_ShouldExpandBFS_FromAnchorNode()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        var userRootKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.User, userId);

        // Act
        var result = await service.ExpandSubgraphAsync(userRootKey, maxDepth: 2);

        // Assert
        result.Should().NotBeNull();
        result.AnchorNodeKey.Should().Be(userRootKey);
        result.MaxDepth.Should().Be(2);
        result.NodeCount.Should().BeGreaterThan(1, "Subgraph should include anchor + neighbours");
        result.Nodes.Should().Contain(n => n.NodeKey == userRootKey);
        result.Edges.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExpandSubgraphAsync_ShouldClampMaxDepth_WhenExceedsLimit()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        var userRootKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.User, userId);

        // Act — request depth 50 (exceeds MaxTraversalDepth=10)
        var result = await service.ExpandSubgraphAsync(userRootKey, maxDepth: 50);

        // Assert — should still succeed; depth is clamped
        result.MaxDepth.Should().Be(10);
        result.Nodes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExpandSubgraphAsync_ShouldReturnEmpty_WhenNodeKeyDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        // Act
        var result = await service.ExpandSubgraphAsync("nonexistent:00000000-0000-0000-0000-000000000000");

        // Assert
        result.NodeCount.Should().Be(0);
        result.EdgeCount.Should().Be(0);
        result.Nodes.Should().BeEmpty();
        result.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task ExpandSubgraphAsync_ShouldFilterByPredicate_WhenPredicateProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        var userRootKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.User, userId);

        // Act
        var result = await service.ExpandSubgraphAsync(userRootKey, maxDepth: 1,
            predicateFilter: FinancialLifeGraphPredicates.OwnsAccount);

        // Assert
        result.Edges.Should().OnlyContain(e => e.Predicate == FinancialLifeGraphPredicates.OwnsAccount);
        // Nodes should be anchor + accounts only
        result.Nodes.Should().Contain(n => n.NodeType == FinancialLifeGraphNodeTypes.PersonalAccount);
    }

    [Fact]
    public async Task GetNodesByTypeAsync_ShouldReturnAllNodesOfType()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        // Act
        var accounts = await service.GetNodesByTypeAsync(FinancialLifeGraphNodeTypes.PersonalAccount);

        // Assert — we seeded 2 accounts
        accounts.Should().HaveCount(2);
        accounts.Should().OnlyContain(n => n.NodeType == FinancialLifeGraphNodeTypes.PersonalAccount);
    }

    [Fact]
    public async Task GetNodesByTypeAsync_ShouldReturnEmpty_WhenNoNodesOfTypeExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        // Seed graph but we won't add any subscriptions
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        // Act
        var subscriptions = await service.GetNodesByTypeAsync(FinancialLifeGraphNodeTypes.Subscription);

        // Assert
        subscriptions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEdgesByPredicateAsync_ShouldReturnMatchingEdges()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        // Act
        var edges = await service.GetEdgesByPredicateAsync(FinancialLifeGraphPredicates.OwnsAccount);

        // Assert — seeded 2 accounts, so 2 OWNS_ACCOUNT edges
        edges.Should().HaveCount(2);
        edges.Should().OnlyContain(e => e.Predicate == FinancialLifeGraphPredicates.OwnsAccount);
    }

    [Fact]
    public async Task GetEdgesByPredicateAsync_ShouldFilterByFromNodeType()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        // Act
        var edges = await service.GetEdgesByPredicateAsync(
            FinancialLifeGraphPredicates.HasTransaction,
            fromNodeType: FinancialLifeGraphNodeTypes.PersonalAccount);

        // Assert
        edges.Should().NotBeEmpty("accounts should have HAS_TRANSACTION edges");
    }

    [Fact]
    public async Task GetNodeContextAsync_ShouldReturnNodeWithEdgesAndNeighbours()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        var userRootKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.User, userId);

        // Act
        var result = await service.GetNodeContextAsync(userRootKey);

        // Assert
        result.Should().NotBeNull();
        result!.Node.NodeKey.Should().Be(userRootKey);
        result.Edges.Should().NotBeEmpty();
        result.Neighbours.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetNodeContextAsync_ShouldReturnNull_WhenNodeKeyDoesNotExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        // Act
        var result = await service.GetNodeContextAsync("nonexistent:00000000-0000-0000-0000-000000000000");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindPathAsync_ShouldFindPath_BetweenConnectedNodes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        await using var pfSeedContext = CreatePersonalFinanceDbContext(tenantId);
        pfSeedContext.PersonalAccounts.Add(new PersonalAccount
        {
            Id = accountId, TenantId = tenantId, UserId = userId,
            Name = "Checking", AccountType = "Checking", Currency = "USD",
            Status = "Active", CurrentBalance = 1000m, CreatedAt = DateTime.UtcNow
        });
        await pfSeedContext.SaveChangesAsync();

        var service = CreateTraversalService(context, tenantId, userId);

        var userRootKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.User, userId);
        var accountKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.PersonalAccount, accountId);

        // Act
        var result = await service.FindPathAsync(userRootKey, accountKey);

        // Assert
        result.Should().NotBeNull();
        result.PathExists.Should().BeTrue();
        result.PathLength.Should().Be(1, "UserRoot -> PersonalAccount is 1 hop via OWNS_ACCOUNT");
        result.PathNodes.Should().NotBeNull();
        result.PathNodes.Should().HaveCount(2);
        result.PathEdges.Should().NotBeNull();
        result.PathEdges.Should().HaveCount(1);
    }

    [Fact]
    public async Task FindPathAsync_ShouldReturnSelfPath_WhenFromAndToAreSame()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        var userRootKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.User, userId);

        // Act
        var result = await service.FindPathAsync(userRootKey, userRootKey);

        // Assert
        result.PathExists.Should().BeTrue();
        result.PathLength.Should().Be(0);
        result.PathNodes.Should().HaveCount(1);
        result.PathEdges.Should().BeEmpty();
    }

    [Fact]
    public async Task FindPathAsync_ShouldReturnNoPath_WhenNodesNotConnected()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedStandardGraph(context, tenantId, userId);
        var service = CreateTraversalService(context, tenantId, userId);

        // Act — try to find a path to a node that doesn't exist
        var result = await service.FindPathAsync(
            FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.User, userId),
            "nonexistent:00000000-0000-0000-0000-000000000000");

        // Assert
        result.PathExists.Should().BeFalse();
        result.PathLength.Should().BeNull();
        result.PathNodes.Should().BeNull();
        result.PathEdges.Should().BeNull();
    }

    [Fact]
    public async Task FindPathAsync_ShouldClampMaxDepth_WhenExceedsLimit()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        await using var pfSeedContext = CreatePersonalFinanceDbContext(tenantId);
        pfSeedContext.PersonalAccounts.Add(new PersonalAccount
        {
            Id = accountId, TenantId = tenantId, UserId = userId,
            Name = "Test", AccountType = "Checking", Currency = "USD",
            Status = "Active", CurrentBalance = 0m, CreatedAt = DateTime.UtcNow
        });
        await pfSeedContext.SaveChangesAsync();

        var service = CreateTraversalService(context, tenantId, userId);

        var userRootKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.User, userId);
        var accountKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.PersonalAccount, accountId);

        // Act — request maxDepth=0 (should clamp to 1)
        var result = await service.FindPathAsync(userRootKey, accountKey, maxDepth: 0);

        // Assert — path of length 1 should still be found since depth is clamped to 1
        result.PathExists.Should().BeTrue();
    }

    [Fact]
    public async Task GetNeighboursAsync_ShouldHandleEmptyGraph()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        // No data seeded — just user root will exist
        var service = CreateTraversalService(context, tenantId, userId);

        var userRootKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.User, userId);

        // Act
        var result = await service.GetNeighboursAsync(userRootKey);

        // Assert — UserRoot exists but may have no neighbours if no data
        result.Should().NotBeNull();
        result.AnchorNodeKey.Should().Be(userRootKey);
    }
}
