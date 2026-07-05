using Aonik.PersonalFinance.Contracts.Models.Accounts;
using Aonik.PersonalFinance.Contracts.Services.Accounts;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities.Accounts;
using Aonik.PersonalFinance.Services.Accounts;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aonik.Application.Tests.Finance.Accounts;

public class AccountLinkServiceTests
{
    private static readonly Guid TestTenantId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid TestPartyId = Guid.NewGuid();

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

    private sealed class FakePartyAccountService : IPartyAccountService
    {
        private readonly Guid _partyId;
        private readonly Dictionary<string, Guid> _accounts = new();
        private readonly List<PartyAccountResult> _createdAccounts = new();

        public FakePartyAccountService(Guid partyId)
        {
            _partyId = partyId;
        }

        public Task<Guid> FindOrCreatePartyAccountAsync(
            Guid tenantId,
            Guid partyId,
            string accountType,
            string maskedIdentifier,
            string? providerRef,
            CancellationToken cancellationToken = default)
        {
            var key = $"{tenantId}:{partyId}:{accountType}:{maskedIdentifier}";
            if (!_accounts.TryGetValue(key, out var id))
            {
                id = Guid.NewGuid();
                _accounts[key] = id;
            }
            return Task.FromResult(id);
        }

        public Task<PartyAccountResult> CreatePartyAccountAsync(
            Guid tenantId, Guid partyId, string accountType,
            string maskedIdentifier, string? providerRef, string verificationStatus,
            string? currency, string? country,
            string? metadataJson, CancellationToken cancellationToken = default)
        {
            var result = new PartyAccountResult(
                Guid.NewGuid(), tenantId, partyId, accountType,
                maskedIdentifier, providerRef, verificationStatus,
                currency, country,
                metadataJson ?? "{}", DateTime.UtcNow, null);
            _createdAccounts.Add(result);
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<PartyAccountResult>> ListPartyAccountsAsync(
            Guid tenantId, CancellationToken cancellationToken = default)
        {
            var results = _createdAccounts.Where(a => a.TenantId == tenantId).ToList();
            return Task.FromResult<IReadOnlyList<PartyAccountResult>>(results);
        }

        public Task<PartyAccountResult?> GetPartyAccountAsync(
            Guid tenantId, Guid accountId, CancellationToken cancellationToken = default)
        {
            var result = _createdAccounts.FirstOrDefault(a => a.Id == accountId && a.TenantId == tenantId);
            return Task.FromResult(result);
        }
    }

    private sealed class FakeFileStore : IFileStore
    {
        public Task<FileUploadResult> UploadAsync(Guid tenantId, Guid ownerEntityId, Stream fileStream,
            string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new FileUploadResult(
                "InMemory", null, $"test/{tenantId}/{ownerEntityId}/{fileName}",
                contentType, fileName, fileStream.Length, "fakehash"));
        }

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(null);

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public string GetUrl(string storageKey) => $"https://storage.test/{storageKey}";
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
            var suffix = request.TemporaryCode.Trim();
            if (suffix.Length > 8) suffix = suffix[..8];

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
                "InitialSyncComplete",
                null,
                new List<AccountLinkProviderAccountResult>
                {
                    new($"acct-{suffix}-current", "Current account", "bank", "current", "USD", "1234", "Connected"),
                    new($"acct-{suffix}-savings", "Savings account", "bank", "savings", "USD", "5678", "Connected")
                }));
        }

        public Task<AccountLinkProviderExchangeResult> RefreshConnectionAsync(
            AccountLinkProviderRefreshRequest request,
            CancellationToken cancellationToken = default)
        {
            var suffix = request.ProviderConnectionReference.Replace("item-", "", StringComparison.Ordinal);
            if (suffix.Length > 8) suffix = suffix[..8];

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
                    new($"acct-{suffix}-current", "Current account", "bank", "current", "USD", "1234", "Connected"),
                    new($"acct-{suffix}-savings", "Savings account", "bank", "savings", "USD", "5678", "Connected")
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
            var suffix = request.ProviderConnectionReference.Replace("item-", "", StringComparison.Ordinal);
            if (suffix.Length > 8) suffix = suffix[..8];

            return Task.FromResult(new AccountLinkProviderTransactionsSyncResult(
                $"cursor-{suffix}-1",
                DateTime.UtcNow,
                "TransactionsSyncComplete",
                null,
                new List<AccountLinkProviderTransactionResult>
                {
                    new($"txn-{suffix}-coffee", $"acct-{suffix}-current", DateTime.UtcNow.Date.AddDays(-1), -6.40m, "USD", "Blue Bottle", "Morning coffee", "eating_out", null, false),
                    new($"txn-{suffix}-groceries", $"acct-{suffix}-current", DateTime.UtcNow.Date.AddDays(-2), -45.25m, "USD", "Fresh Market", "Weekly groceries", "groceries", null, false)
                },
                []));
        }
    }

    private sealed class FakePartyReader : IPartyReader
    {
        private readonly Guid _tenantPartyId;

        public FakePartyReader(Guid tenantPartyId)
        {
            _tenantPartyId = tenantPartyId;
        }

        public Task<Guid?> GetTenantPartyIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(_tenantPartyId);

        public Task<IReadOnlyList<PartyHistoryItem>> GetByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> partyIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PartyHistoryItem>>([]);

        public Task<IReadOnlyList<PartyRelationshipHistoryItem>> GetRelationshipsForPartyAsync(
            Guid tenantId, Guid partyId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PartyRelationshipHistoryItem>>([]);

        public Task<bool> ExistsAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> HasActiveRelationshipBetweenAsync(
            Guid tenantId, Guid partyAId, Guid partyBId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private static PersonalFinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static AccountLinkService CreateService(
        PersonalFinanceDbContext context,
        Guid tenantId,
        Guid userId,
        TestTenantContext tenantContext,
        IPersonalAccountLinkProviderGateway? gateway = null)
    {
        gateway ??= new FakeAccountLinkProviderGateway();

        var syncOptions = Microsoft.Extensions.Options.Options.Create(new AccountConnectionSyncOptions
        {
            EnableRecurringSync = true,
            DefaultSyncIntervalMinutes = 60,
            FailureRetryDelayMinutes = 5
        });

        var categoryMapper = new Aonik.PersonalFinance.Services.PersonalFinance.ChronicleCategoryMapper();
        var categorizer = new AccountTransactionCategorizer(categoryMapper);
        var orchestrator = new AccountTransactionSyncOrchestrator(
            context,
            tenantContext,
            new[] { gateway },
            categorizer,
            syncOptions,
            NullLogger<AccountTransactionSyncOrchestrator>.Instance);

        return new AccountLinkService(
            context,
            new TestTenantProvider(tenantId),
            tenantContext,
            new TestCurrentUserProvider(userId),
            new[] { gateway },
            orchestrator,
            categorizer,
            categoryMapper,
            new FakePartyAccountService(TestPartyId),
            new FakePartyReader(TestPartyId),
            new FakeFileStore(),
            syncOptions,
            NullLogger<AccountLinkService>.Instance);
    }

    private static Task SeedTenantParty(PersonalFinanceDbContext context, Guid tenantId)
    {
        // The tenant's own party is now resolved through IPartyReader (a Platform
        // read model on PlatformDbContext), which the FakePartyReader supplies as
        // TestPartyId — so no Party row needs seeding into this context.
        _ = context;
        _ = tenantId;
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateSessionAsync_Should_CreateSessionAndReturnLaunchToken()
    {
        // Arrange
        await using var context = CreateDbContext(TestTenantId);
        var tenantContext = new TestTenantContext { TenantId = TestTenantId };
        var service = CreateService(context, TestTenantId, TestUserId, tenantContext);

        // Act
        var response = await service.CreateSessionAsync(
            new CreateAccountLinkSessionRequest("Plaid"));

        // Assert
        response.Should().NotBeNull();
        response.Provider.Should().Be("Plaid");
        response.LaunchToken.Should().StartWith("launch-");
        response.Mode.Should().Be("connect");
        response.Status.Should().Be("Ready");
        response.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task ExchangeSessionAsync_Should_CreateConnectionAndLinkedAccounts()
    {
        // Arrange
        await using var context = CreateDbContext(TestTenantId);
        await SeedTenantParty(context, TestTenantId);
        var tenantContext = new TestTenantContext { TenantId = TestTenantId };
        var service = CreateService(context, TestTenantId, TestUserId, tenantContext);

        var session = await service.CreateSessionAsync(
            new CreateAccountLinkSessionRequest("Plaid"));

        // Act
        var response = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.SessionId, "public-sandbox-test1234"));

        // Assert
        response.Should().NotBeNull();
        response.Connection.Should().NotBeNull();
        response.Connection.Provider.Should().Be("Plaid");
        response.Connection.InstitutionName.Should().Be("Test Bank");
        response.Connection.Status.Should().Be("Connected");
        response.Connection.LinkedAccounts.Should().HaveCount(2);
        response.Connection.LinkedAccounts[0].Name.Should().Be("Current account");
        response.Connection.LinkedAccounts[1].Name.Should().Be("Savings account");
    }

    [Fact]
    public async Task ExchangeSessionAsync_Should_ThrowWhenSessionExpired()
    {
        // Arrange
        await using var context = CreateDbContext(TestTenantId);
        var tenantContext = new TestTenantContext { TenantId = TestTenantId };
        var service = CreateService(context, TestTenantId, TestUserId, tenantContext);

        var session = await service.CreateSessionAsync(
            new CreateAccountLinkSessionRequest("Plaid"));

        // Manually expire the session
        var sessionEntity = await context.AccountConnectionSessions
            .FirstAsync(s => s.Id == session.SessionId);
        sessionEntity.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        await context.SaveChangesAsync();

        // Act
        var act = () => service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.SessionId, "public-sandbox-test1234"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public async Task ExchangeSessionAsync_Should_ThrowWhenSessionAlreadyExchanged()
    {
        // Arrange
        await using var context = CreateDbContext(TestTenantId);
        await SeedTenantParty(context, TestTenantId);
        var tenantContext = new TestTenantContext { TenantId = TestTenantId };
        var service = CreateService(context, TestTenantId, TestUserId, tenantContext);

        var session = await service.CreateSessionAsync(
            new CreateAccountLinkSessionRequest("Plaid"));
        await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.SessionId, "public-sandbox-test1234"));

        // Act
        var act = () => service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.SessionId, "public-sandbox-test1234"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been exchanged*");
    }

    [Fact]
    public async Task ListConnectionsAsync_Should_ReturnActiveConnections()
    {
        // Arrange
        await using var context = CreateDbContext(TestTenantId);
        await SeedTenantParty(context, TestTenantId);
        var tenantContext = new TestTenantContext { TenantId = TestTenantId };
        var service = CreateService(context, TestTenantId, TestUserId, tenantContext);

        var session = await service.CreateSessionAsync(
            new CreateAccountLinkSessionRequest("Plaid"));
        await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.SessionId, "public-sandbox-test1234"));

        // Act
        var connections = await service.ListConnectionsAsync();

        // Assert
        connections.Should().HaveCount(1);
        connections[0].Status.Should().Be("Connected");
        connections[0].LinkedAccounts.Should().HaveCount(2);
    }

    [Fact]
    public async Task DisconnectConnectionAsync_Should_MarkDisconnected()
    {
        // Arrange
        await using var context = CreateDbContext(TestTenantId);
        await SeedTenantParty(context, TestTenantId);
        var tenantContext = new TestTenantContext { TenantId = TestTenantId };
        var service = CreateService(context, TestTenantId, TestUserId, tenantContext);

        var session = await service.CreateSessionAsync(
            new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.SessionId, "public-sandbox-test1234"));
        var connectionId = exchange.Connection.ConnectionId;

        // Act
        var result = await service.DisconnectConnectionAsync(connectionId);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("Disconnected");
        result.LinkedAccounts.Should().AllSatisfy(la => la.Status.Should().Be("Archived"));
    }

    [Fact]
    public async Task SyncConnectionTransactionsAsync_Should_CreateTransactionsWithUnmatchedStatus()
    {
        // Arrange
        await using var context = CreateDbContext(TestTenantId);
        await SeedTenantParty(context, TestTenantId);
        var tenantContext = new TestTenantContext { TenantId = TestTenantId };
        var service = CreateService(context, TestTenantId, TestUserId, tenantContext);

        var session = await service.CreateSessionAsync(
            new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.SessionId, "public-sandbox-test1234"));

        // Act — the initial sync during exchange already created transactions,
        // so a second sync will update (not add) them. Either way, verify
        // transactions exist with correct status.
        var syncResult = await service.SyncConnectionTransactionsAsync(exchange.Connection.ConnectionId);

        // Assert
        syncResult.Should().NotBeNull();
        (syncResult!.TransactionsAdded + syncResult.TransactionsUpdated).Should().BeGreaterThan(0);

        var transactions = await context.AccountTransactions
            .Where(t => t.TenantId == TestTenantId)
            .ToListAsync();

        transactions.Should().NotBeEmpty();
        transactions.Should().AllSatisfy(t => t.ReconciliationStatus.Should().Be("Unmatched"));
    }

    [Fact]
    public async Task ListTransactionsAsync_Should_ReturnPagedTransactions()
    {
        // Arrange
        await using var context = CreateDbContext(TestTenantId);
        await SeedTenantParty(context, TestTenantId);
        var tenantContext = new TestTenantContext { TenantId = TestTenantId };
        var service = CreateService(context, TestTenantId, TestUserId, tenantContext);

        var session = await service.CreateSessionAsync(
            new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.SessionId, "public-sandbox-test1234"));
        await service.SyncConnectionTransactionsAsync(exchange.Connection.ConnectionId);

        // Act
        var result = await service.ListTransactionsAsync(
            new ListAccountTransactionsRequest(
                ConnectionId: exchange.Connection.ConnectionId));

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        result.Items.Should().AllSatisfy(t => t.ReconciliationStatus.Should().Be("Unmatched"));
    }

    // ── Manual Account CRUD Tests ────────────────────────────────

    [Fact]
    public async Task CreateAccountAsync_Should_CreateAccount()
    {
        // Arrange
        await using var context = CreateDbContext(TestTenantId);
        await SeedTenantParty(context, TestTenantId);
        var tenantContext = new TestTenantContext { TenantId = TestTenantId };
        var service = CreateService(context, TestTenantId, TestUserId, tenantContext);

        // Act
        var response = await service.CreateAccountAsync(
            new CreateAccountRequest("GTBank Naira", "BankAccount", "NGN", null, "GTBank", "1234", null));

        // Assert
        response.Should().NotBeNull();
        response.AccountType.Should().Be("BankAccount");
        response.VerificationStatus.Should().Be("Manual");
    }

    [Fact]
    public async Task ListAccountsAsync_Should_ReturnAllTenantAccounts()
    {
        // Arrange
        await using var context = CreateDbContext(TestTenantId);
        await SeedTenantParty(context, TestTenantId);
        var tenantContext = new TestTenantContext { TenantId = TestTenantId };
        var service = CreateService(context, TestTenantId, TestUserId, tenantContext);

        await service.CreateAccountAsync(
            new CreateAccountRequest("Account A", "BankAccount", "USD", null, null, null, null));
        await service.CreateAccountAsync(
            new CreateAccountRequest("Account B", "CreditCard", "GBP", null, null, null, null));

        // Act
        var accounts = await service.ListAccountsAsync();

        // Assert
        accounts.Should().HaveCount(2);
    }

    // ── Manual Transaction Tests ─────────────────────────────────

    [Fact]
    public async Task CreateTransactionAsync_Should_CreateManualTransaction()
    {
        // Arrange
        await using var context = CreateDbContext(TestTenantId);
        await SeedTenantParty(context, TestTenantId);
        var tenantContext = new TestTenantContext { TenantId = TestTenantId };
        var service = CreateService(context, TestTenantId, TestUserId, tenantContext);

        var account = await service.CreateAccountAsync(
            new CreateAccountRequest("Test Account", "BankAccount", "USD", null, null, null, null));

        // Act
        var transaction = await service.CreateTransactionAsync(
            new CreateAccountTransactionRequest(
                account.AccountId,
                DateTime.UtcNow.Date,
                -500.00m,
                "USD",
                "Office Supplies Inc",
                "Monthly supplies",
                "REF-001",
                "Expenses",
                null));

        // Assert
        transaction.Should().NotBeNull();
        transaction.Amount.Should().Be(-500.00m);
        transaction.Counterparty.Should().Be("Office Supplies Inc");
        transaction.ReconciliationStatus.Should().Be("Unmatched");
        transaction.AccountConnectionId.Should().BeNull();
    }

    [Fact]
    public async Task CreateTransactionAsync_Should_ThrowWhenAccountNotFound()
    {
        // Arrange
        await using var context = CreateDbContext(TestTenantId);
        await SeedTenantParty(context, TestTenantId);
        var tenantContext = new TestTenantContext { TenantId = TestTenantId };
        var service = CreateService(context, TestTenantId, TestUserId, tenantContext);

        // Act
        var act = () => service.CreateTransactionAsync(
            new CreateAccountTransactionRequest(
                Guid.NewGuid(),
                DateTime.UtcNow.Date,
                -100m,
                "USD",
                null, null, null, null, null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ── Attachment Tests ─────────────────────────────────────────

    [Fact]
    public async Task AddTransactionAttachmentAsync_Should_UploadFileAndCreateRecord()
    {
        // Arrange
        await using var context = CreateDbContext(TestTenantId);
        await SeedTenantParty(context, TestTenantId);
        var tenantContext = new TestTenantContext { TenantId = TestTenantId };
        var service = CreateService(context, TestTenantId, TestUserId, tenantContext);

        // Create a transaction to attach to (via Plaid flow for simplicity)
        var session = await service.CreateSessionAsync(
            new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.SessionId, "public-sandbox-attach"));

        var transactions = await context.AccountTransactions
            .Where(t => t.TenantId == TestTenantId)
            .ToListAsync();

        transactions.Should().NotBeEmpty("initial sync should create transactions");
        var transactionId = transactions.First().Id;

        // Act
        using var stream = new MemoryStream("test file content"u8.ToArray());
        var attachment = await service.AddTransactionAttachmentAsync(
            transactionId, stream, "receipt.pdf", "application/pdf");

        // Assert
        attachment.Should().NotBeNull();
        attachment.FileName.Should().Be("receipt.pdf");
        attachment.ContentType.Should().Be("application/pdf");
        attachment.Url.Should().Contain("receipt.pdf");
    }

    [Fact]
    public async Task DeleteTransactionAttachmentAsync_Should_RemoveRecord()
    {
        // Arrange
        await using var context = CreateDbContext(TestTenantId);
        await SeedTenantParty(context, TestTenantId);
        var tenantContext = new TestTenantContext { TenantId = TestTenantId };
        var service = CreateService(context, TestTenantId, TestUserId, tenantContext);

        var session = await service.CreateSessionAsync(
            new CreateAccountLinkSessionRequest("Plaid"));
        var exchange = await service.ExchangeSessionAsync(
            new ExchangeAccountLinkSessionRequest(session.SessionId, "public-sandbox-del"));

        var transactions = await context.AccountTransactions
            .Where(t => t.TenantId == TestTenantId)
            .ToListAsync();
        var transactionId = transactions.First().Id;

        using var stream = new MemoryStream("test content"u8.ToArray());
        var attachment = await service.AddTransactionAttachmentAsync(
            transactionId, stream, "receipt.pdf", "application/pdf");

        // Act
        await service.DeleteTransactionAttachmentAsync(attachment.AttachmentId);

        // Assert
        var remaining = await service.ListTransactionAttachmentsAsync(transactionId);
        remaining.Should().BeEmpty();
    }
}
