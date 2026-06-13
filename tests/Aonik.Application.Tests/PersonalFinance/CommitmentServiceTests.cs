using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.PersonalFinance;

public class CommitmentServiceTests
{
    // ═══════════════════════════════════════════════════════════════════
    // ListCommitmentsAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ListCommitmentsAsync_Should_ReturnAllTypes_When_NoFilterApplied()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        SeedBill(context, tenantId, userId, "Thames Water", "Active");
        SeedSubscription(context, tenantId, userId, "Netflix", "Active");
        SeedDebtRepayment(context, tenantId, userId, "Halifax Mortgage", "Active");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        var result = await service.ListCommitmentsAsync(new CommitmentListFilter());

        // Assert
        result.Items.Should().HaveCount(3);
        result.Totals.BillsCount.Should().Be(1);
        result.Totals.SubscriptionsCount.Should().Be(1);
        result.Totals.DebtRepaymentsCount.Should().Be(1);
    }

    [Fact]
    public async Task ListCommitmentsAsync_Should_FilterByType_When_TypeSpecified()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        SeedBill(context, tenantId, userId, "Council Tax", "Active");
        SeedSubscription(context, tenantId, userId, "Spotify", "Active");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        var result = await service.ListCommitmentsAsync(new CommitmentListFilter(Type: "Subscription"));

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].DisplayName.Should().Be("Spotify");
        result.Items[0].CommitmentType.Should().Be("Subscription");
    }

    [Fact]
    public async Task ListCommitmentsAsync_Should_FilterByStatus_When_StatusSpecified()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        SeedBill(context, tenantId, userId, "Active Bill", "Active");
        SeedBill(context, tenantId, userId, "Cancelled Bill", "Cancelled");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        var result = await service.ListCommitmentsAsync(new CommitmentListFilter(Status: "Active"));

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].DisplayName.Should().Be("Active Bill");
    }

    [Fact]
    public async Task ListCommitmentsAsync_Should_FilterByVerificationStatus_When_Specified()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        SeedBill(context, tenantId, userId, "Confirmed Bill", "Active", verificationStatus: "Confirmed");
        SeedBill(context, tenantId, userId, "Detected Bill", "Active", verificationStatus: "Detected");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        var result = await service.ListCommitmentsAsync(new CommitmentListFilter(VerificationStatus: "Detected"));

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].DisplayName.Should().Be("Detected Bill");
        result.Totals.DetectedCount.Should().Be(1);
    }

    [Fact]
    public async Task ListCommitmentsAsync_Should_IsolateTenants_When_MultipleTenantsExist()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantA);

        SeedBill(context, tenantA, userId, "Tenant A Bill", "Active");
        SeedBill(context, tenantB, userId, "Tenant B Bill", "Active");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantA, userId);

        // Act
        var result = await service.ListCommitmentsAsync(new CommitmentListFilter());

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].DisplayName.Should().Be("Tenant A Bill");
    }

    [Fact]
    public async Task ListCommitmentsAsync_Should_Paginate_When_PageSizeExceeded()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        for (var i = 0; i < 5; i++)
            SeedBill(context, tenantId, userId, $"Bill {i}", "Active");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        var page1 = await service.ListCommitmentsAsync(new CommitmentListFilter(Page: 1, PageSize: 3));
        var page2 = await service.ListCommitmentsAsync(new CommitmentListFilter(Page: 2, PageSize: 3));

        // Assert
        page1.Items.Should().HaveCount(3);
        page1.HasMore.Should().BeTrue();
        page2.Items.Should().HaveCount(2);
        page2.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task ListCommitmentsAsync_Should_FilterBySearch_When_SearchSpecified()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        SeedBill(context, tenantId, userId, "Thames Water", "Active");
        SeedSubscription(context, tenantId, userId, "Netflix", "Active");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        var result = await service.ListCommitmentsAsync(new CommitmentListFilter(Search: "thames"));

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].DisplayName.Should().Be("Thames Water");
    }

    [Fact]
    public async Task ListCommitmentsAsync_Should_ComputeTotals_When_CommitmentsExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        SeedBill(context, tenantId, userId, "Bill 1", "Active", amount: 100m,
            dueDate: DateTime.UtcNow.AddDays(3));
        SeedBill(context, tenantId, userId, "Bill 2", "Active", amount: 50m,
            dueDate: DateTime.UtcNow.AddDays(10));
        SeedBill(context, tenantId, userId, "Detected", "Active", amount: 25m,
            dueDate: DateTime.UtcNow.AddDays(2), verificationStatus: "Detected");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        var result = await service.ListCommitmentsAsync(new CommitmentListFilter());

        // Assert
        result.Totals.TotalUpcomingAmount.Should().Be(175m);
        result.Totals.DueSoonCount.Should().Be(2); // within 7 days
        result.Totals.DetectedCount.Should().Be(1);
        result.Totals.BillsCount.Should().Be(3);
    }

    // ═══════════════════════════════════════════════════════════════════
    // GetCommitmentAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCommitmentAsync_Should_ReturnBill_When_BillIdProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var bill = SeedBill(context, tenantId, userId, "BT Broadband", "Active");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        var result = await service.GetCommitmentAsync(bill.Id);

        // Assert
        result.Should().NotBeNull();
        result!.CommitmentType.Should().Be("Bill");
        result.DisplayName.Should().Be("BT Broadband");
    }

    [Fact]
    public async Task GetCommitmentAsync_Should_ReturnSubscription_When_SubscriptionIdProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var sub = SeedSubscription(context, tenantId, userId, "Disney+", "Active");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        var result = await service.GetCommitmentAsync(sub.Id);

        // Assert
        result.Should().NotBeNull();
        result!.CommitmentType.Should().Be("Subscription");
        result.DisplayName.Should().Be("Disney+");
    }

    [Fact]
    public async Task GetCommitmentAsync_Should_ReturnDebtRepayment_When_DebtIdProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var debt = SeedDebtRepayment(context, tenantId, userId, "Student Loan", "Active");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        var result = await service.GetCommitmentAsync(debt.Id);

        // Assert
        result.Should().NotBeNull();
        result!.CommitmentType.Should().Be("DebtRepayment");
        result.DisplayName.Should().Be("Student Loan");
    }

    [Fact]
    public async Task GetCommitmentAsync_Should_ReturnNull_When_IdNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        // Act
        var result = await service.GetCommitmentAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // CreateFromTransactionAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateFromTransactionAsync_Should_CreateBill_When_BillTypeSpecified()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var tx = SeedTransaction(context, tenantId, userId, -150m, "Thames Water");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);
        var request = new CreateCommitmentFromTransactionRequest(
            TransactionId: tx.Id,
            CommitmentType: "Bill",
            DisplayName: "Thames Water",
            Frequency: "Monthly",
            NextDueDate: DateTime.UtcNow.AddDays(30),
            ExpectedAmount: 150m,
            Currency: "GBP",
            PaidFromAccountId: null,
            Autopay: false,
            Notes: null,
            DebtType: null,
            AccountReference: null);

        // Act
        var result = await service.CreateFromTransactionAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.CommitmentType.Should().Be("Bill");
        result.DisplayName.Should().Be("Thames Water");
        result.Origin.Should().Be("PromotedFromTransaction");
        result.VerificationStatus.Should().Be("Confirmed");
    }

    [Fact]
    public async Task CreateFromTransactionAsync_Should_CreateSubscription_When_SubscriptionTypeSpecified()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var tx = SeedTransaction(context, tenantId, userId, -9.99m, "Netflix");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);
        var request = new CreateCommitmentFromTransactionRequest(
            TransactionId: tx.Id,
            CommitmentType: "Subscription",
            DisplayName: "Netflix",
            Frequency: "Monthly",
            NextDueDate: DateTime.UtcNow.AddDays(30),
            ExpectedAmount: 9.99m,
            Currency: "GBP",
            PaidFromAccountId: null,
            Autopay: true,
            Notes: null,
            DebtType: null,
            AccountReference: null);

        // Act
        var result = await service.CreateFromTransactionAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.CommitmentType.Should().Be("Subscription");
        result.DisplayName.Should().Be("Netflix");
        result.Autopay.Should().BeTrue();
    }

    [Fact]
    public async Task CreateFromTransactionAsync_Should_CreateDebtRepayment_When_DebtTypeSpecified()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var tx = SeedTransaction(context, tenantId, userId, -850m, "Halifax");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);
        var request = new CreateCommitmentFromTransactionRequest(
            TransactionId: tx.Id,
            CommitmentType: "DebtRepayment",
            DisplayName: "Halifax",
            Frequency: "Monthly",
            NextDueDate: DateTime.UtcNow.AddDays(30),
            ExpectedAmount: 850m,
            Currency: "GBP",
            PaidFromAccountId: null,
            Autopay: true,
            Notes: null,
            DebtType: "Mortgage",
            AccountReference: "MORT-12345");

        // Act
        var result = await service.CreateFromTransactionAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.CommitmentType.Should().Be("DebtRepayment");
        result.DisplayName.Should().Be("Halifax");
        result.AccountReference.Should().Be("MORT-12345");
    }

    [Fact]
    public async Task CreateFromTransactionAsync_Should_Throw_When_TransactionNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var request = new CreateCommitmentFromTransactionRequest(
            TransactionId: Guid.NewGuid(),
            CommitmentType: "Bill",
            DisplayName: "Missing",
            Frequency: "Monthly",
            NextDueDate: DateTime.UtcNow.AddDays(30),
            ExpectedAmount: 100m,
            Currency: "GBP",
            PaidFromAccountId: null,
            Autopay: false,
            Notes: null,
            DebtType: null,
            AccountReference: null);

        // Act
        Func<Task> action = () => service.CreateFromTransactionAsync(request);

        // Assert
        await action.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*not found*");
    }

    // ═══════════════════════════════════════════════════════════════════
    // ConfirmDetectedAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ConfirmDetectedAsync_Should_TransitionToConfirmed_When_StatusIsDetected()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var bill = SeedBill(context, tenantId, userId, "Detected Bill", "Active",
            verificationStatus: "Detected");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        var result = await service.ConfirmDetectedAsync(bill.Id);

        // Assert
        result.VerificationStatus.Should().Be("Confirmed");

        // Verify persisted
        var persisted = await context.Set<PersonalRecurringBill>()
            .FirstAsync(b => b.Id == bill.Id);
        persisted.VerificationStatus.Should().Be("Confirmed");
    }

    [Fact]
    public async Task ConfirmDetectedAsync_Should_Throw_When_AlreadyConfirmed()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var bill = SeedBill(context, tenantId, userId, "Confirmed Bill", "Active",
            verificationStatus: "Confirmed");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        Func<Task> action = () => service.ConfirmDetectedAsync(bill.Id);

        // Assert
        await action.Should().ThrowAsync<InvalidStateException>()
            .WithMessage("*expected 'Detected'*");
    }

    [Fact]
    public async Task ConfirmDetectedAsync_Should_Throw_When_NotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        // Act
        Func<Task> action = () => service.ConfirmDetectedAsync(Guid.NewGuid());

        // Assert
        await action.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*not found*");
    }

    // ═══════════════════════════════════════════════════════════════════
    // RejectDetectedAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RejectDetectedAsync_Should_TransitionToRejected_When_StatusIsDetected()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var sub = SeedSubscription(context, tenantId, userId, "False Positive", "Active",
            verificationStatus: "Detected");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        await service.RejectDetectedAsync(sub.Id, "Not a real subscription");

        // Assert
        var persisted = await context.Set<Subscription>()
            .FirstAsync(s => s.Id == sub.Id);
        persisted.VerificationStatus.Should().Be("Rejected");
        persisted.Notes.Should().Contain("Rejected: Not a real subscription");
    }

    [Fact]
    public async Task RejectDetectedAsync_Should_Throw_When_AlreadyConfirmed()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var sub = SeedSubscription(context, tenantId, userId, "Confirmed Sub", "Active",
            verificationStatus: "Confirmed");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        Func<Task> action = () => service.RejectDetectedAsync(sub.Id);

        // Assert
        await action.Should().ThrowAsync<InvalidStateException>()
            .WithMessage("*expected 'Detected'*");
    }

    // ═══════════════════════════════════════════════════════════════════
    // ListDetectedAsync
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ListDetectedAsync_Should_ReturnOnlyDetected_When_MixedStatusesExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        SeedBill(context, tenantId, userId, "Confirmed Bill", "Active", verificationStatus: "Confirmed");
        SeedBill(context, tenantId, userId, "Detected Bill", "Active", verificationStatus: "Detected");
        SeedSubscription(context, tenantId, userId, "Detected Sub", "Active", verificationStatus: "Detected");
        SeedDebtRepayment(context, tenantId, userId, "Rejected Debt", "Active", verificationStatus: "Rejected");
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        // Act
        var result = await service.ListDetectedAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Select(i => i.DisplayName).Should().Contain("Detected Bill");
        result.Select(i => i.DisplayName).Should().Contain("Detected Sub");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static PersonalFinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private sealed class FakeTaskService : ITaskService
    {
        public Task<TaskResponse> ScheduleAsync(ScheduleTaskRequest request, CancellationToken ct = default) => Task.FromResult<TaskResponse>(null!);
        public Task<TaskResponse?> GetAsync(Guid taskId, CancellationToken ct = default) => Task.FromResult<TaskResponse?>(null);
        public Task<IReadOnlyList<TaskResponse>> ListForSubjectAsync(string subjectType, Guid subjectId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TaskResponse>>([]);
        public Task<IReadOnlyList<TaskResponse>> ListForAssigneeAsync(string assigneeType, Guid? assigneeId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TaskResponse>>([]);
        public Task PauseAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResumeAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
        public Task CancelAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static CommitmentService CreateService(
        PersonalFinanceDbContext context, Guid tenantId, Guid userId)
    {
        var tenantProvider = new TestTenantProvider(tenantId);
        var userProvider = new TestCurrentUserProvider(userId);
        var paymentLogService = new PaymentLogService(context, tenantProvider, userProvider);
        return new CommitmentService(
            context,
            tenantProvider,
            userProvider,
            paymentLogService,
            new FakeTaskService(),
            NullLogger<CommitmentService>.Instance);
    }

    private static PersonalRecurringBill SeedBill(
        PersonalFinanceDbContext context,
        Guid tenantId,
        Guid userId,
        string payee,
        string status,
        string verificationStatus = "Confirmed",
        decimal? amount = 100m,
        DateTime? dueDate = null)
    {
        var bill = new PersonalRecurringBill
        {
            TenantId = tenantId,
            UserId = userId,
            Payee = payee,
            Frequency = "Monthly",
            NextDueDate = dueDate ?? DateTime.UtcNow.AddDays(15),
            ExpectedAmount = amount,
            Currency = "GBP",
            Status = status,
            VerificationStatus = verificationStatus,
            Origin = verificationStatus == "Detected" ? "Detected" : "Manual",
        };
        context.Set<PersonalRecurringBill>().Add(bill);
        return bill;
    }

    private static Subscription SeedSubscription(
        PersonalFinanceDbContext context,
        Guid tenantId,
        Guid userId,
        string merchant,
        string status,
        string verificationStatus = "Confirmed")
    {
        var sub = new Subscription
        {
            TenantId = tenantId,
            UserId = userId,
            Merchant = merchant,
            RenewalDate = DateTime.UtcNow.AddDays(15),
            ExpectedAmount = 9.99m,
            Currency = "GBP",
            Status = status,
            DetectedBy = "Test",
            VerificationStatus = verificationStatus,
            Origin = verificationStatus == "Detected" ? "Detected" : "Manual",
        };
        context.Set<Subscription>().Add(sub);
        return sub;
    }

    private static DebtRepayment SeedDebtRepayment(
        PersonalFinanceDbContext context,
        Guid tenantId,
        Guid userId,
        string creditorName,
        string status,
        string verificationStatus = "Confirmed")
    {
        var debt = new DebtRepayment
        {
            TenantId = tenantId,
            UserId = userId,
            CreditorName = creditorName,
            DebtType = "PersonalLoan",
            NextDueDate = DateTime.UtcNow.AddDays(15),
            ExpectedAmount = 500m,
            Currency = "GBP",
            Status = status,
            VerificationStatus = verificationStatus,
            Origin = verificationStatus == "Detected" ? "Detected" : "Manual",
        };
        context.Set<DebtRepayment>().Add(debt);
        return debt;
    }

    private static PersonalTransaction SeedTransaction(
        PersonalFinanceDbContext context,
        Guid tenantId,
        Guid userId,
        decimal amount,
        string merchant)
    {
        var tx = new PersonalTransaction
        {
            TenantId = tenantId,
            UserId = userId,
            OccurredAt = DateTime.UtcNow.AddDays(-5),
            Amount = amount,
            Currency = "GBP",
            Merchant = merchant,
            SourceType = "Manual",
            SourceId = Guid.NewGuid(),
            TransactionType = amount < 0 ? "Expense" : "Income",
        };
        context.Set<PersonalTransaction>().Add(tx);
        return tx;
    }

    // ── Test Doubles ─────────────────────────────────────────────

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
}
