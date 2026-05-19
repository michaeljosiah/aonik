using Aonik.Finance.Entities;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Caching;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Aonik.Application.Tests.PersonalFinance;

public class FinancialLifeGraphServiceTests
{
    // Spec 027: PartyReader / UserDirectoryReader live in Aonik.Platform but
    // tests still use FinanceDbContext, which carries the legacy read-model
    // projections for these aggregates. These adapters bridge the two so the
    // test fixture doesn't need a separate PlatformDbContext.
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

    private sealed class TestCacheInvalidationPublisher : ICacheInvalidationPublisher
    {
        public event Func<CacheInvalidationEvent, CancellationToken, Task>? Invalidated;

        public async Task PublishAsync(CacheInvalidationEvent cacheInvalidationEvent, CancellationToken cancellationToken = default)
        {
            if (Invalidated != null)
            {
                await Invalidated.Invoke(cacheInvalidationEvent, cancellationToken);
            }
        }
    }

    private sealed class NoOpGraphCacheInvalidator : IFinancialLifeGraphCacheInvalidator
    {
        public void InvalidateCurrentUserGraph()
        {
        }

        public Task InvalidateCurrentUserGraphAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InvalidateUserGraphAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InvalidateUserGraphsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InvalidateAllGraphCachesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId)
        {
            _tenantId = tenantId;
        }

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

        public TestCurrentUserProvider(Guid userId)
        {
            _userId = userId;
        }

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
        _lastDbName = $"FinancialLifeGraph_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase(_lastDbName)
            .Options;

        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static AgentsDbContext CreateAgentsDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AgentsDbContext>()
            .UseInMemoryDatabase($"AgentsDb_{Guid.NewGuid()}")
            .Options;

        return new AgentsDbContext(options, new TestTenantProvider(tenantId));
    }

    private static Aonik.PersonalFinance.Persistence.PersonalFinanceDbContext CreatePersonalFinanceDbContext(
        string sharedDbName, Guid tenantId)
    {
        // Spec 027 Phase 3: FinancialLifeGraphLoader was relocated to
        // Aonik.PersonalFinance and now depends on PersonalFinanceDbContext.
        // Both contexts share the same in-memory store keyed by `sharedDbName`.
        var options = new DbContextOptionsBuilder<Aonik.PersonalFinance.Persistence.PersonalFinanceDbContext>()
            .UseInMemoryDatabase(sharedDbName)
            .Options;
        return new Aonik.PersonalFinance.Persistence.PersonalFinanceDbContext(
            options, new TestTenantProvider(tenantId));
    }

    private static FinancialLifeGraphService CreateGraphService(
        FinanceDbContext context,
        Guid tenantId,
        Guid userId,
        ICacheStore cacheStore)
    {
        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var pfContext = CreatePersonalFinanceDbContext(_lastDbName, tenantId);
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
            tenantProvider,
            currentUserProvider,
            cacheStore,
            loader,
            metrics);

        return new FinancialLifeGraphService(hydrationService);
    }

    [Fact]
    public async Task GetGraphAsync_Should_ProjectExpectedNodesAndEdges()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var householdId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var linkedAccountId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var relatedPartyId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();

        await using var context = CreateDbContext(tenantId);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId,
            HouseholdId = householdId
        });
        context.Households.Add(new Household
        {
            Id = householdId,
            TenantId = tenantId,
            Name = "Home"
        });
        context.HouseholdMembers.Add(new HouseholdMember
        {
            TenantId = tenantId,
            HouseholdId = householdId,
            UserId = userId,
            Role = "Owner",
            PermissionsJson = "[]"
        });
        context.PersonalAccounts.Add(new PersonalAccount
        {
            Id = accountId,
            TenantId = tenantId,
            UserId = userId,
            Name = "Main Account",
            AccountType = "Bank",
            Currency = "USD",
            Status = "Active"
        });
        context.PersonalLinkedAccounts.Add(new PersonalLinkedAccount
        {
            Id = linkedAccountId,
            TenantId = tenantId,
            UserId = userId,
            FinancialConnectionId = Guid.NewGuid(),
            PersonalAccountId = accountId,
            ProviderAccountReference = "provider-1",
            Name = "Linked Checking",
            AccountType = "Checking",
            Currency = "USD",
            Status = "Active"
        });
        context.PersonalTransactions.Add(new PersonalTransaction
        {
            Id = transactionId,
            TenantId = tenantId,
            UserId = userId,
            PersonalAccountId = accountId,
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow.AddDays(-1),
            Amount = 42.5m,
            Currency = "USD",
            Merchant = "Store",
            Category = "Groceries",
            ReviewStatus = "Reviewed"
        });
        context.Bills.Add(new Bill
        {
            Id = billId,
            TenantId = tenantId,
            UserId = userId,
            PaidFromAccountId = accountId,
            LinkedOrderId = orderId,
            LinkedInvoiceId = invoiceId,
            Payee = "Utility",
            Frequency = "Monthly",
            NextDueDate = DateTime.UtcNow.AddDays(5),
            ExpectedAmount = 80m,
            Currency = "USD",
            Status = "Active"
        });
        context.Goals.Add(new Goal
        {
            Id = goalId,
            TenantId = tenantId,
            UserId = userId,
            FundingAccountId = accountId,
            Name = "Emergency Fund",
            TargetAmount = 1000m,
            ProgressAmount = 250m,
            Currency = "USD",
            TargetDate = DateTime.UtcNow.AddDays(10),
            Status = "Active"
        });
        context.Subscriptions.Add(new Subscription
        {
            Id = subscriptionId,
            TenantId = tenantId,
            UserId = userId,
            Merchant = "Streaming",
            RenewalDate = DateTime.UtcNow.AddDays(3),
            ExpectedAmount = 12m,
            Currency = "USD",
            Status = "Active",
            DetectedBy = "manual"
        });
        context.Parties.Add(new PartyReadModel
        {
            Id = relatedPartyId,
            TenantId = tenantId,
            DisplayName = "Mum",
            Status = "Active"
        });
        context.PartyRelationships.Add(new PartyRelationshipReadModel
        {
            TenantId = tenantId,
            FromPartyId = partyId,
            ToPartyId = relatedPartyId,
            RelationshipTypeCode = "Mother",
            IsActive = true,
            Notes = "Family support"
        });
        context.Orders.Add(new Aonik.Finance.Entities.Orders.Order
        {
            Id = orderId,
            TenantId = tenantId,
            OrderType = "BillPayment",
            AmountIn = 80m,
            CurrencyIn = "USD",
            Status = "Draft"
        });
        context.Invoices.Add(new Aonik.Finance.Entities.Billing.Invoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            OrderId = orderId,
            CustomerAccountId = Guid.NewGuid(),
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(5),
            Currency = "USD",
            Subtotal = 80m,
            TaxTotal = 0m,
            DiscountTotal = 0m,
            Total = 80m,
            Status = "Issued"
        });
        context.PaymentIntents.Add(new Aonik.Finance.Entities.Payments.PaymentIntent
        {
            Id = paymentIntentId,
            TenantId = tenantId,
            Amount = 80m,
            Currency = "USD",
            PayerPartyId = partyId,
            OrderId = orderId,
            InvoiceId = invoiceId,
            PurposeType = "BillPayment",
            PurposeId = billId,
            PaymentMethodType = "BankTransfer",
            Status = "Pending"
        });
        await context.SaveChangesAsync();

        var cacheStore = new TestCacheStore();
        var service = CreateGraphService(context, tenantId, userId, cacheStore);

        // Act
        var graph = await service.GetGraphAsync();

        // Assert
        graph.UserId.Should().Be(userId);
        graph.Summary.AccountsCount.Should().Be(1);
        graph.Summary.LinkedAccountsCount.Should().Be(1);
        graph.Summary.RelatedPartiesCount.Should().Be(1);
        graph.Summary.FundingRelationshipCount.Should().Be(2);
        graph.Summary.InferredAnnotationCount.Should().Be(0);
        graph.Nodes.Should().Contain(node => node.NodeType == "PersonalAccount" && node.DisplayName == "Main Account");
        graph.Nodes.Should().Contain(node => node.NodeType == "Party" && node.DisplayName == "Mum (Mother)");
        graph.Edges.Should().Contain(edge => edge.Predicate == "OWNS_ACCOUNT");
        graph.Edges.Should().Contain(edge => edge.Predicate == "RELATED_TO_PARTY");
        graph.Edges.Should().Contain(edge => edge.Predicate == "USES_ACCOUNT" && edge.FromNodeId == $"personal-transaction:{transactionId:D}" && edge.ToNodeId == $"personal-account:{accountId:D}");
        graph.Edges.Should().Contain(edge => edge.Predicate == "USES_LINKED_ACCOUNT" && edge.FromNodeId == $"personal-account:{accountId:D}" && edge.ToNodeId == $"linked-account:{linkedAccountId:D}");
        graph.Edges.Should().Contain(edge => edge.Predicate == "FUNDED_BY_ACCOUNT" && edge.FromNodeId == $"bill:{billId:D}" && edge.ToNodeId == $"personal-account:{accountId:D}");
        graph.Edges.Should().Contain(edge => edge.Predicate == "FUNDED_BY_ACCOUNT" && edge.FromNodeId == $"goal:{goalId:D}" && edge.ToNodeId == $"personal-account:{accountId:D}");
        graph.Nodes.Should().Contain(node => node.NodeType == FinancialLifeGraphNodeTypes.OrderRef && node.SourceId == orderId);
        graph.Nodes.Should().Contain(node => node.NodeType == FinancialLifeGraphNodeTypes.InvoiceRef && node.SourceId == invoiceId);
        graph.Nodes.Should().Contain(node => node.NodeType == FinancialLifeGraphNodeTypes.PaymentIntentRef && node.SourceId == paymentIntentId);
        graph.Edges.Should().Contain(edge => edge.Predicate == FinancialLifeGraphPredicates.LinkedToOrder && edge.FromNodeId == $"bill:{billId:D}" && edge.ToNodeId == $"order-ref:{orderId:D}");
        graph.Edges.Should().Contain(edge => edge.Predicate == FinancialLifeGraphPredicates.LinkedToInvoice && edge.FromNodeId == $"bill:{billId:D}" && edge.ToNodeId == $"invoice-ref:{invoiceId:D}");
        graph.Edges.Should().Contain(edge => edge.Predicate == FinancialLifeGraphPredicates.LinkedToPaymentIntent && edge.FromNodeId == $"bill:{billId:D}" && edge.ToNodeId == $"payment-intent-ref:{paymentIntentId:D}");
    }

    [Fact]
    public async Task GetGraphAsync_Should_LimitTransactionsToRecentWindow()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await using var context = CreateDbContext(tenantId);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId
        });
        context.PersonalTransactions.Add(new PersonalTransaction
        {
            TenantId = tenantId,
            UserId = userId,
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow.AddDays(-(FinancialLifeGraphHydrationService.TransactionWindowDays + 5)),
            Amount = -10m,
            Currency = "USD",
            Merchant = "Old Merchant",
            Description = "Old transaction",
            TagsJson = "[]",
            ReviewStatus = "Pending"
        });
        context.PersonalTransactions.Add(new PersonalTransaction
        {
            TenantId = tenantId,
            UserId = userId,
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow.AddDays(-5),
            Amount = -12m,
            Currency = "USD",
            Merchant = "Recent Merchant",
            Description = "Recent transaction",
            TagsJson = "[]",
            ReviewStatus = "Pending"
        });
        await context.SaveChangesAsync();

        var service = CreateGraphService(context, tenantId, userId, new TestCacheStore());

        // Act
        var graph = await service.GetGraphAsync();

        // Assert
        graph.Summary.TransactionsCount.Should().Be(1);
        graph.Nodes.Should().Contain(node => node.NodeType == "PersonalTransaction" && node.DisplayName == "Recent Merchant");
        graph.Nodes.Should().NotContain(node => node.NodeType == "PersonalTransaction" && node.DisplayName == "Old Merchant");
    }

    [Fact]
    public async Task GetGraphAsync_Should_Only_ProjectRelevantFxQuotes_ForUserAccountCurrencies()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await using var context = CreateDbContext(tenantId);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId
        });
        context.PersonalAccounts.AddRange(
            new PersonalAccount
            {
                TenantId = tenantId,
                UserId = userId,
                Name = "USD Account",
                AccountType = "Bank",
                Currency = "USD",
                Status = "Active"
            },
            new PersonalAccount
            {
                TenantId = tenantId,
                UserId = userId,
                Name = "NGN Account",
                AccountType = "Bank",
                Currency = "NGN",
                Status = "Active"
            });
        context.FxQuotes.AddRange(
            new Aonik.Finance.Entities.Pricing.FxQuote
            {
                TenantId = tenantId,
                BaseCurrency = "USD",
                TargetCurrency = "NGN",
                Rate = 1500m,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            },
            new Aonik.Finance.Entities.Pricing.FxQuote
            {
                TenantId = tenantId,
                BaseCurrency = "NGN",
                TargetCurrency = "USD",
                Rate = 0.0007m,
                ExpiresAt = DateTime.UtcNow.AddHours(2)
            },
            new Aonik.Finance.Entities.Pricing.FxQuote
            {
                TenantId = tenantId,
                BaseCurrency = "EUR",
                TargetCurrency = "USD",
                Rate = 1.08m,
                ExpiresAt = DateTime.UtcNow.AddHours(3)
            },
            new Aonik.Finance.Entities.Pricing.FxQuote
            {
                TenantId = tenantId,
                BaseCurrency = "GBP",
                TargetCurrency = "EUR",
                Rate = 1.17m,
                ExpiresAt = DateTime.UtcNow.AddHours(4)
            });
        await context.SaveChangesAsync();

        var service = CreateGraphService(context, tenantId, userId, new TestCacheStore());

        // Act
        var graph = await service.GetGraphAsync();

        // Assert
        graph.Nodes.Count(item => item.NodeType == "FxQuote").Should().Be(2);
        graph.Nodes.Should().Contain(item => item.NodeType == "FxQuote" && item.DisplayName == "USD/NGN");
        graph.Nodes.Should().Contain(item => item.NodeType == "FxQuote" && item.DisplayName == "NGN/USD");
        graph.Nodes.Should().NotContain(item => item.NodeType == "FxQuote" && item.DisplayName == "EUR/USD");
        graph.Nodes.Should().NotContain(item => item.NodeType == "FxQuote" && item.DisplayName == "GBP/EUR");
        graph.Edges.Count(item => item.Predicate == "HAS_FX_CONTEXT").Should().Be(2);
    }

    [Fact]
    public async Task GetUpcomingObligationsAsync_Should_ReturnItemsOrderedByDueDate()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var context = CreateDbContext(tenantId);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = Guid.NewGuid()
        });
        context.Bills.Add(new Bill
        {
            TenantId = tenantId,
            UserId = userId,
            Payee = "Internet",
            Frequency = "Monthly",
            NextDueDate = DateTime.UtcNow.AddDays(7),
            ExpectedAmount = 50m,
            Currency = "USD",
            Status = "Active"
        });
        context.Subscriptions.Add(new Subscription
        {
            TenantId = tenantId,
            UserId = userId,
            Merchant = "Music",
            RenewalDate = DateTime.UtcNow.AddDays(2),
            ExpectedAmount = 11m,
            Currency = "USD",
            Status = "Active",
            DetectedBy = "manual"
        });
        await context.SaveChangesAsync();

        var cacheStore = new TestCacheStore();
        var service = CreateGraphService(context, tenantId, userId, cacheStore);

        // Act
        var obligations = await service.GetUpcomingObligationsAsync(14);

        // Assert
        obligations.Should().HaveCount(2);
        obligations[0].ItemType.Should().Be("Subscription");
        obligations[1].ItemType.Should().Be("Bill");
    }

    [Fact]
    public async Task GetGraphAsync_Should_IncludeNativeNodesAndEdges()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await using var context = CreateDbContext(tenantId);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId
        });

        var nativeNode = new FinancialLifeGraphNode
        {
            TenantId = tenantId,
            UserId = userId,
            NodeType = "NativeAnnotation",
            DisplayName = "Supports Mum",
            PropertiesJson = "{\"tag\":\"family\"}",
            Status = FinancialLifeGraphEntityStatus.Active
        };

        context.FinancialLifeGraphNodes.Add(nativeNode);
        await context.SaveChangesAsync();

        context.FinancialLifeGraphEdges.Add(new FinancialLifeGraphEdge
        {
            TenantId = tenantId,
            UserId = userId,
            FromNodeKey = $"native-node:{nativeNode.Id:D}",
            Predicate = "ANNOTATED_AS",
            ToNodeKey = $"native-node:{nativeNode.Id:D}",
            PropertiesJson = "{}",
            Status = FinancialLifeGraphEntityStatus.Active
        });
        await context.SaveChangesAsync();

        var cacheStore = new TestCacheStore();
        var service = CreateGraphService(context, tenantId, userId, cacheStore);

        // Act
        var graph = await service.GetGraphAsync();

        // Assert
        graph.Nodes.Should().Contain(item => item.NodeType == "NativeAnnotation" && item.DisplayName == "Supports Mum");
        graph.Edges.Should().ContainSingle(item => item.Predicate == "ANNOTATED_AS");
        graph.Edges.Single(item => item.Predicate == "ANNOTATED_AS").MetadataJson.Should().BeNull();
    }

    [Fact]
    public async Task WriteService_Should_CreateNativeNodeAndEdge()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await using var context = CreateDbContext(tenantId);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId
        });
        await context.SaveChangesAsync();

        await using var agentsContext = CreateAgentsDbContext(tenantId);
        var cacheStore = new TestCacheStore();
        var invalidationPublisher = new TestCacheInvalidationPublisher();
        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var graphService = CreateGraphService(context, tenantId, userId, cacheStore);
        var validationService = new FinancialLifeGraphValidationService(context, tenantProvider, currentUserProvider, new FinancialLifeGraphSchema());
        var writeService = new FinancialLifeGraphWriteService(
            context,
            tenantProvider,
            currentUserProvider,
            validationService,
            new FinancialLifeGraphCacheInvalidator(tenantProvider, currentUserProvider, invalidationPublisher));

        // Act
        var nodeResult = await writeService.CreateNodeAsync(new Aonik.Finance.Contracts.Models.PersonalFinance.CreateFinancialLifeGraphNodeRequest(
            "NativeAnnotation",
            "Regular support",
            "{\"category\":\"family\"}",
            null,
            null,
            null,
            FinancialLifeGraphEntityStatus.Active,
            false,
            null));

        var edgeResult = await writeService.CreateEdgeAsync(new Aonik.Finance.Contracts.Models.PersonalFinance.CreateFinancialLifeGraphEdgeRequest(
            $"user:{userId:D}",
            "ANNOTATED_AS",
            nodeResult.NodeKey,
            "{}",
            null,
            FinancialLifeGraphEntityStatus.Active,
            false,
            null));

        // Assert
        nodeResult.GraphNodeId.Should().NotBeEmpty();
        edgeResult.GraphEdgeId.Should().NotBeEmpty();
        context.FinancialLifeGraphNodes.Should().HaveCount(1);
        context.FinancialLifeGraphEdges.Should().HaveCount(1);
    }

    [Fact]
    public async Task WriteService_Should_InvalidateCachedGraphAfterNodeCreate()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await using var context = CreateDbContext(tenantId);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId
        });
        await context.SaveChangesAsync();

        var cacheStore = new TestCacheStore();
        var invalidationPublisher = new TestCacheInvalidationPublisher();
        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        invalidationPublisher.Invalidated += (evt, ct) =>
        {
            if (evt.CacheKey != null)
            {
                cacheStore.Remove(evt.CacheKey);
            }

            return Task.CompletedTask;
        };

        var graphService = CreateGraphService(context, tenantId, userId, cacheStore);
        var validationService = new FinancialLifeGraphValidationService(context, tenantProvider, currentUserProvider, new FinancialLifeGraphSchema());
        var writeService = new FinancialLifeGraphWriteService(
            context,
            tenantProvider,
            currentUserProvider,
            validationService,
            new FinancialLifeGraphCacheInvalidator(tenantProvider, currentUserProvider, invalidationPublisher));

        var initialGraph = await graphService.GetGraphAsync();
        initialGraph.Nodes.Should().NotContain(item => item.DisplayName == "Fresh annotation");

        // Act
        await writeService.CreateNodeAsync(new Aonik.Finance.Contracts.Models.PersonalFinance.CreateFinancialLifeGraphNodeRequest(
            "NativeAnnotation",
            "Fresh annotation",
            "{}",
            null,
            null,
            null,
            FinancialLifeGraphEntityStatus.Active,
            false,
            null));

        var refreshedGraph = await graphService.GetGraphAsync();

        // Assert
        refreshedGraph.Nodes.Should().Contain(item => item.DisplayName == "Fresh annotation");
    }

    [Fact]
    public async Task InferenceService_Should_CreateProposedRecurringMerchantAnnotations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var aiRunId = Guid.NewGuid();

        await using var context = CreateDbContext(tenantId);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId
        });

        for (var index = 0; index < 3; index++)
        {
            context.PersonalTransactions.Add(new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-(index + 1)),
                Amount = -25m,
                Currency = "USD",
                Merchant = "Family Transfer",
                Description = "Support payment",
                TagsJson = "[]",
                ReviewStatus = "Pending"
            });
        }

        await context.SaveChangesAsync();

        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        await using var agentsContext = CreateAgentsDbContext(tenantId);
        var cacheStore = new TestCacheStore();
        var invalidationPublisher = new TestCacheInvalidationPublisher();
        var service = new FinancialLifeGraphInferenceService(
            CreatePersonalFinanceDbContext(_lastDbName, tenantId),
            new AgentProposalStore(agentsContext, currentUserProvider),
            tenantProvider,
            currentUserProvider,
            new FinancialLifeGraphCacheInvalidator(tenantProvider, currentUserProvider, invalidationPublisher));

        // Act
        var proposals = await service.ProposeRecurringMerchantAnnotationsAsync(
            new Aonik.Finance.Contracts.Models.PersonalFinance.ProposeRecurringMerchantGraphAnnotationsRequest(aiRunId, 3, 30));

        // Assert
        proposals.Should().ContainSingle();
        proposals[0].Status.Should().Be(FinancialLifeGraphProposalStatus.Proposed);
        proposals[0].ProposalId.Should().NotBeEmpty();

        var node = await context.FinancialLifeGraphNodes.SingleAsync();
        var edge = await context.FinancialLifeGraphEdges.SingleAsync();
        var proposal = await agentsContext.Proposals.SingleAsync();
        node.Status.Should().Be(FinancialLifeGraphEntityStatus.Proposed);
        node.AiRunId.Should().Be(aiRunId);
        edge.Status.Should().Be(FinancialLifeGraphEntityStatus.Proposed);
        edge.ToNodeKey.Should().Be($"native-node:{node.Id:D}");
        proposal.Status.Should().Be(Aonik.Agents.Entities.ProposalStatus.Proposed);

        using var payload = JsonDocument.Parse(proposal.PayloadJson);
        payload.RootElement.GetProperty("GraphNodeId").GetGuid().Should().Be(node.Id);
        payload.RootElement.GetProperty("GraphEdgeId").GetGuid().Should().Be(edge.Id);
    }

    [Fact]
    public async Task WriteService_Should_RejectDuplicateNativeNodeDisplayName()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await using var context = CreateDbContext(tenantId);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId
        });
        context.FinancialLifeGraphNodes.Add(new FinancialLifeGraphNode
        {
            TenantId = tenantId,
            UserId = userId,
            NodeType = "NativeAnnotation",
            DisplayName = "Support note",
            PropertiesJson = "{}",
            Status = FinancialLifeGraphEntityStatus.Active
        });
        await context.SaveChangesAsync();

        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var graphService = CreateGraphService(context, tenantId, userId, new TestCacheStore());
        var validationService = new FinancialLifeGraphValidationService(context, tenantProvider, currentUserProvider, new FinancialLifeGraphSchema());
        var writeService = new FinancialLifeGraphWriteService(context, tenantProvider, currentUserProvider, validationService, new NoOpGraphCacheInvalidator());

        // Act
        Func<Task> action = () => writeService.CreateNodeAsync(new Aonik.Finance.Contracts.Models.PersonalFinance.CreateFinancialLifeGraphNodeRequest(
            "NativeAnnotation",
            "Support note",
            "{}",
            null,
            null,
            null,
            FinancialLifeGraphEntityStatus.Active,
            false,
            null));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*same type and display name already exists*");
    }

    [Fact]
    public async Task InferenceService_Should_ApproveProposalAndActivateGraphNode()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var aiRunId = Guid.NewGuid();

        await using var context = CreateDbContext(tenantId);
        await using var agentsContext = CreateAgentsDbContext(tenantId);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId
        });
        context.PersonalTransactions.AddRange(
            Enumerable.Range(1, 3).Select(index => new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-index),
                Amount = -30m,
                Currency = "USD",
                Merchant = "Family Transfer",
                Description = "Support",
                TagsJson = "[]",
                ReviewStatus = "Pending"
            }));
        await context.SaveChangesAsync();

        var cacheStore = new TestCacheStore();
        var invalidationPublisher = new TestCacheInvalidationPublisher();
        invalidationPublisher.Invalidated += (evt, ct) =>
        {
            if (evt.CacheKey != null)
            {
                cacheStore.Remove(evt.CacheKey);
            }

            return Task.CompletedTask;
        };

        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var graphService = CreateGraphService(context, tenantId, userId, cacheStore);
        var inferenceService = new FinancialLifeGraphInferenceService(
            CreatePersonalFinanceDbContext(_lastDbName, tenantId),
            new AgentProposalStore(agentsContext, currentUserProvider),
            tenantProvider,
            currentUserProvider,
            new FinancialLifeGraphCacheInvalidator(tenantProvider, currentUserProvider, invalidationPublisher));

        var proposals = await inferenceService.ProposeRecurringMerchantAnnotationsAsync(
            new Aonik.Finance.Contracts.Models.PersonalFinance.ProposeRecurringMerchantGraphAnnotationsRequest(aiRunId, 3, 30));

        var graphBeforeApproval = await graphService.GetGraphAsync();

        // Act
        await inferenceService.ApproveProposalAsync(proposals[0].ProposalId);
        var graphAfterApproval = await graphService.GetGraphAsync();

        // Assert
        graphBeforeApproval.Nodes.Should().NotContain(item => item.DisplayName == proposals[0].DisplayName);
        graphAfterApproval.Nodes.Should().Contain(item => item.DisplayName == proposals[0].DisplayName);
        graphAfterApproval.Summary.InferredAnnotationCount.Should().BeGreaterThan(0);

        var approvedProposal = await agentsContext.Proposals.SingleAsync();
        approvedProposal.Status.Should().Be(Aonik.Agents.Entities.ProposalStatus.Approved);
        approvedProposal.ApprovedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task WriteService_Should_RejectDuplicateEdgeShape()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await using var context = CreateDbContext(tenantId);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId
        });

        var node = new FinancialLifeGraphNode
        {
            TenantId = tenantId,
            UserId = userId,
            NodeType = "NativeAnnotation",
            DisplayName = "Support note",
            PropertiesJson = "{}",
            Status = FinancialLifeGraphEntityStatus.Active
        };
        context.FinancialLifeGraphNodes.Add(node);
        await context.SaveChangesAsync();

        context.FinancialLifeGraphEdges.Add(new FinancialLifeGraphEdge
        {
            TenantId = tenantId,
            UserId = userId,
            FromNodeKey = $"user:{userId:D}",
            Predicate = "ANNOTATED_AS",
            ToNodeKey = $"native-node:{node.Id:D}",
            PropertiesJson = "{}",
            Status = FinancialLifeGraphEntityStatus.Active
        });
        await context.SaveChangesAsync();

        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var graphService = CreateGraphService(context, tenantId, userId, new TestCacheStore());
        var validationService = new FinancialLifeGraphValidationService(context, tenantProvider, currentUserProvider, new FinancialLifeGraphSchema());
        var writeService = new FinancialLifeGraphWriteService(context, tenantProvider, currentUserProvider, validationService, new NoOpGraphCacheInvalidator());

        // Act
        Func<Task> action = () => writeService.CreateEdgeAsync(new Aonik.Finance.Contracts.Models.PersonalFinance.CreateFinancialLifeGraphEdgeRequest(
            $"user:{userId:D}",
            "ANNOTATED_AS",
            $"native-node:{node.Id:D}",
            "{}",
            null,
            FinancialLifeGraphEntityStatus.Active,
            false,
            null));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*same shape already exists*");
    }

    [Fact]
    public async Task CreateEdgeAsync_Should_ValidateAgainstDatabase_Not_StaleCachedGraph()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await using var context = CreateDbContext(tenantId);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId
        });

        var node = new FinancialLifeGraphNode
        {
            TenantId = tenantId,
            UserId = userId,
            NodeType = "NativeAnnotation",
            DisplayName = "Temporary annotation",
            PropertiesJson = "{}",
            Status = FinancialLifeGraphEntityStatus.Active
        };
        context.FinancialLifeGraphNodes.Add(node);
        await context.SaveChangesAsync();

        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var cacheStore = new TestCacheStore();
        var graphService = CreateGraphService(context, tenantId, userId, cacheStore);

        // Prime the cache with the active node.
        var cachedGraph = await graphService.GetGraphAsync();
        cachedGraph.Nodes.Should().Contain(item => item.NodeId == $"native-node:{node.Id:D}");

        // Change database state after the snapshot has been cached.
        node.Status = FinancialLifeGraphEntityStatus.Rejected;
        await context.SaveChangesAsync();

        var validationService = new FinancialLifeGraphValidationService(context, tenantProvider, currentUserProvider, new FinancialLifeGraphSchema());
        var writeService = new FinancialLifeGraphWriteService(context, tenantProvider, currentUserProvider, validationService, new NoOpGraphCacheInvalidator());

        // Act
        Func<Task> action = () => writeService.CreateEdgeAsync(new Aonik.Finance.Contracts.Models.PersonalFinance.CreateFinancialLifeGraphEdgeRequest(
            $"user:{userId:D}",
            "ANNOTATED_AS",
            $"native-node:{node.Id:D}",
            "{}",
            null,
            FinancialLifeGraphEntityStatus.Active,
            false,
            null));

        // Assert
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ToNodeKey does not exist in the current graph*");
    }

    [Fact]
    public async Task GetContextMethods_Should_ReturnHouseholdAndRelatedPartyContext()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var relatedPartyId = Guid.NewGuid();
        var householdId = Guid.NewGuid();

        await using var context = CreateDbContext(tenantId);
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId,
            HouseholdId = householdId
        });
        context.Households.Add(new Household
        {
            Id = householdId,
            TenantId = tenantId,
            Name = "Home"
        });
        context.HouseholdMembers.Add(new HouseholdMember
        {
            TenantId = tenantId,
            HouseholdId = householdId,
            UserId = userId,
            Role = "Owner",
            PermissionsJson = "[]"
        });
        context.Parties.Add(new PartyReadModel
        {
            Id = relatedPartyId,
            TenantId = tenantId,
            DisplayName = "Sibling",
            Status = "Active"
        });
        context.PartyRelationships.Add(new PartyRelationshipReadModel
        {
            TenantId = tenantId,
            FromPartyId = partyId,
            ToPartyId = relatedPartyId,
            RelationshipTypeCode = "Sibling",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = CreateGraphService(context, tenantId, userId, new TestCacheStore());

        // Act
        var householdContext = await service.GetHouseholdFinanceContextAsync();
        var relatedPartyContext = await service.GetRelatedPartyFinanceContextAsync();

        // Assert
        householdContext.HasHousehold.Should().BeTrue();
        householdContext.MemberCount.Should().Be(1);
        relatedPartyContext.Parties.Should().ContainSingle();
        relatedPartyContext.Parties[0].DisplayName.Should().Be("Sibling");
    }

    [Fact]
    public async Task GetGraphAsync_Should_UseProfileDisplayNames_ForNonCurrentHouseholdMembers()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var otherPartyId = Guid.NewGuid();
        var householdId = Guid.NewGuid();

        await using var context = CreateDbContext(tenantId);
        context.PersonalProfiles.AddRange(
            new PersonalProfile
            {
                TenantId = tenantId,
                UserId = userId,
                PartyId = partyId,
                HouseholdId = householdId
            },
            new PersonalProfile
            {
                TenantId = tenantId,
                UserId = otherUserId,
                PartyId = otherPartyId,
                HouseholdId = householdId
            });
        context.Households.Add(new Household
        {
            Id = householdId,
            TenantId = tenantId,
            Name = "Home"
        });
        context.HouseholdMembers.AddRange(
            new HouseholdMember
            {
                TenantId = tenantId,
                HouseholdId = householdId,
                UserId = userId,
                Role = "Owner",
                PermissionsJson = "[]"
            },
            new HouseholdMember
            {
                TenantId = tenantId,
                HouseholdId = householdId,
                UserId = otherUserId,
                Role = "Member",
                PermissionsJson = "[]"
            });
        context.Parties.Add(new PartyReadModel
        {
            Id = otherPartyId,
            TenantId = tenantId,
            DisplayName = "Brother Joe",
            Status = "Active"
        });
        await context.SaveChangesAsync();

        var graphService = CreateGraphService(context, tenantId, userId, new TestCacheStore());

        // Act
        var graph = await graphService.GetGraphAsync();

        // Assert
        graph.Nodes.Should().Contain(item => item.NodeType == FinancialLifeGraphNodeTypes.HouseholdMember && item.DisplayName == "Brother Joe");
    }
}
