using System.Net;
using System.Net.Http.Json;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
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
                    [new CustomerInsightConcentrationRatio("USD", 75m)]),
                new CustomerInsightMerchantInsights(
                    30,
                    new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc),
                    [new CustomerInsightMerchantSpend("Landlord", "USD", 900m, 75m, 1)],
                    [new CustomerInsightMerchantFrequency("Landlord", "USD", 1, 900m)],
                    [],
                    [new CustomerInsightConcentrationRatio("USD", 75m)]),
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
            new CustomerInsightEvidence(0, 0, [], new DateTime(2025, 10, 4, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc), [], [], [], []));

        return System.Text.Json.JsonSerializer.Serialize(document);
    }
}
