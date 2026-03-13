using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

public class PersonalAccountLinkServiceTests
{
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

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }

        public string? ResolutionSource { get; set; }

        public bool IsResolved => TenantId.HasValue;
    }

    private sealed class FakeAccountLinkProviderGateway : IPersonalAccountLinkProviderGateway
    {
        public string ProviderCode => "Plaid";

        public string DisplayName => "Plaid";

        public Task<AccountLinkProviderSessionResult> CreateSessionAsync(
            AccountLinkProviderSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AccountLinkProviderSessionResult(
                $"launch-{request.SessionId:N}",
                $"session-{request.SessionId:N}",
                DateTime.UtcNow.AddMinutes(30)));
        }

        public Task<AccountLinkProviderExchangeResult> ExchangeSessionAsync(
            AccountLinkProviderExchangeRequest request,
            CancellationToken cancellationToken = default)
        {
            var suffix = request.TemporaryCode.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
            if (suffix.Length > 8)
            {
                suffix = suffix[..8];
            }

            var connectionReference = request.Mode == "update" && !string.IsNullOrWhiteSpace(request.ExistingConnectionReference)
                ? request.ExistingConnectionReference
                : $"item-{suffix}";

            return Task.FromResult(new AccountLinkProviderExchangeResult(
                connectionReference!,
                $"vault://connections/{connectionReference}",
                "Test Bank",
                $"ins-{suffix}",
                "Granted",
                DateTime.UtcNow,
                request.Mode == "update" ? "UpdateModeComplete" : "InitialSyncComplete",
                null,
                new List<AccountLinkProviderAccountResult>
                {
                    new(
                        $"acct-{suffix}-current",
                        "Current account",
                        "bank",
                        "current",
                        "USD",
                        "1234",
                        "Connected"),
                    new(
                        $"acct-{suffix}-savings",
                        "Savings account",
                        "bank",
                        "savings",
                        "USD",
                        "5678",
                        "Connected")
                }));
        }

        public Task<AccountLinkProviderExchangeResult> RefreshConnectionAsync(
            AccountLinkProviderRefreshRequest request,
            CancellationToken cancellationToken = default)
        {
            var suffix = request.ProviderConnectionReference.Replace("item-", string.Empty, StringComparison.Ordinal);
            if (suffix.Length > 8)
            {
                suffix = suffix[..8];
            }

            return Task.FromResult(new AccountLinkProviderExchangeResult(
                request.ProviderConnectionReference,
                request.SecretReference,
                "Test Bank",
                $"ins-{suffix}",
                "Granted",
                DateTime.UtcNow,
                "RefreshComplete",
                null,
                new List<AccountLinkProviderAccountResult>
                {
                    new(
                        $"acct-{suffix}-current",
                        "Current account",
                        "bank",
                        "current",
                        "USD",
                        "1234",
                        "Connected"),
                    new(
                        $"acct-{suffix}-savings",
                        "Savings account",
                        "bank",
                        "savings",
                        "USD",
                        "5678",
                        "Connected")
                }));
        }

        public Task DisconnectConnectionAsync(
            AccountLinkProviderDisconnectRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<AccountLinkProviderTransactionsSyncResult> SyncTransactionsAsync(
            AccountLinkProviderTransactionsSyncRequest request,
            CancellationToken cancellationToken = default)
        {
            var suffix = request.ProviderConnectionReference.Replace("item-", string.Empty, StringComparison.Ordinal);
            if (suffix.Length > 8)
            {
                suffix = suffix[..8];
            }

            return Task.FromResult(new AccountLinkProviderTransactionsSyncResult(
                $"cursor-{suffix}-1",
                DateTime.UtcNow,
                "TransactionsSyncComplete",
                null,
                new List<AccountLinkProviderTransactionResult>
                {
                    new(
                        $"txn-{suffix}-coffee",
                        $"acct-{suffix}-current",
                        DateTime.UtcNow.Date.AddDays(-1),
                        -6.40m,
                        "USD",
                        "Blue Bottle",
                        "Morning coffee",
                        "Food And Drink",
                        false),
                    new(
                        $"txn-{suffix}-groceries",
                        $"acct-{suffix}-current",
                        DateTime.UtcNow.Date.AddDays(-2),
                        -45.25m,
                        "USD",
                        "Fresh Market",
                        "Weekly groceries",
                        "Shops",
                        false)
                },
                []));
        }
    }

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    [Fact]
    public async Task CreateSessionAsync_Should_CreateReadySession_WhenProviderIsSupported()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new PersonalAccountLinkService(
            context,
            new TestTenantProvider(tenantId),
            new TestTenantContext { TenantId = tenantId },
            new TestCurrentUserProvider(userId),
            new[] { new FakeAccountLinkProviderGateway() });

        // Act
        var result = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));

        // Assert
        result.Provider.Should().Be("Plaid");
        result.Status.Should().Be("Ready");
        result.Mode.Should().Be("connect");
        result.LaunchToken.Should().StartWith("launch-");

        context.FinancialConnectionSessions.Should().ContainSingle(item => item.Id == result.AccountLinkSessionId);
    }

    [Fact]
    public async Task ExchangeSessionAsync_Should_CreateConnectionAndPersonalAccounts_WhenSessionIsValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new PersonalAccountLinkService(
            context,
            new TestTenantProvider(tenantId),
            new TestTenantContext { TenantId = tenantId },
            new TestCurrentUserProvider(userId),
            new[] { new FakeAccountLinkProviderGateway() });

        var session = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));

        // Act
        var result = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.AccountLinkSessionId, "sandbox123"));

        // Assert
        result.Should().NotBeNull();
        result!.Connection.Provider.Should().Be("Plaid");
        result.Connection.Accounts.Should().HaveCount(2);

        context.FinancialConnections.Should().ContainSingle();
        context.FinancialLinkedAccounts.Should().HaveCount(2);
        context.PersonalAccounts.Should().HaveCount(2);
        context.FinancialConnectionSessions.Single().Status.Should().Be("Exchanged");
    }

    [Fact]
    public async Task GetSummaryAsync_Should_ReturnManualAndLinkedAccounts_WhenBothExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        context.PersonalAccounts.Add(new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Cash wallet",
            AccountType = "CashWallet",
            Currency = "USD",
            Status = "Active",
            IsArchived = false,
            OpenedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new PersonalAccountLinkService(
            context,
            new TestTenantProvider(tenantId),
            new TestTenantContext { TenantId = tenantId },
            new TestCurrentUserProvider(userId),
            new[] { new FakeAccountLinkProviderGateway() });

        var session = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));
        await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.AccountLinkSessionId, "summary01"));

        // Act
        var result = await service.GetSummaryAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().ContainSingle(item => item.SourceType == "manual" && item.Name == "Cash wallet");
        result.Should().Contain(item => item.SourceType == "linked" && item.Provider == "Plaid");
    }

    [Fact]
    public async Task CreateSessionAsync_Should_TargetExistingConnection_WhenReconnectRequested()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new PersonalAccountLinkService(
            context,
            new TestTenantProvider(tenantId),
            new TestTenantContext { TenantId = tenantId },
            new TestCurrentUserProvider(userId),
            new[] { new FakeAccountLinkProviderGateway() });

        var initialSession = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));
        var initialExchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(initialSession.AccountLinkSessionId, "reconnect1"));

        // Act
        var updateSession = await service.CreateSessionAsync(
            new CreateAccountLinkSessionRequest(
                "Plaid",
                Mode: "update",
                ConnectionId: initialExchange!.Connection.ConnectionId));

        // Assert
        updateSession.Mode.Should().Be("update");
        updateSession.ConnectionId.Should().Be(initialExchange.Connection.ConnectionId);
    }

    [Fact]
    public async Task RefreshConnectionAsync_Should_UpdateExistingLinkedConnection_WhenConnectionExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new PersonalAccountLinkService(
            context,
            new TestTenantProvider(tenantId),
            new TestTenantContext { TenantId = tenantId },
            new TestCurrentUserProvider(userId),
            new[] { new FakeAccountLinkProviderGateway() });

        var session = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.AccountLinkSessionId, "refresh1"));

        // Act
        var refreshed = await service.RefreshConnectionAsync(exchange!.Connection.ConnectionId);

        // Assert
        refreshed.Should().NotBeNull();
        refreshed!.ConnectionId.Should().Be(exchange.Connection.ConnectionId);
        refreshed.LastSyncStatus.Should().Be("RefreshComplete");
    }

    [Fact]
    public async Task DisconnectConnectionAsync_Should_ArchiveLinkedAccounts_WhenConnectionExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new PersonalAccountLinkService(
            context,
            new TestTenantProvider(tenantId),
            new TestTenantContext { TenantId = tenantId },
            new TestCurrentUserProvider(userId),
            new[] { new FakeAccountLinkProviderGateway() });

        var session = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.AccountLinkSessionId, "disconnect1"));

        // Act
        var disconnected = await service.DisconnectConnectionAsync(exchange!.Connection.ConnectionId);

        // Assert
        disconnected.Should().NotBeNull();
        disconnected!.Status.Should().Be("Disconnected");
        disconnected.DisconnectedAt.Should().NotBeNull();
        context.PersonalAccounts.Should().OnlyContain(item => item.IsArchived);
    }

    [Fact]
    public async Task ProcessPlaidWebhookAsync_Should_MarkConnectionActionRequired_WhenPendingDisconnectReceived()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantContext = new TestTenantContext();
        var service = new PersonalAccountLinkService(
            context,
            new TestTenantProvider(tenantId),
            tenantContext,
            new TestCurrentUserProvider(userId),
            new[] { new FakeAccountLinkProviderGateway() });

        var session = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.AccountLinkSessionId, "pending99"));

        tenantContext.TenantId = null;
        tenantContext.ResolutionSource = null;

        // Act
        await service.ProcessPlaidWebhookAsync(new PlaidAccountLinkWebhookRequest
        {
            WebhookType = "ITEM",
            WebhookCode = "PENDING_DISCONNECT",
            ItemId = exchange!.Connection.ProviderConnectionReference
        });

        // Assert
        var connection = context.FinancialConnections.Single();
        connection.Status.Should().Be("ActionRequired");
        connection.ConsentStatus.Should().Be("ActionRequired");
        connection.LastSyncStatus.Should().Be("PENDING_DISCONNECT");

        context.FinancialLinkedAccounts.Should().OnlyContain(item => item.Status == "ActionRequired");
        context.FinancialWebhookEvents.Should().ContainSingle(item => item.ProviderEventCode == "PENDING_DISCONNECT");
    }

    [Fact]
    public async Task ProcessPlaidWebhookAsync_Should_DisconnectConnection_WhenPermissionRevokedReceived()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantContext = new TestTenantContext();
        var service = new PersonalAccountLinkService(
            context,
            new TestTenantProvider(tenantId),
            tenantContext,
            new TestCurrentUserProvider(userId),
            new[] { new FakeAccountLinkProviderGateway() });

        var session = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.AccountLinkSessionId, "revoked1"));

        tenantContext.TenantId = null;
        tenantContext.ResolutionSource = null;

        // Act
        await service.ProcessPlaidWebhookAsync(new PlaidAccountLinkWebhookRequest
        {
            WebhookType = "ITEM",
            WebhookCode = "USER_PERMISSION_REVOKED",
            ItemId = exchange!.Connection.ProviderConnectionReference
        });

        // Assert
        context.FinancialConnections.Single().Status.Should().Be("Disconnected");
        context.PersonalAccounts.Should().OnlyContain(item => item.IsArchived);
        context.FinancialWebhookEvents.Should().ContainSingle(item => item.ProviderEventCode == "USER_PERMISSION_REVOKED");
    }

    [Fact]
    public async Task SyncConnectionTransactionsAsync_Should_PersistLinkedTransactions_WhenProviderReturnsTransactions()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new PersonalAccountLinkService(
            context,
            new TestTenantProvider(tenantId),
            new TestTenantContext { TenantId = tenantId },
            new TestCurrentUserProvider(userId),
            new[] { new FakeAccountLinkProviderGateway() });

        var session = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.AccountLinkSessionId, "sync1234"));

        // Act
        var syncResult = await service.SyncConnectionTransactionsAsync(exchange!.Connection.ConnectionId);

        // Assert
        syncResult.Should().NotBeNull();
        syncResult!.TransactionsAdded.Should().Be(2);
        syncResult.TransactionsUpdated.Should().Be(0);
        syncResult.TransactionsRemoved.Should().Be(0);

        context.PersonalTransactions.Should().HaveCount(2);
        context.PersonalTransactions.Should().OnlyContain(item => item.SourceType == "linked_account_sync");
        context.PersonalTransactions.Should().Contain(item => item.Merchant == "Blue Bottle");
    }
}
