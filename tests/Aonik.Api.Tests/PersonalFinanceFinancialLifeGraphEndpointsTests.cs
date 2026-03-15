using System.Net;
using System.Net.Http.Json;
using Aonik.Finance.Entities;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public class PersonalFinanceFinancialLifeGraphEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PersonalFinanceFinancialLifeGraphEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetGraphSummary_Should_ReturnExpectedCounts()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await SeedGraphDataAsync(tenantId, userId, partyId);

        var client = await _factory.CreateAuthenticatedClientAsync(
            new TestAuthOptions
            {
                UserId = userId,
                TenantId = tenantId
            }.WithRoles("PersonalUser"));

        // Act
        var response = await client.GetAsync("/personal-finance/graph/summary");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GraphSummaryPayload>();
        body.Should().NotBeNull();
        body!.AccountsCount.Should().Be(1);
        body.RelatedPartiesCount.Should().Be(1);
        body.BillsCount.Should().Be(1);
    }

    [Fact]
    public async Task GetUpcomingObligations_Should_ReturnGraphObligations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await SeedGraphDataAsync(tenantId, userId, partyId);

        var client = await _factory.CreateAuthenticatedClientAsync(
            new TestAuthOptions
            {
                UserId = userId,
                TenantId = tenantId
            }.WithRoles("PersonalUser"));

        // Act
        var response = await client.GetAsync("/personal-finance/graph/upcoming-obligations?withinDays=14");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<UpcomingObligationPayload>>();
        body.Should().NotBeNull();
        body!.Should().Contain(item => item.ItemType == "Bill");
        body.Should().Contain(item => item.ItemType == "Subscription");
    }

    [Fact]
    public async Task CreateNode_Should_PersistAndAppearInGraph()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await SeedGraphDataAsync(tenantId, userId, partyId);

        var client = await _factory.CreateAuthenticatedClientAsync(
            new TestAuthOptions
            {
                UserId = userId,
                TenantId = tenantId
            }.WithRoles("PersonalUser"));

        // Act
        var createResponse = await client.PostAsJsonAsync("/personal-finance/graph/nodes", new CreateFinancialLifeGraphNodeRequest(
            "NativeAnnotation",
            "Helps family budget",
            "{\"topic\":\"family\"}",
            null,
            null,
            null,
            FinancialLifeGraphEntityStatus.Active,
            false,
            null));

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<FinancialLifeGraphNodeWriteResponse>();
        created.Should().NotBeNull();

        var graphResponse = await client.GetAsync("/personal-finance/graph");
        graphResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var graph = await graphResponse.Content.ReadFromJsonAsync<GraphPayload>();
        graph.Should().NotBeNull();
        graph!.Nodes.Should().Contain(item => item.NodeId == created!.NodeKey && item.DisplayName == "Helps family budget");
    }

    [Fact]
    public async Task DeleteNode_Should_RemovePersistedNativeNode()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        await SeedGraphDataAsync(tenantId, userId, partyId);

        Guid graphNodeId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var financeDbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.TenantId = tenantId;

            var node = new FinancialLifeGraphNode
            {
                TenantId = tenantId,
                UserId = userId,
                NodeType = "NativeAnnotation",
                DisplayName = "Temporary annotation",
                PropertiesJson = "{}",
                Status = FinancialLifeGraphEntityStatus.Active
            };

            financeDbContext.FinancialLifeGraphNodes.Add(node);
            await financeDbContext.SaveChangesAsync();
            graphNodeId = node.Id;
        }

        var client = await _factory.CreateAuthenticatedClientAsync(
            new TestAuthOptions
            {
                UserId = userId,
                TenantId = tenantId
            }.WithRoles("PersonalUser"));

        // Act
        var deleteResponse = await client.DeleteAsync($"/personal-finance/graph/nodes/{graphNodeId}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var deletedNode = await verifyContext.FinancialLifeGraphNodes
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == graphNodeId);
        deletedNode.Should().NotBeNull();
        deletedNode!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task ProposalEndpoints_Should_CreateAndApproveRecurringMerchantProposal()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        await SeedGraphDataAsync(tenantId, userId, partyId);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var financeDbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.TenantId = tenantId;

            financeDbContext.PersonalTransactions.AddRange(
                Enumerable.Range(1, 3).Select(index => new PersonalTransaction
                {
                    TenantId = tenantId,
                    UserId = userId,
                    SourceType = "manual",
                    SourceId = Guid.NewGuid(),
                    OccurredAt = DateTime.UtcNow.AddDays(-index),
                    Amount = -20m,
                    Currency = "USD",
                    Merchant = "Family Transfer",
                    Description = "Support",
                    TagsJson = "[]",
                    ReviewStatus = "Pending"
                }));

            await financeDbContext.SaveChangesAsync();
        }

        var client = await _factory.CreateAuthenticatedClientAsync(
            new TestAuthOptions
            {
                UserId = userId,
                TenantId = tenantId
            }.WithRoles("PersonalUser"));

        // Act
        var proposalResponse = await client.PostAsJsonAsync(
            "/personal-finance/graph/proposals/recurring-merchants",
            new ProposeRecurringMerchantGraphAnnotationsRequest(Guid.NewGuid(), 3, 30));

        // Assert proposal created
        proposalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var proposals = await proposalResponse.Content.ReadFromJsonAsync<List<InferenceProposalPayload>>();
        proposals.Should().NotBeNull();
        proposals!.Should().ContainSingle();

        var pendingResponse = await client.GetAsync("/personal-finance/graph/proposals/pending");
        pendingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var pending = await pendingResponse.Content.ReadFromJsonAsync<List<PendingProposalPayload>>();
        pending.Should().NotBeNull();
        pending!.Should().ContainSingle();

        var approveResponse = await client.PostAsync($"/personal-finance/graph/proposals/{pending[0].ProposalId}/approve", null);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var graphResponse = await client.GetAsync("/personal-finance/graph");
        graphResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var graph = await graphResponse.Content.ReadFromJsonAsync<GraphPayload>();
        graph!.Nodes.Should().Contain(item => item.DisplayName == pending[0].DisplayName);
    }

    [Fact]
    public async Task ProposalEndpoints_Should_CreateAndRejectRecurringMerchantProposal()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        await SeedGraphDataAsync(tenantId, userId, partyId);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var financeDbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.TenantId = tenantId;

            financeDbContext.PersonalTransactions.AddRange(
                Enumerable.Range(1, 3).Select(index => new PersonalTransaction
                {
                    TenantId = tenantId,
                    UserId = userId,
                    SourceType = "manual",
                    SourceId = Guid.NewGuid(),
                    OccurredAt = DateTime.UtcNow.AddDays(-index),
                    Amount = -20m,
                    Currency = "USD",
                    Merchant = "Reject Merchant",
                    Description = "Support",
                    TagsJson = "[]",
                    ReviewStatus = "Pending"
                }));

            await financeDbContext.SaveChangesAsync();
        }

        var client = await _factory.CreateAuthenticatedClientAsync(
            new TestAuthOptions
            {
                UserId = userId,
                TenantId = tenantId
            }.WithRoles("PersonalUser"));

        var proposalResponse = await client.PostAsJsonAsync(
            "/personal-finance/graph/proposals/recurring-merchants",
            new ProposeRecurringMerchantGraphAnnotationsRequest(Guid.NewGuid(), 3, 30));

        proposalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var proposals = await proposalResponse.Content.ReadFromJsonAsync<List<InferenceProposalPayload>>();
        proposals.Should().NotBeNull();
        proposals!.Should().ContainSingle();

        // Act
        var rejectResponse = await client.PostAsJsonAsync(
            $"/personal-finance/graph/proposals/{proposals[0].ProposalId}/reject",
            new RejectFinancialLifeGraphProposalRequest("User declined suggestion"));

        // Assert
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var pendingResponse = await client.GetAsync("/personal-finance/graph/proposals/pending");
        var pending = await pendingResponse.Content.ReadFromJsonAsync<List<PendingProposalPayload>>();
        pending.Should().NotBeNull();
        pending!.Should().BeEmpty();

        var graphResponse = await client.GetAsync("/personal-finance/graph");
        var graph = await graphResponse.Content.ReadFromJsonAsync<GraphPayload>();
        graph!.Nodes.Should().NotContain(item => item.DisplayName == proposals[0].DisplayName);
    }

    [Fact]
    public async Task ContextEndpoints_Should_ReturnHouseholdAndRelatedPartyViews()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var householdId = Guid.NewGuid();

        await SeedGraphDataAsync(tenantId, userId, partyId, householdId);

        var client = await _factory.CreateAuthenticatedClientAsync(
            new TestAuthOptions
            {
                UserId = userId,
                TenantId = tenantId
            }.WithRoles("PersonalUser"));

        // Act
        var householdResponse = await client.GetAsync("/personal-finance/graph/household-context");
        var relatedPartyResponse = await client.GetAsync("/personal-finance/graph/related-party-context");

        // Assert
        householdResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        relatedPartyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var household = await householdResponse.Content.ReadFromJsonAsync<HouseholdContextPayload>();
        var relatedParty = await relatedPartyResponse.Content.ReadFromJsonAsync<RelatedPartyContextPayload>();

        household.Should().NotBeNull();
        household!.HasHousehold.Should().BeTrue();
        household.MemberCount.Should().BeGreaterThan(0);

        relatedParty.Should().NotBeNull();
        relatedParty!.Parties.Should().Contain(item => item.DisplayName == "Dad");
    }

    private async Task SeedGraphDataAsync(Guid tenantId, Guid userId, Guid partyId, Guid? householdId = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var financeDbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;

        financeDbContext.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId,
            HouseholdId = householdId
        });
        if (householdId.HasValue)
        {
            financeDbContext.Households.Add(new Household
            {
                Id = householdId.Value,
                TenantId = tenantId,
                Name = "Family Home"
            });
            financeDbContext.HouseholdMembers.Add(new HouseholdMember
            {
                TenantId = tenantId,
                HouseholdId = householdId.Value,
                UserId = userId,
                Role = "Owner",
                PermissionsJson = "[]"
            });
        }
        financeDbContext.PersonalAccounts.Add(new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Main Account",
            AccountType = "Bank",
            Currency = "USD",
            Status = "Active"
        });
        financeDbContext.Bills.Add(new Bill
        {
            TenantId = tenantId,
            UserId = userId,
            Payee = "Electricity",
            Frequency = "Monthly",
            NextDueDate = DateTime.UtcNow.AddDays(5),
            ExpectedAmount = 70m,
            Currency = "USD",
            Status = "Active"
        });
        financeDbContext.Subscriptions.Add(new Subscription
        {
            TenantId = tenantId,
            UserId = userId,
            Merchant = "Video",
            RenewalDate = DateTime.UtcNow.AddDays(2),
            ExpectedAmount = 15m,
            Currency = "USD",
            Status = "Active",
            DetectedBy = "manual"
        });
        financeDbContext.Parties.Add(new PartyReadModel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DisplayName = "Dad",
            Status = "Active"
        });

        var relatedParty = financeDbContext.Parties.Local.Last();

        financeDbContext.PartyRelationships.Add(new PartyRelationshipReadModel
        {
            TenantId = tenantId,
            FromPartyId = partyId,
            ToPartyId = relatedParty.Id,
            RelationshipTypeCode = "Father",
            IsActive = true
        });

        await financeDbContext.SaveChangesAsync();
    }

    private sealed record GraphSummaryPayload(
        int AccountsCount,
        int LinkedAccountsCount,
        int TransactionsCount,
        int BillsCount,
        int GoalsCount,
        int SubscriptionsCount,
        int FundingRelationshipCount,
        int InferredAnnotationCount,
        bool HasHousehold,
        int HouseholdMembersCount,
        int RelatedPartiesCount,
        Guid? PartyId,
        Guid? HouseholdId);

    private sealed record UpcomingObligationPayload(
        string ItemType,
        Guid SourceId,
        string DisplayName,
        decimal? Amount,
        string Currency,
        DateTime DueDate,
        int DaysUntilDue,
        string Status);

    private sealed record GraphNodePayload(
        string NodeId,
        string NodeType,
        string DisplayName,
        string SourceType,
        Guid? SourceId,
        string? MetadataJson);

    private sealed record GraphPayload(
        Guid TenantId,
        Guid UserId,
        Guid? HouseholdId,
        DateTime GeneratedAt,
        GraphSummaryPayload Summary,
        List<GraphNodePayload> Nodes,
        List<object> Edges,
        List<object> SourceCoverage);

    private sealed record InferenceProposalPayload(
        Guid ProposalId,
        Guid GraphNodeId,
        Guid GraphEdgeId,
        string DisplayName,
        string Reasoning,
        int OccurrenceCount,
        string Status);

    private sealed record PendingProposalPayload(
        Guid ProposalId,
        Guid GraphNodeId,
        Guid GraphEdgeId,
        string NodeType,
        string DisplayName,
        string Predicate,
        string Status,
        Guid AiRunId,
        string MetadataJson);

    private sealed record HouseholdContextPayload(
        bool HasHousehold,
        Guid? HouseholdId,
        int MemberCount,
        List<GraphNodePayload> Nodes,
        List<object> Edges);

    private sealed record RelatedPartyContextItemPayload(
        Guid PartyId,
        string DisplayName,
        string? RelationshipTypeCode,
        string? Notes);

    private sealed record RelatedPartyContextPayload(
        List<RelatedPartyContextItemPayload> Parties);
}
