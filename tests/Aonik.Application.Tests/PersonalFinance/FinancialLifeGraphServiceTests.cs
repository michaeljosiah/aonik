using Aonik.Finance.Entities;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Caching;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

public class FinancialLifeGraphServiceTests
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

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"FinancialLifeGraph_{Guid.NewGuid()}")
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

        var cacheStore = new TestCacheStore();
        var service = new FinancialLifeGraphService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            cacheStore);

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

        var cacheStore = new TestCacheStore();
        var service = new FinancialLifeGraphService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            cacheStore);

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

        var cacheStore = new TestCacheStore();
        var service = new FinancialLifeGraphService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            cacheStore);

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

        await using var agentsContext = CreateAgentsDbContext(tenantId);
        var cacheStore = new TestCacheStore();
        var invalidationPublisher = new TestCacheInvalidationPublisher();
        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var graphService = new FinancialLifeGraphService(context, tenantProvider, currentUserProvider, cacheStore);
        var validationService = new FinancialLifeGraphValidationService(context, tenantProvider, currentUserProvider);
        var writeService = new FinancialLifeGraphWriteService(
            context,
            tenantProvider,
            currentUserProvider,
            validationService,
            graphService,
            new FinancialLifeGraphCacheInvalidator(tenantProvider, currentUserProvider, invalidationPublisher));

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

        var graphService = new FinancialLifeGraphService(context, tenantProvider, currentUserProvider, cacheStore);
        var validationService = new FinancialLifeGraphValidationService(context, tenantProvider, currentUserProvider);
        var writeService = new FinancialLifeGraphWriteService(
            context,
            tenantProvider,
            currentUserProvider,
            validationService,
            graphService,
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
        await using var agentsContext = CreateAgentsDbContext(tenantId);
        var cacheStore = new TestCacheStore();
        var invalidationPublisher = new TestCacheInvalidationPublisher();
        var service = new FinancialLifeGraphInferenceService(
            context,
            agentsContext,
            tenantProvider,
            currentUserProvider,
            new FinancialLifeGraphCacheInvalidator(tenantProvider, currentUserProvider, invalidationPublisher));

        // Act
        var proposals = await service.ProposeRecurringMerchantAnnotationsAsync(
            new Aonik.Finance.Contracts.Models.PersonalFinance.ProposeRecurringMerchantGraphAnnotationsRequest(aiRunId, 3, 30));

        // Assert
        proposals.Should().ContainSingle();
        proposals[0].Status.Should().Be("Proposed");
        proposals[0].ProposalId.Should().NotBeEmpty();

        var node = await context.FinancialLifeGraphNodes.SingleAsync();
        var edge = await context.FinancialLifeGraphEdges.SingleAsync();
        var proposal = await agentsContext.Proposals.SingleAsync();
        node.Status.Should().Be("Proposed");
        node.AiRunId.Should().Be(aiRunId);
        edge.Status.Should().Be("Proposed");
        proposal.Status.Should().Be("Proposed");
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
            Status = "Active"
        });
        await context.SaveChangesAsync();

        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var graphService = new FinancialLifeGraphService(context, tenantProvider, currentUserProvider, new TestCacheStore());
        var validationService = new FinancialLifeGraphValidationService(context, tenantProvider, currentUserProvider);
        var writeService = new FinancialLifeGraphWriteService(context, tenantProvider, currentUserProvider, validationService, graphService, new NoOpGraphCacheInvalidator());

        // Act
        Func<Task> action = () => writeService.CreateNodeAsync(new Aonik.Finance.Contracts.Models.PersonalFinance.CreateFinancialLifeGraphNodeRequest(
            "NativeAnnotation",
            "Support note",
            "{}",
            null,
            null,
            null,
            "Active",
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
        var graphService = new FinancialLifeGraphService(context, tenantProvider, currentUserProvider, cacheStore);
        var inferenceService = new FinancialLifeGraphInferenceService(
            context,
            agentsContext,
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

        var approvedProposal = await agentsContext.Proposals.SingleAsync();
        approvedProposal.Status.Should().Be("Approved");
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
            Status = "Active"
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
            Status = "Active"
        });
        await context.SaveChangesAsync();

        var tenantProvider = new TestTenantProvider(tenantId);
        var currentUserProvider = new TestCurrentUserProvider(userId);
        var graphService = new FinancialLifeGraphService(context, tenantProvider, currentUserProvider, new TestCacheStore());
        var validationService = new FinancialLifeGraphValidationService(context, tenantProvider, currentUserProvider);
        var writeService = new FinancialLifeGraphWriteService(context, tenantProvider, currentUserProvider, validationService, graphService, new NoOpGraphCacheInvalidator());

        // Act
        Func<Task> action = () => writeService.CreateEdgeAsync(new Aonik.Finance.Contracts.Models.PersonalFinance.CreateFinancialLifeGraphEdgeRequest(
            $"user:{userId:D}",
            "ANNOTATED_AS",
            $"native-node:{node.Id:D}",
            "{}",
            null,
            "Active",
            false,
            null));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*same shape already exists*");
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

        var service = new FinancialLifeGraphService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new TestCacheStore());

        // Act
        var householdContext = await service.GetHouseholdFinanceContextAsync();
        var relatedPartyContext = await service.GetRelatedPartyFinanceContextAsync();

        // Assert
        householdContext.HasHousehold.Should().BeTrue();
        householdContext.MemberCount.Should().Be(1);
        relatedPartyContext.Parties.Should().ContainSingle();
        relatedPartyContext.Parties[0].DisplayName.Should().Be("Sibling");
    }
}
