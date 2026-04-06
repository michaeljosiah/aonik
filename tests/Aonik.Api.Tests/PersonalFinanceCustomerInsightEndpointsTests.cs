using System.Net;
using System.Net.Http.Json;

using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public class PersonalFinanceCustomerInsightEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PersonalFinanceCustomerInsightEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CustomerInsights_CurrentAndHistory_ShouldReturnSnapshotArtifacts()
    {
        // Arrange
        var tenantId = Guid.Parse("12121212-1212-1212-1212-121212121212");
        var options = TestAuthOptions.Create()
            .WithTenant(tenantId)
            .WithRoles("PersonalUser");

        await SeedSnapshotsAsync(tenantId, options.UserId);
        var client = await _factory.CreateAuthenticatedClientAsync(options);

        // Act
        var currentResponse = await client.GetAsync("/personal-finance/customer-insights/current");
        var current = await currentResponse.Content.ReadFromJsonAsync<CustomerInsightSnapshotResponse>();

        var historyResponse = await client.GetAsync("/personal-finance/customer-insights/history?take=10");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<CustomerInsightSnapshotHistoryItemResponse>>();

        // Assert
        currentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        current.Should().NotBeNull();
        current!.Status.Should().Be(CustomerInsightSnapshotContract.StatusCurrent);
        current.Version.Should().Be(2);
        current.Snapshot.Should().NotBeNull();
        current.Snapshot!.Metrics.CashPosition.TotalBalanceByCurrency.Should().ContainSingle(x => x.Currency == "USD" && x.Amount == 1800m);

        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        history.Should().NotBeNull();
        history!.Should().HaveCount(2);
        history[0].Version.Should().Be(2);
        history[0].Status.Should().Be(CustomerInsightSnapshotContract.StatusCurrent);
        history[1].Version.Should().Be(1);
        history[1].Status.Should().Be(CustomerInsightSnapshotContract.StatusSuperseded);
    }

    [Fact]
    public async Task CustomerInsights_ById_ShouldReturnNotFound_ForAnotherUsersSnapshot()
    {
        // Arrange
        var tenantId = Guid.Parse("34343434-3434-3434-3434-343434343434");
        var ownerOptions = TestAuthOptions.Create()
            .WithTenant(tenantId)
            .WithRoles("PersonalUser");
        var otherOptions = TestAuthOptions.Create()
            .WithTenant(tenantId)
            .WithRoles("PersonalUser");

        var snapshotId = await SeedSingleCurrentSnapshotAsync(tenantId, ownerOptions.UserId, version: 1, supersededById: null);
        var client = await _factory.CreateAuthenticatedClientAsync(otherOptions);

        // Act
        var response = await client.GetAsync($"/personal-finance/customer-insights/{snapshotId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CustomerInsights_AiSummary_ShouldReturnCurrentSummary_ForSnapshotOwner()
    {
        // Arrange
        var tenantId = Guid.Parse("45454545-4545-4545-4545-454545454545");
        var options = TestAuthOptions.Create()
            .WithTenant(tenantId)
            .WithRoles("PersonalUser");

        var snapshotId = await SeedSingleCurrentSnapshotAsync(tenantId, options.UserId, version: 1, supersededById: null);
        await SeedCurrentAiSummaryAsync(tenantId, options.UserId, snapshotId);
        var client = await _factory.CreateAuthenticatedClientAsync(options);

        // Act
        var response = await client.GetAsync($"/personal-finance/customer-insights/{snapshotId}/ai-summary");
        var summary = await response.Content.ReadFromJsonAsync<CustomerInsightAiSummaryResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        summary.Should().NotBeNull();
        summary!.CustomerInsightSnapshotId.Should().Be(snapshotId);
        summary.Status.Should().Be(CustomerInsightAiSummaryContract.StatusCurrent);
        summary.Summary.Should().NotBeNull();
        summary.Summary!.Headline.Should().Be("Stable cash position");
    }

    [Fact]
    public async Task AdminCustomerInsights_RebuildSnapshot_ShouldGenerateCurrentSnapshot()
    {
        // Arrange
        var tenantId = Guid.Parse("67676767-6767-6767-6767-676767676767");
        var userId = Guid.Parse("78787878-7878-7878-7878-787878787878");
        await SeedRebuildSnapshotScenarioAsync(tenantId, userId);

        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create()
                .WithTenant(tenantId)
                .WithRoles("TenantAdmin"));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/admin/personal-finance/customer-insights/rebuild/{userId}",
            new { UserId = userId });
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var snapshot = System.Text.Json.JsonSerializer.Deserialize<CustomerInsightSnapshotResponse>(body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        snapshot.Should().NotBeNull();
        snapshot!.UserId.Should().Be(userId);
        snapshot.Status.Should().Be(CustomerInsightSnapshotContract.StatusCurrent);
        snapshot.Snapshot.Should().NotBeNull();
        snapshot.Snapshot!.Metrics.CashPosition.TotalBalanceByCurrency.Should().ContainSingle(x => x.Currency == "USD" && x.Amount == 2200m);
    }

    [Fact]
    public async Task AdminCustomerInsights_ShouldReturnStructuredAiSummary()
    {
        // Arrange
        var tenantId = Guid.Parse("90909090-9090-9090-9090-909090909090");
        var userId = Guid.Parse("91919191-9191-9191-9191-919191919191");
        var partyId = Guid.Parse("92929292-9292-9292-9292-929292929292");
        var snapshotId = await SeedSingleCurrentSnapshotAsync(tenantId, userId, version: 1, supersededById: null);
        await SeedCurrentAiSummaryAsync(tenantId, userId, snapshotId);
        await SeedAdminCustomerLinkAsync(tenantId, userId, partyId);

        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create()
                .WithTenant(tenantId)
                .WithRoles("TenantAdmin"));

        // Act
        var response = await client.GetAsync($"/admin/customers/{partyId}/insights");
        var payload = await response.Content.ReadFromJsonAsync<CustomerInsightsResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.Should().NotBeNull();
        payload!.AiSummary.Should().NotBeNull();
        payload.AiSummary!.Headline.Should().Be("Stable cash position");
        payload.AiSummary.Summary.Should().Contain("Cashflow remains stable");
        payload.AiSummary.KeyObservations.Should().ContainSingle();
        payload.AiSummary.PositivePatterns.Should().ContainSingle();
        payload.AiSummary.RiskPatterns.Should().ContainSingle();
        payload.AiSummary.RecommendedFocusAreas.Should().ContainSingle();
        payload.Snapshot.Should().NotBeNull();
        payload.Snapshot!.IsPartial.Should().BeFalse();
    }

    private async Task SeedSnapshotsAsync(Guid tenantId, Guid userId)
    {
        var currentId = Guid.Parse("56565656-5656-5656-5656-565656565656");
        var previousId = await SeedSingleCurrentSnapshotAsync(tenantId, userId, version: 1, supersededById: currentId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "test";

        var financeDbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var current = await financeDbContext.CustomerInsightSnapshots.FindAsync(currentId);
        current.Should().BeNull();

        financeDbContext.CustomerInsightSnapshots.Add(new CustomerInsightSnapshot
        {
            Id = currentId,
            TenantId = tenantId,
            UserId = userId,
            Status = CustomerInsightSnapshotContract.StatusCurrent,
            AsOfUtc = new DateTime(2026, 3, 31, 10, 0, 0, DateTimeKind.Utc),
            WindowStartUtc = new DateTime(2025, 10, 4, 0, 0, 0, DateTimeKind.Utc),
            WindowEndUtc = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc),
            Version = 2,
            SourceHash = "hash-current",
            SnapshotJson = BuildSnapshotJson(tenantId, userId, 1800m),
            GeneratedBy = CustomerInsightSnapshotContract.GeneratorVersion,
            GenerationDurationMs = 250
        });

        await financeDbContext.SaveChangesAsync();

        var previous = await financeDbContext.CustomerInsightSnapshots.FindAsync(previousId);
        previous.Should().NotBeNull();
        previous!.Status = CustomerInsightSnapshotContract.StatusSuperseded;
        previous.SupersededById = currentId;
        await financeDbContext.SaveChangesAsync();
    }

    private async Task<Guid> SeedSingleCurrentSnapshotAsync(Guid tenantId, Guid userId, int version, Guid? supersededById)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "test";

        var financeDbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var snapshotId = Guid.NewGuid();

        financeDbContext.CustomerInsightSnapshots.Add(new CustomerInsightSnapshot
        {
            Id = snapshotId,
            TenantId = tenantId,
            UserId = userId,
            Status = CustomerInsightSnapshotContract.StatusCurrent,
            AsOfUtc = new DateTime(2026, 3, 30, 10, 0, 0, DateTimeKind.Utc),
            WindowStartUtc = new DateTime(2025, 10, 3, 0, 0, 0, DateTimeKind.Utc),
            WindowEndUtc = new DateTime(2026, 3, 30, 23, 59, 59, DateTimeKind.Utc),
            Version = version,
            SourceHash = $"hash-{version}",
            SnapshotJson = BuildSnapshotJson(tenantId, userId, 1500m + version * 100m),
            GeneratedBy = CustomerInsightSnapshotContract.GeneratorVersion,
            GenerationDurationMs = 200,
            SupersededById = supersededById
        });

        await financeDbContext.SaveChangesAsync();
        return snapshotId;
    }

    private async Task SeedCurrentAiSummaryAsync(Guid tenantId, Guid userId, Guid snapshotId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "test";

        var aiDbContext = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        var aiRunId = Guid.NewGuid();
        var modelId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        aiDbContext.AiProviders.Add(new AiProvider
        {
            Id = providerId,
            Name = "StubProvider",
            CapabilitiesJson = "[]",
            IsActive = true
        });

        aiDbContext.AiModels.Add(new AiModel
        {
            Id = modelId,
            AiProviderId = providerId,
            ModelName = "stub-chat-model",
            ContextWindow = 16000,
            CostProfileJson = "{}",
            LatencyProfileJson = "{}",
            PolicyTagsJson = "[]",
            IsActive = true
        });

        aiDbContext.AiRuns.Add(new AiRun
        {
            Id = aiRunId,
            TenantId = tenantId,
            UserId = userId,
            UseCase = CustomerInsightAiSummaryContract.UseCase,
            AiModelId = modelId,
            InputRefsJson = "{}",
            Outcome = "Completed"
        });

        aiDbContext.CustomerInsightAiSummaries.Add(new CustomerInsightAiSummary
        {
            TenantId = tenantId,
            UserId = userId,
            CustomerInsightSnapshotId = snapshotId,
            AiRunId = aiRunId,
            Status = CustomerInsightAiSummaryContract.StatusCurrent,
            AsOfUtc = new DateTime(2026, 3, 31, 10, 0, 0, DateTimeKind.Utc),
            NarrativeVersion = CustomerInsightAiSummaryContract.BuildNarrativeVersion("stub-chat-model"),
            SummaryJson = BuildAiSummaryJson()
        });

        await aiDbContext.SaveChangesAsync();
    }

    private async Task SeedRebuildSnapshotScenarioAsync(Guid tenantId, Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "test";

        var financeDbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var accountId = Guid.Parse("89898989-8989-8989-8989-898989898989");

        financeDbContext.PersonalAccounts.Add(new PersonalAccount
        {
            Id = accountId,
            TenantId = tenantId,
            UserId = userId,
            Name = "Main Wallet",
            AccountType = "Bank",
            Currency = "USD",
            Status = "Active",
            CurrentBalance = 2200m,
            BalanceAsOf = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)
        });

        financeDbContext.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                PersonalAccountId = accountId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc),
                Amount = 3200m,
                Currency = "USD",
                Merchant = "Employer Inc",
                TransactionType = TransactionCategoryReference.TypeIncome,
                Category = TransactionCategoryReference.Income,
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                PersonalAccountId = accountId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = new DateTime(2026, 3, 11, 12, 0, 0, DateTimeKind.Utc),
                Amount = -600m,
                Currency = "USD",
                Merchant = "Tesco",
                TransactionType = TransactionCategoryReference.TypeExpense,
                Category = TransactionCategoryReference.Groceries,
                TagsJson = "[]"
            });

        await financeDbContext.SaveChangesAsync();
    }

    private async Task SeedAdminCustomerLinkAsync(Guid tenantId, Guid userId, Guid partyId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "test";

        var platformDbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        platformDbContext.Parties.Add(new Party
        {
            Id = partyId,
            TenantId = tenantId,
            PartyType = "Individual",
            DisplayName = "Insight Customer",
            Status = "Active"
        });

        platformDbContext.UserParties.Add(new UserParty
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId,
            LinkType = "Individual"
        });

        await platformDbContext.SaveChangesAsync();
    }

    private static string BuildSnapshotJson(Guid tenantId, Guid userId, decimal totalBalance)
    {
        var document = new CustomerInsightSnapshotDocument(
            CustomerInsightSnapshotContract.SchemaVersion,
            userId,
            tenantId,
            new DateTime(2026, 3, 31, 10, 0, 0, DateTimeKind.Utc),
            new CustomerInsightAnalysisWindow(
                new DateTime(2025, 10, 4, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc),
                CustomerInsightSnapshotContract.OperationalWindowDays,
                CustomerInsightSnapshotContract.TrendWindowDays,
                CustomerInsightSnapshotContract.BehaviourWindowDays,
                CustomerInsightSnapshotContract.ObligationsLookaheadDays),
            new CustomerInsightCurrencyPolicy(
                CustomerInsightSnapshotContract.MonetaryPolicyNativeCurrency,
                null,
                null,
                CustomerInsightSnapshotContract.TransferPolicyNormalizedTransfers),
            ["USD"],
            new CustomerInsightCoverage(false, ["accounts", "transactions"], [], [], []),
            new CustomerInsightMetrics(
                new CustomerInsightCashPosition(
                    1,
                    [new CustomerInsightMoneyAmount("USD", totalBalance)],
                    [new CustomerInsightMoneyAmount("USD", totalBalance)],
                    [new CustomerInsightAccountBalance(Guid.NewGuid(), "Main Wallet", "Bank", "USD", totalBalance, 100m)],
                    [new CustomerInsightConcentrationRatio("USD", 100m)]),
                new CustomerInsightIncomeSummary(
                    30,
                    new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc),
                    [new CustomerInsightMoneyAmount("USD", 3000m)],
                    [new CustomerInsightMoneyAmount("USD", 3000m)],
                    "monthly",
                    [new CustomerInsightSourceAmount("Employer", "USD", 3000m, 1)],
                    [new CustomerInsightAccountFlow(Guid.NewGuid(), "Main Wallet", "USD", 3000m, 1)],
                    [new CustomerInsightPeriodDelta("USD", 3000m, 2900m, 100m, 3.45m)]),
                new CustomerInsightExpenseSummary(
                    30,
                    new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc),
                    [new CustomerInsightMoneyAmount("USD", 1200m)],
                    [new CustomerInsightMoneyAmount("USD", 900m)],
                    [new CustomerInsightMoneyAmount("USD", 300m)],
                    [new CustomerInsightMoneyAmount("USD", 1000m)],
                    [new CustomerInsightMoneyAmount("USD", 200m)],
                    [new CustomerInsightAccountFlow(Guid.NewGuid(), "Main Wallet", "USD", 1200m, 3)],
                    [new CustomerInsightPeriodDelta("USD", 1200m, 1100m, 100m, 9.09m)],
                    [new CustomerInsightAverageSpend("USD", 280m, 1200m)]),
                new CustomerInsightCategoryInsights(
                    30,
                    new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc),
                    [new CustomerInsightCategorySpend("housing", "USD", 900m, 75m, 1, 850m, 5.88m)],
                    [new CustomerInsightCategorySpend("housing", "USD", 900m, 75m, 1, 850m, 5.88m)],
                    [new CustomerInsightCategorySpend("housing", "USD", 900m, 75m, 1, 850m, 5.88m)],
                    [new CustomerInsightConcentrationRatio("USD", 75m)],
                    []),
                new CustomerInsightMerchantInsights(
                    30,
                    new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc),
                    [new CustomerInsightMerchantSpend("Landlord", "USD", 900m, 75m, 1)],
                    [new CustomerInsightMerchantFrequency("Landlord", "USD", 1, 900m)],
                    [],
                    [new CustomerInsightConcentrationRatio("USD", 75m)],
                    []),
                new CustomerInsightObligationInsights(
                    30,
                    new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 4, 30, 23, 59, 59, DateTimeKind.Utc),
                    [],
                    [],
                    [],
                    [],
                    []),
                new CustomerInsightBudgetInsights(0, [], [], [], []),
                new CustomerInsightGoalInsights(0, [], CustomerInsightSnapshotContract.ConfidenceLow)),
            [],
            new CustomerInsightRiskOverview(CustomerInsightSnapshotContract.SeverityLow, CustomerInsightSnapshotContract.SeverityLow, [], CustomerInsightSnapshotContract.SeverityLow, []),
            new CustomerInsightEvidence(0, 0, [], new DateTime(2025, 10, 4, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc), [], [], [], []),
            null,
            null);

        return System.Text.Json.JsonSerializer.Serialize(document);
    }

    private static string BuildAiSummaryJson()
    {
        var document = new CustomerInsightAiSummaryDocument(
            CustomerInsightAiSummaryContract.SchemaVersion,
            "Stable cash position",
            "Cashflow remains stable with manageable discretionary pressure.",
            ["Cash buffers remain healthy."],
            ["Savings remain on track."],
            ["Entertainment spend is rising."],
            ["Review discretionary categories."],
            ["Ask about month-end spend spikes."],
            ["metrics.cashPosition.totalBalanceByCurrency"],
            []);

        return System.Text.Json.JsonSerializer.Serialize(document);
    }

    private sealed class CustomerInsightsResponse
    {
        public CustomerInsightAiSummaryDetail? AiSummary { get; set; }
        public CustomerInsightSnapshotOverview? Snapshot { get; set; }
    }

    private sealed class CustomerInsightAiSummaryDetail
    {
        public Guid Id { get; set; }
        public string Headline { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> KeyObservations { get; set; } = [];
        public List<string> PositivePatterns { get; set; } = [];
        public List<string> RiskPatterns { get; set; } = [];
        public List<string> RecommendedFocusAreas { get; set; } = [];
        public List<string> ConversationSuggestions { get; set; } = [];
        public List<string> Caveats { get; set; } = [];
        public string NarrativeVersion { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
    }

    private sealed class CustomerInsightSnapshotOverview
    {
        public Guid Id { get; set; }
        public DateTime AsOfUtc { get; set; }
        public bool IsPartial { get; set; }
        public string? TopSignalTitle { get; set; }
        public string? CashflowStressLevel { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
