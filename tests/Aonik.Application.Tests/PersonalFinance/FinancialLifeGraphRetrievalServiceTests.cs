using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.PersonalFinance;

public class FinancialLifeGraphRetrievalServiceTests
{
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

    private sealed class StubPartyReader : IPartyReader
    {
        private readonly Dictionary<Guid, PartyHistoryItem> _parties;
        private readonly List<PartyRelationshipHistoryItem> _relationships;

        public StubPartyReader(
            IEnumerable<PartyHistoryItem>? parties = null,
            IEnumerable<PartyRelationshipHistoryItem>? relationships = null)
        {
            _parties = (parties ?? []).ToDictionary(p => p.PartyId);
            _relationships = (relationships ?? []).ToList();
        }

        public Task<IReadOnlyList<PartyHistoryItem>> GetByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> partyIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PartyHistoryItem>>(
                partyIds.Where(_parties.ContainsKey).Select(id => _parties[id]).ToList());

        public Task<IReadOnlyList<PartyRelationshipHistoryItem>> GetRelationshipsForPartyAsync(
            Guid tenantId, Guid partyId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PartyRelationshipHistoryItem>>(
                _relationships.Where(r => r.FromPartyId == partyId || r.ToPartyId == partyId).ToList());

        public Task<bool> ExistsAsync(Guid tenantId, Guid partyId, CancellationToken ct = default)
            => Task.FromResult(_parties.ContainsKey(partyId));

        public Task<bool> HasActiveRelationshipBetweenAsync(
            Guid tenantId, Guid partyAId, Guid partyBId, CancellationToken ct = default)
            => Task.FromResult(_relationships.Any(r => r.IsActive
                && ((r.FromPartyId == partyAId && r.ToPartyId == partyBId)
                    || (r.ToPartyId == partyAId && r.FromPartyId == partyBId))));

        public Task<Guid?> GetTenantPartyIdAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<Guid?>(_parties.Keys.OrderBy(id => id).Cast<Guid?>().FirstOrDefault());
    }

    private static PersonalFinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"GraphRetrieval_{Guid.NewGuid()}")
            .Options;
        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static FinancialLifeGraphRetrievalService CreateService(
        PersonalFinanceDbContext context,
        Guid tenantId,
        Guid userId,
        IPartyReader? partyReader = null)
    {
        return new FinancialLifeGraphRetrievalService(
            context,
            partyReader ?? new StubPartyReader(),
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            NullLogger<FinancialLifeGraphRetrievalService>.Instance);
    }

    // ────────────────────────────────────────────────────────────────
    // GetBillPaymentHistoryAsync
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBillPaymentHistoryAsync_ShouldReturnPaymentHistory_WhenBillExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        context.PersonalAccounts.Add(new PersonalAccount
        {
            Id = accountId, TenantId = tenantId, UserId = userId,
            Name = "Main", AccountType = "Checking", Currency = "USD",
            Status = "Active", CurrentBalance = 5000m, CreatedAt = DateTime.UtcNow
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

        // Add payment transactions matching the bill's payee
        context.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
                PersonalAccountId = accountId, Amount = -120m, Currency = "USD",
                Merchant = "Electric Co", Description = "Bill payment",
                OccurredAt = DateTime.UtcNow.AddDays(-30),
                SourceType = "Manual", TransactionType = "Expense",
                CreatedAt = DateTime.UtcNow
            },
            new PersonalTransaction
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
                PersonalAccountId = accountId, Amount = -115m, Currency = "USD",
                Merchant = "Electric Co", Description = "Bill payment",
                OccurredAt = DateTime.UtcNow.AddDays(-60),
                SourceType = "Manual", TransactionType = "Expense",
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);
        var nodeKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.Bill, billId);

        // Act
        var result = await service.GetBillPaymentHistoryAsync(nodeKey);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.BillId.Should().Be(billId);
        result.Data.Payee.Should().Be("Electric Co");
        result.Data.PaymentCount.Should().Be(2);
        result.Data.TotalPaid.Should().Be(235m); // 120 + 115 (absolute values)
        result.Data.Payments.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBillPaymentHistoryAsync_ShouldFail_WhenBillNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var nodeKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.Bill, Guid.NewGuid());

        // Act
        var result = await service.GetBillPaymentHistoryAsync(nodeKey);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Contain("not found");
    }

    [Fact]
    public async Task GetBillPaymentHistoryAsync_ShouldBoundTimeWindow_WhenExceedsMaxDays()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        context.Bills.Add(new Bill
        {
            Id = billId, TenantId = tenantId, UserId = userId,
            Payee = "Water Co", Frequency = "Monthly",
            NextDueDate = DateTime.UtcNow.AddDays(10),
            ExpectedAmount = 50m, Currency = "USD", Status = "Active",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);
        var nodeKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.Bill, billId);

        // Act — request a window exceeding 730 days
        var from = DateTime.UtcNow.AddDays(-1000);
        var to = DateTime.UtcNow;
        var result = await service.GetBillPaymentHistoryAsync(nodeKey, from, to);

        // Assert — should succeed but clamp the window (not fail)
        result.Success.Should().BeTrue();
    }

    // ────────────────────────────────────────────────────────────────
    // GetGoalContributionHistoryAsync
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGoalContributionHistoryAsync_ShouldReturnContributions_WhenGoalExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var fundingAccountId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        context.PersonalAccounts.Add(new PersonalAccount
        {
            Id = fundingAccountId, TenantId = tenantId, UserId = userId,
            Name = "Savings", AccountType = "Savings", Currency = "USD",
            Status = "Active", CurrentBalance = 5000m, CreatedAt = DateTime.UtcNow
        });

        context.Goals.Add(new Goal
        {
            Id = goalId, TenantId = tenantId, UserId = userId,
            Name = "Vacation Fund", TargetAmount = 5000m, Currency = "USD",
            ProgressAmount = 1500m, Status = "Active",
            FundingAccountId = fundingAccountId,
            CreatedAt = DateTime.UtcNow
        });

        // Add transfer transactions to the funding account
        context.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
                PersonalAccountId = fundingAccountId, Amount = 500m, Currency = "USD",
                Description = "Monthly transfer", Category = "Transfer",
                OccurredAt = DateTime.UtcNow.AddDays(-30),
                SourceType = "Manual", TransactionType = "Income",
                CreatedAt = DateTime.UtcNow
            },
            new PersonalTransaction
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
                PersonalAccountId = fundingAccountId, Amount = 1000m, Currency = "USD",
                Description = "Big transfer", Category = "Transfer",
                OccurredAt = DateTime.UtcNow.AddDays(-60),
                SourceType = "Manual", TransactionType = "Income",
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);
        var nodeKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.Goal, goalId);

        // Act
        var result = await service.GetGoalContributionHistoryAsync(nodeKey);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.GoalId.Should().Be(goalId);
        result.Data.GoalName.Should().Be("Vacation Fund");
        result.Data.TargetAmount.Should().Be(5000m);
        result.Data.ProgressAmount.Should().Be(1500m);
        result.Data.ContributionCount.Should().Be(2);
        result.Data.TotalContributed.Should().Be(1500m);
    }

    [Fact]
    public async Task GetGoalContributionHistoryAsync_ShouldFail_WhenGoalNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var nodeKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.Goal, Guid.NewGuid());

        // Act
        var result = await service.GetGoalContributionHistoryAsync(nodeKey);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Contain("not found");
    }

    [Fact]
    public async Task GetGoalContributionHistoryAsync_ShouldReturnEmpty_WhenNoFundingAccount()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        context.Goals.Add(new Goal
        {
            Id = goalId, TenantId = tenantId, UserId = userId,
            Name = "No Funding Goal", TargetAmount = 1000m, Currency = "USD",
            ProgressAmount = 0m, Status = "Active",
            FundingAccountId = null,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);
        var nodeKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.Goal, goalId);

        // Act
        var result = await service.GetGoalContributionHistoryAsync(nodeKey);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.ContributionCount.Should().Be(0);
        result.Data.TotalContributed.Should().Be(0);
    }

    // ────────────────────────────────────────────────────────────────
    // GetAccountStatementAsync
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccountStatementAsync_ShouldReturnStatement_WhenAccountExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        context.PersonalAccounts.Add(new PersonalAccount
        {
            Id = accountId, TenantId = tenantId, UserId = userId,
            Name = "Main Checking", AccountType = "Checking", Currency = "USD",
            Status = "Active", CurrentBalance = 5000m, CreatedAt = DateTime.UtcNow
        });

        var now = DateTime.UtcNow;
        context.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
                PersonalAccountId = accountId, Amount = 2000m, Currency = "USD",
                Merchant = "Employer", Description = "Salary",
                OccurredAt = now.AddDays(-20),
                SourceType = "Manual", TransactionType = "Income",
                CreatedAt = now
            },
            new PersonalTransaction
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
                PersonalAccountId = accountId, Amount = -150m, Currency = "USD",
                Merchant = "Grocery Store", Description = "Groceries",
                OccurredAt = now.AddDays(-15),
                SourceType = "Manual", TransactionType = "Expense",
                CreatedAt = now
            },
            new PersonalTransaction
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
                PersonalAccountId = accountId, Amount = -50m, Currency = "USD",
                Merchant = "Gas Station", Description = "Gas",
                OccurredAt = now.AddDays(-10),
                SourceType = "Manual", TransactionType = "Expense",
                CreatedAt = now
            });
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);
        var nodeKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.PersonalAccount, accountId);

        // Act
        var result = await service.GetAccountStatementAsync(nodeKey, now.AddDays(-30), now);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccountId.Should().Be(accountId);
        result.Data.AccountName.Should().Be("Main Checking");
        result.Data.TransactionCount.Should().Be(3);
        result.Data.TotalInflow.Should().Be(2000m);
        result.Data.TotalOutflow.Should().Be(200m); // 150 + 50
        result.Data.NetChange.Should().Be(1800m);
        result.Data.Transactions.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAccountStatementAsync_ShouldCalculateRunningBalance()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        context.PersonalAccounts.Add(new PersonalAccount
        {
            Id = accountId, TenantId = tenantId, UserId = userId,
            Name = "Test Account", AccountType = "Checking", Currency = "USD",
            Status = "Active", CurrentBalance = 0m, CreatedAt = DateTime.UtcNow
        });

        var now = DateTime.UtcNow;
        context.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
                PersonalAccountId = accountId, Amount = 1000m, Currency = "USD",
                OccurredAt = now.AddDays(-3),
                SourceType = "Manual", TransactionType = "Income",
                CreatedAt = now
            },
            new PersonalTransaction
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
                PersonalAccountId = accountId, Amount = -300m, Currency = "USD",
                OccurredAt = now.AddDays(-2),
                SourceType = "Manual", TransactionType = "Expense",
                CreatedAt = now
            },
            new PersonalTransaction
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
                PersonalAccountId = accountId, Amount = -200m, Currency = "USD",
                OccurredAt = now.AddDays(-1),
                SourceType = "Manual", TransactionType = "Expense",
                CreatedAt = now
            });
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);
        var nodeKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.PersonalAccount, accountId);

        // Act
        var result = await service.GetAccountStatementAsync(nodeKey, now.AddDays(-5), now);

        // Assert
        result.Success.Should().BeTrue();
        var txns = result.Data!.Transactions;
        txns.Should().HaveCount(3);
        txns[0].RunningBalance.Should().Be(1000m);   // +1000
        txns[1].RunningBalance.Should().Be(700m);     // +1000 - 300
        txns[2].RunningBalance.Should().Be(500m);     // +1000 - 300 - 200
    }

    [Fact]
    public async Task GetAccountStatementAsync_ShouldFail_WhenWindowExceedsMaxDays()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        context.PersonalAccounts.Add(new PersonalAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
            Name = "Test", AccountType = "Checking", Currency = "USD",
            Status = "Active", CurrentBalance = 0m, CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);
        var nodeKey = FinancialLifeGraphNodeKeys.Build(
            FinancialLifeGraphNodeKeys.PersonalAccount, Guid.NewGuid());

        // Act — request window > 365 days
        var from = DateTime.UtcNow.AddDays(-400);
        var to = DateTime.UtcNow;
        var result = await service.GetAccountStatementAsync(nodeKey, from, to);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Contain("exceeds maximum");
    }

    [Fact]
    public async Task GetAccountStatementAsync_ShouldFail_WhenToDateBeforeFromDate()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);
        var nodeKey = FinancialLifeGraphNodeKeys.Build(
            FinancialLifeGraphNodeKeys.PersonalAccount, Guid.NewGuid());

        // Act
        var from = DateTime.UtcNow;
        var to = DateTime.UtcNow.AddDays(-10);
        var result = await service.GetAccountStatementAsync(nodeKey, from, to);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Contain("'to' date must be after 'from' date");
    }

    [Fact]
    public async Task GetAccountStatementAsync_ShouldFail_WhenAccountNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var nodeKey = FinancialLifeGraphNodeKeys.Build(
            FinancialLifeGraphNodeKeys.PersonalAccount, Guid.NewGuid());

        // Act
        var result = await service.GetAccountStatementAsync(
            nodeKey, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Contain("not found");
    }

    // ────────────────────────────────────────────────────────────────
    // GetPartyObligationSummaryAsync
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPartyObligationSummaryAsync_ShouldReturnObligations_WhenPartyExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var partyReader = new StubPartyReader(
            parties: [new PartyHistoryItem(partyId, "Electric Co", "Active", null)]);

        context.Bills.Add(new Bill
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
            Payee = "Electric Co", Frequency = "Monthly",
            NextDueDate = DateTime.UtcNow.AddDays(15),
            ExpectedAmount = 120m, Currency = "USD", Status = "Active",
            CreatedAt = DateTime.UtcNow
        });

        context.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
            Merchant = "Electric Co Premium", ExpectedAmount = 25m, Currency = "USD",
            RenewalDate = DateTime.UtcNow.AddDays(30), Status = "Active",
            DetectedBy = "Manual", CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId, partyReader);
        var nodeKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.Party, partyId);

        // Act
        var result = await service.GetPartyObligationSummaryAsync(nodeKey);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.PartyId.Should().Be(partyId);
        result.Data.PartyDisplayName.Should().Be("Electric Co");
        result.Data.TotalObligations.Should().Be(2); // 1 bill + 1 subscription
        result.Data.Obligations.Should().HaveCount(2);
        result.Data.TotalMonthlyEstimate.Should().BeGreaterThan(0);
        result.Data.PrimaryCurrency.Should().Be("USD");
    }

    [Fact]
    public async Task GetPartyObligationSummaryAsync_ShouldFail_WhenPartyNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var nodeKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.Party, Guid.NewGuid());

        // Act
        var result = await service.GetPartyObligationSummaryAsync(nodeKey);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Contain("not found");
    }

    [Fact]
    public async Task GetPartyObligationSummaryAsync_ShouldExcludeCancelledObligations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var partyReader = new StubPartyReader(
            parties: [new PartyHistoryItem(partyId, "Cable Co", "Active", null)]);

        context.Bills.AddRange(
            new Bill
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
                Payee = "Cable Co", Frequency = "Monthly",
                NextDueDate = DateTime.UtcNow.AddDays(10),
                ExpectedAmount = 80m, Currency = "USD", Status = "Active",
                CreatedAt = DateTime.UtcNow
            },
            new Bill
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
                Payee = "Cable Co Premium", Frequency = "Monthly",
                NextDueDate = DateTime.UtcNow.AddDays(10),
                ExpectedAmount = 40m, Currency = "USD", Status = "Cancelled",
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId, partyReader);
        var nodeKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.Party, partyId);

        // Act
        var result = await service.GetPartyObligationSummaryAsync(nodeKey);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.TotalObligations.Should().Be(1, "Cancelled bills should be excluded");
    }

    // ────────────────────────────────────────────────────────────────
    // General envelope validation
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AllRetrievalMethods_ShouldPopulateNodeKeyAndToolName_InResult()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var billKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.Bill, Guid.NewGuid());
        var goalKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.Goal, Guid.NewGuid());
        var acctKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.PersonalAccount, Guid.NewGuid());
        var partyKey = FinancialLifeGraphNodeKeys.Build(FinancialLifeGraphNodeKeys.Party, Guid.NewGuid());

        // Act
        var billResult = await service.GetBillPaymentHistoryAsync(billKey);
        var goalResult = await service.GetGoalContributionHistoryAsync(goalKey);
        var acctResult = await service.GetAccountStatementAsync(acctKey, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var partyResult = await service.GetPartyObligationSummaryAsync(partyKey);

        // Assert — every result should have the correct NodeKey and a non-empty ToolName
        billResult.NodeKey.Should().Be(billKey);
        billResult.ToolName.Should().NotBeNullOrWhiteSpace();

        goalResult.NodeKey.Should().Be(goalKey);
        goalResult.ToolName.Should().NotBeNullOrWhiteSpace();

        acctResult.NodeKey.Should().Be(acctKey);
        acctResult.ToolName.Should().NotBeNullOrWhiteSpace();

        partyResult.NodeKey.Should().Be(partyKey);
        partyResult.ToolName.Should().NotBeNullOrWhiteSpace();
    }

}
