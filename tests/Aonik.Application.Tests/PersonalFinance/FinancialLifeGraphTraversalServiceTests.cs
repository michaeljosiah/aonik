using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Caching;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.PersonalFinance;

public class FinancialLifeGraphTraversalServiceTests
{
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

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"GraphTraversal_{Guid.NewGuid()}")
            .Options;
        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static FinancialLifeGraphTraversalService CreateTraversalService(
        FinanceDbContext context,
        Guid tenantId,
        Guid userId)
    {
        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var cacheStore = new TestCacheStore();
        var loader = new FinancialLifeGraphLoader(context);
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

        context.PersonalAccounts.AddRange(
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

        context.Bills.Add(new Bill
        {
            Id = billId, TenantId = tenantId, UserId = userId,
            Payee = "Electric Co", Frequency = "Monthly",
            NextDueDate = DateTime.UtcNow.AddDays(15),
            ExpectedAmount = 120m, Currency = "USD",
            PaidFromAccountId = accountId, Status = "Active",
            CreatedAt = DateTime.UtcNow
        });

        context.Goals.Add(new Goal
        {
            Id = goalId, TenantId = tenantId, UserId = userId,
            Name = "Emergency Fund", TargetAmount = 10000m, Currency = "USD",
            ProgressAmount = 3000m, Status = "Active",
            FundingAccountId = account2Id, CreatedAt = DateTime.UtcNow
        });

        context.PersonalTransactions.AddRange(
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

        await context.SaveChangesAsync();
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

        context.PersonalAccounts.Add(new PersonalAccount
        {
            Id = accountId, TenantId = tenantId, UserId = userId,
            Name = "Checking", AccountType = "Checking", Currency = "USD",
            Status = "Active", CurrentBalance = 1000m, CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

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

        context.PersonalAccounts.Add(new PersonalAccount
        {
            Id = accountId, TenantId = tenantId, UserId = userId,
            Name = "Test", AccountType = "Checking", Currency = "USD",
            Status = "Active", CurrentBalance = 0m, CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

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
