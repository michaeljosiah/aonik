using Aonik.Finance.Entities;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Aonik.Application.Tests.PersonalFinance;

public class FinancialLifeGraphServiceTests
{
    private sealed class NoOpGraphCacheInvalidator : IFinancialLifeGraphCacheInvalidator
    {
        public void InvalidateCurrentUserGraph()
        {
        }
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

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"FinancialLifeGraph_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
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
        context.FinancialLinkedAccounts.Add(new FinancialLinkedAccount
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
        await context.SaveChangesAsync();

        var service = new FinancialLifeGraphService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new MemoryCache(new MemoryCacheOptions()));

        // Act
        var graph = await service.GetGraphAsync();

        // Assert
        graph.UserId.Should().Be(userId);
        graph.Summary.AccountsCount.Should().Be(1);
        graph.Summary.LinkedAccountsCount.Should().Be(1);
        graph.Summary.RelatedPartiesCount.Should().Be(1);
        graph.Nodes.Should().Contain(node => node.NodeType == "PersonalAccount" && node.DisplayName == "Main Account");
        graph.Nodes.Should().Contain(node => node.NodeType == "Party" && node.DisplayName == "Mum");
        graph.Edges.Should().Contain(edge => edge.Predicate == "OWNS_ACCOUNT");
        graph.Edges.Should().Contain(edge => edge.Predicate == "RELATED_TO_PARTY");
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

        var service = new FinancialLifeGraphService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new MemoryCache(new MemoryCacheOptions()));

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
            Status = "Active"
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
            Status = "Active"
        });
        await context.SaveChangesAsync();

        var service = new FinancialLifeGraphService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new MemoryCache(new MemoryCacheOptions()));

        // Act
        var graph = await service.GetGraphAsync();

        // Assert
        graph.Nodes.Should().Contain(item => item.NodeType == "NativeAnnotation" && item.DisplayName == "Supports Mum");
        graph.Edges.Should().Contain(item => item.Predicate == "ANNOTATED_AS");
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

        var cache = new MemoryCache(new MemoryCacheOptions());
        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var graphService = new FinancialLifeGraphService(context, tenantProvider, currentUserProvider, cache);
        var validationService = new FinancialLifeGraphValidationService(context, tenantProvider, currentUserProvider);
        var writeService = new FinancialLifeGraphWriteService(
            context,
            tenantProvider,
            currentUserProvider,
            validationService,
            graphService,
            new FinancialLifeGraphCacheInvalidator(tenantProvider, currentUserProvider, cache));

        // Act
        var nodeResult = await writeService.CreateNodeAsync(new Aonik.Finance.Contracts.Models.PersonalFinance.CreateFinancialLifeGraphNodeRequest(
            "NativeAnnotation",
            "Regular support",
            "{\"category\":\"family\"}",
            null,
            null,
            null,
            "Active",
            false,
            null));

        var edgeResult = await writeService.CreateEdgeAsync(new Aonik.Finance.Contracts.Models.PersonalFinance.CreateFinancialLifeGraphEdgeRequest(
            nodeResult.NodeKey,
            "ANNOTATED_AS",
            nodeResult.NodeKey,
            "{}",
            null,
            "Active",
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

        var cache = new MemoryCache(new MemoryCacheOptions());
        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var graphService = new FinancialLifeGraphService(context, tenantProvider, currentUserProvider, cache);
        var validationService = new FinancialLifeGraphValidationService(context, tenantProvider, currentUserProvider);
        var writeService = new FinancialLifeGraphWriteService(
            context,
            tenantProvider,
            currentUserProvider,
            validationService,
            graphService,
            new FinancialLifeGraphCacheInvalidator(tenantProvider, currentUserProvider, cache));

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
            "Active",
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
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new FinancialLifeGraphInferenceService(
            context,
            tenantProvider,
            currentUserProvider,
            new FinancialLifeGraphCacheInvalidator(tenantProvider, currentUserProvider, cache));

        // Act
        var proposals = await service.ProposeRecurringMerchantAnnotationsAsync(
            new Aonik.Finance.Contracts.Models.PersonalFinance.ProposeRecurringMerchantGraphAnnotationsRequest(aiRunId, 3, 30));

        // Assert
        proposals.Should().ContainSingle();
        proposals[0].Status.Should().Be("Proposed");

        var node = await context.FinancialLifeGraphNodes.SingleAsync();
        var edge = await context.FinancialLifeGraphEdges.SingleAsync();
        node.Status.Should().Be("Proposed");
        node.AiRunId.Should().Be(aiRunId);
        edge.Status.Should().Be("Proposed");
    }
}
