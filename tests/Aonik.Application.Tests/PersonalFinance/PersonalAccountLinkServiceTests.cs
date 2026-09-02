using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.PersonalFinance.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
                BuildAccounts(suffix)));
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
                BuildAccounts(suffix)));
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
                        "eating_out",
                        null,
                        false),
                    new(
                        $"txn-{suffix}-groceries",
                        $"acct-{suffix}-current",
                        DateTime.UtcNow.Date.AddDays(-2),
                        -45.25m,
                        "USD",
                        "Fresh Market",
                        "Weekly groceries",
                        "groceries",
                        null,
                        false)
                },
                []));
        }

        private static IReadOnlyList<AccountLinkProviderAccountResult> BuildAccounts(string suffix)
        {
            var currentLast4 = suffix.Contains("mask", StringComparison.OrdinalIgnoreCase)
                ? "*****1234"
                : "1234";

            return new List<AccountLinkProviderAccountResult>
            {
                new(
                    $"acct-{suffix}-current",
                    "Current account",
                    "bank",
                    "current",
                    "USD",
                    currentLast4,
                    "Connected"),
                new(
                    $"acct-{suffix}-savings",
                    "Savings account",
                    "bank",
                    "savings",
                    "USD",
                    "5678",
                    "Connected")
            };
        }
    }

    private static PersonalFinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static PersonalAccountLinkService CreateService(
        PersonalFinanceDbContext context,
        Guid tenantId,
        Guid userId,
        TestTenantContext tenantContext,
        IPersonalAccountLinkProviderGateway? gateway = null,
        IModuleGate? moduleGate = null)
    {
        gateway ??= new FakeAccountLinkProviderGateway();

        var syncOptions = Microsoft.Extensions.Options.Options.Create(new FinancialConnectionSyncOptions
        {
            EnableRecurringSync = true,
            DefaultSyncIntervalMinutes = 60,
            WorkerPollIntervalSeconds = 30,
            BatchSize = 10,
            FailureRetryDelayMinutes = 5
        });

        var orchestrator = new FinancialConnectionTransactionSyncOrchestrator(
            context,
            tenantContext,
            new[] { gateway },
            syncOptions,
            NullLogger<FinancialConnectionTransactionSyncOrchestrator>.Instance,
            new NoOpGraphCacheInvalidator());

        return new PersonalAccountLinkService(
            context,
            new TestTenantProvider(tenantId),
            tenantContext,
            new TestCurrentUserProvider(userId),
            new[] { gateway },
            orchestrator,
            syncOptions,
            new NoOpGraphCacheInvalidator(),
            moduleGate ?? TestModuleGate.AllowAll,
            NullLogger<PersonalAccountLinkService>.Instance);
    }

    [Fact]
    public async Task CreateSessionAsync_Should_CreateReadySession_WhenProviderIsSupported()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var service = CreateService(context, tenantId, userId, tenantContext);

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
        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var service = CreateService(context, tenantId, userId, tenantContext);

        var session = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));

        // Act
        var result = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.AccountLinkSessionId, "sandbox123"));

        // Assert
        result.Should().NotBeNull();
        result!.Connection.Provider.Should().Be("Plaid");
        result.Connection.Accounts.Should().HaveCount(2);

        context.FinancialConnections.Should().ContainSingle();
        context.PersonalLinkedAccounts.Should().HaveCount(2);
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

        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var service = CreateService(context, tenantId, userId, tenantContext);

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
        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var service = CreateService(context, tenantId, userId, tenantContext);

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
        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var service = CreateService(context, tenantId, userId, tenantContext);

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
    public async Task RefreshConnectionAsync_Should_PreserveManualArchive_WhenPersonalAccountWasArchivedByUser()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var service = CreateService(context, tenantId, userId, tenantContext);

        var session = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.AccountLinkSessionId, "archive1"));

        var personalAccount = context.PersonalAccounts
            .Single(item => item.ExternalReference == "acct-archive1-current");
        var archivedAt = DateTime.UtcNow.AddDays(-3);
        personalAccount.IsArchived = true;
        personalAccount.Status = "Archived";
        personalAccount.ClosedAt = archivedAt;
        await context.SaveChangesAsync();

        // Act
        var refreshed = await service.RefreshConnectionAsync(exchange.Connection.ConnectionId);

        // Assert
        refreshed.Should().NotBeNull();
        var reloaded = context.PersonalAccounts.Single(item => item.Id == personalAccount.Id);
        reloaded.IsArchived.Should().BeTrue();
        reloaded.Status.Should().Be("Archived");
        reloaded.ClosedAt.Should().Be(archivedAt);
    }

    [Fact]
    public async Task ExchangeSessionAsync_Should_UseTrailingCharacters_WhenProviderReturnsMaskedLast4()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var service = CreateService(context, tenantId, userId, tenantContext);

        var session = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));

        // Act
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.AccountLinkSessionId, "mask1234"));

        // Assert
        exchange.Connection.Accounts.Should().Contain(item => item.Last4 == "1234");
        context.PersonalAccounts.Should().Contain(item => item.Last4 == "1234");
    }

    [Fact]
    public async Task DisconnectConnectionAsync_Should_ArchiveLinkedAccounts_WhenConnectionExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var service = CreateService(context, tenantId, userId, tenantContext);

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
        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var service = CreateService(context, tenantId, userId, tenantContext);

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
        connection.NextScheduledSyncAt.Should().BeNull();

        context.PersonalLinkedAccounts.Should().OnlyContain(item => item.Status == "ActionRequired");
        context.FinancialWebhookEvents.Should().ContainSingle(item => item.ProviderEventCode == "PENDING_DISCONNECT");
    }

    [Fact]
    public async Task ProcessPlaidWebhookAsync_Should_RefuseAndRecordNothing_When_OwningTenantHasPersonalFinanceOff()
    {
        // Spec 097 §11: the webhook is anonymous, so the HTTP module gate may have had no tenant to
        // check. The processor learns the owning tenant from the connection it locates and must re-check
        // there — before the connection is touched and before a webhook event is recorded.
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var setup = CreateService(context, tenantId, userId, tenantContext);

        var session = await setup.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await setup.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.AccountLinkSessionId, "pf-off-001"));

        tenantContext.TenantId = null;
        tenantContext.ResolutionSource = null;
        var gate = TestModuleGate.Denying(ModuleIds.PersonalFinance);
        var service = CreateService(context, tenantId, userId, tenantContext, moduleGate: gate);

        // Act
        var act = () => service.ProcessPlaidWebhookAsync(new PlaidAccountLinkWebhookRequest
        {
            WebhookType = "ITEM",
            WebhookCode = "PENDING_DISCONNECT",
            ItemId = exchange!.Connection.ProviderConnectionReference
        });

        // Assert
        (await act.Should().ThrowAsync<ModuleDisabledException>()).Which.ModuleId.Should().Be(ModuleIds.PersonalFinance);
        gate.Calls.Should().ContainSingle().Which.Should().Be((tenantId, ModuleIds.PersonalFinance),
            "the gate must be asked about the tenant the connection resolved to");

        var connection = context.FinancialConnections.AsNoTracking().Single();
        connection.Status.Should().NotBe("ActionRequired", "the connection must not be touched");
        connection.LastSyncStatus.Should().NotBe("PENDING_DISCONNECT");
        connection.LastWebhookReceivedAt.Should().BeNull();
        context.FinancialWebhookEvents.AsNoTracking().Should().BeEmpty("no webhook event is recorded for a module that is off");
        tenantContext.TenantId.Should().BeNull("the tenant override must be unwound even on refusal");
    }

    [Fact]
    public async Task RefreshConnectionAsync_Should_ThrowActionRequiredException_WhenReconnectIsRequired()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var service = CreateService(context, tenantId, userId, tenantContext);

        var session = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.AccountLinkSessionId, "pending-refresh"));

        tenantContext.TenantId = null;
        tenantContext.ResolutionSource = null;

        await service.ProcessPlaidWebhookAsync(new PlaidAccountLinkWebhookRequest
        {
            WebhookType = "ITEM",
            WebhookCode = "PENDING_DISCONNECT",
            ItemId = exchange.Connection.ProviderConnectionReference
        });

        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "Test";

        // Act
        var action = () => service.RefreshConnectionAsync(exchange.Connection.ConnectionId);

        // Assert
        var exception = await action.Should().ThrowAsync<AccountLinkActionRequiredException>();
        exception.Which.RequiredAction.Should().Be("reconnect");
        exception.Which.Provider.Should().Be("Plaid");
        exception.Which.ProviderErrorCode.Should().Be("PENDING_DISCONNECT");
    }

    [Fact]
    public async Task ProcessPlaidWebhookAsync_Should_DisconnectConnection_WhenPermissionRevokedReceived()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var service = CreateService(context, tenantId, userId, tenantContext);

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
    public async Task ProcessPlaidWebhookAsync_Should_QueueImmediateSync_WhenTransactionWebhookArrives()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var service = CreateService(context, tenantId, userId, tenantContext);

        var session = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.AccountLinkSessionId, "synchook1"));

        tenantContext.TenantId = null;
        tenantContext.ResolutionSource = null;

        // Act
        await service.ProcessPlaidWebhookAsync(new PlaidAccountLinkWebhookRequest
        {
            WebhookType = "TRANSACTIONS",
            WebhookCode = "SYNC_UPDATES_AVAILABLE",
            ItemId = exchange!.Connection.ProviderConnectionReference
        });

        // Assert
        var connection = context.FinancialConnections.Single();
        connection.LastSyncStatus.Should().Be("SYNC_UPDATES_AVAILABLE");
        connection.NextScheduledSyncAt.Should().NotBeNull();
        connection.NextScheduledSyncAt.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task SyncConnectionTransactionsAsync_Should_PersistLinkedTransactions_WhenProviderReturnsTransactions()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var service = CreateService(context, tenantId, userId, tenantContext);

        var session = await service.CreateSessionAsync(new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.AccountLinkSessionId, "sync1234"));

        // The initial sync during exchange already persisted transactions.
        // A subsequent sync sees them as existing and updates rather than adds.

        // Act
        var syncResult = await service.SyncConnectionTransactionsAsync(exchange!.Connection.ConnectionId);

        // Assert
        syncResult.Should().NotBeNull();
        syncResult!.TransactionsUpdated.Should().Be(2);
        syncResult.TransactionsAdded.Should().Be(0);
        syncResult.TransactionsRemoved.Should().Be(0);
        syncResult.NextCursor.Should().Be("cursor-sync1234-1");

        var connection = context.FinancialConnections.Single();
        connection.SyncCursor.Should().Be("cursor-sync1234-1");
        connection.NextScheduledSyncAt.Should().NotBeNull();

        context.PersonalTransactions.Should().HaveCount(2);
        context.PersonalTransactions.Should().OnlyContain(item => item.SourceType == "linked_account_sync");
        context.PersonalTransactions.Should().Contain(item => item.Merchant == "Blue Bottle");
    }
}
