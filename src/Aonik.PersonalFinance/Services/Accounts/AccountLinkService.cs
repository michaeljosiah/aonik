using Aonik.PersonalFinance.Contracts.Models.Accounts;
using Aonik.PersonalFinance.Contracts.Services.Accounts;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Services.Accounts.Linking;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Finance.Categorization;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Storage;
using Aonik.SharedKernel.Modules;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.PersonalFinance.Services.Accounts;

/// <summary>
/// Tenant-scoped account-link orchestrator. Implements the public
/// <see cref="IAccountLinkService"/> contract by delegating to focused
/// helpers under <c>Linking/</c>:
/// <list type="bullet">
///   <item>Session handshake — <see cref="AccountLinkSessionCoordinator"/></item>
///   <item>Connection lifecycle — <see cref="AccountConnectionLifecycleManager"/></item>
///   <item>Transaction sync — <see cref="AccountTransactionSyncOrchestrator"/></item>
///   <item>Plaid webhooks — <see cref="PlaidAccountWebhookProcessor"/></item>
///   <item>Manual CRUD — <see cref="ManualAccountManager"/></item>
///   <item>Attachments — <see cref="TransactionAttachmentHandler"/></item>
/// </list>
/// The constructor signature is preserved so tests and DI registration
/// remain unchanged; the helpers are constructed inline.
/// </summary>
internal sealed class AccountLinkService : IAccountLinkService
{
    private readonly ITenantProvider _tenantProvider;
    private readonly AccountTransactionSyncOrchestrator _transactionSyncOrchestrator;
    private readonly AccountLinkSessionCoordinator _sessionCoordinator;
    private readonly AccountConnectionLifecycleManager _lifecycleManager;
    private readonly PlaidAccountWebhookProcessor _plaidWebhookProcessor;
    private readonly ManualAccountManager _manualAccountManager;
    private readonly TransactionAttachmentHandler _attachmentHandler;
    private readonly AccountTransactionCategoryManager _categoryManager;

    public AccountLinkService(
        PersonalFinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ITenantContext tenantContext,
        ICurrentUserProvider currentUserProvider,
        IEnumerable<IPersonalAccountLinkProviderGateway> providerGateways,
        AccountTransactionSyncOrchestrator transactionSyncOrchestrator,
        IAccountTransactionCategorizer categorizer,
        IChronicleCategoryMapper categoryMapper,
        IPartyAccountService partyAccountService,
        IPartyReader partyReader,
        IFileStore fileStore,
        IOptions<AccountConnectionSyncOptions> syncOptions,
        IModuleGate moduleGate,
        ILogger<AccountLinkService> logger)
    {
        _tenantProvider = tenantProvider;
        _transactionSyncOrchestrator = transactionSyncOrchestrator;

        var providerResolver = new AccountLinkProviderResolver(providerGateways);
        var syncApplicator = new AccountConnectionSyncApplicator(
            financeDbContext,
            partyAccountService,
            partyReader,
            syncOptions);

        _sessionCoordinator = new AccountLinkSessionCoordinator(
            financeDbContext,
            tenantProvider,
            currentUserProvider,
            providerResolver,
            syncApplicator,
            transactionSyncOrchestrator,
            logger);

        _lifecycleManager = new AccountConnectionLifecycleManager(
            financeDbContext,
            tenantProvider,
            currentUserProvider,
            providerResolver,
            syncApplicator);

        _plaidWebhookProcessor = new PlaidAccountWebhookProcessor(
            financeDbContext,
            tenantContext,
            moduleGate,
            syncOptions,
            logger);

        _manualAccountManager = new ManualAccountManager(
            financeDbContext,
            tenantProvider,
            partyAccountService,
            syncApplicator);

        _attachmentHandler = new TransactionAttachmentHandler(
            financeDbContext,
            tenantProvider,
            fileStore);

        _categoryManager = new AccountTransactionCategoryManager(
            financeDbContext,
            tenantProvider,
            currentUserProvider,
            categorizer,
            categoryMapper);
    }

    public Task<AccountLinkSessionResponse> CreateSessionAsync(
        CreateAccountLinkSessionRequest request,
        CancellationToken cancellationToken = default)
        => _sessionCoordinator.CreateSessionAsync(request, cancellationToken);

    public Task<AccountLinkExchangeResponse> ExchangeSessionAsync(
        ExchangeAccountLinkSessionRequest request,
        CancellationToken cancellationToken = default)
        => _sessionCoordinator.ExchangeSessionAsync(request, cancellationToken);

    public Task<IReadOnlyList<AccountConnectionResponse>> ListConnectionsAsync(
        bool includeDisconnected = false,
        CancellationToken cancellationToken = default)
        => _lifecycleManager.ListConnectionsAsync(includeDisconnected, cancellationToken);

    public Task<AccountConnectionResponse?> RefreshConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
        => _lifecycleManager.RefreshConnectionAsync(connectionId, cancellationToken);

    public Task<AccountConnectionResponse?> DisconnectConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
        => _lifecycleManager.DisconnectConnectionAsync(connectionId, cancellationToken);

    public Task<AccountTransactionSyncResponse?> SyncConnectionTransactionsAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        return _transactionSyncOrchestrator.SyncConnectionTransactionsAsync(
            tenantId,
            connectionId,
            "manual_sync",
            cancellationToken);
    }

    public Task<PagedResult<AccountTransactionResponse>> ListTransactionsAsync(
        ListAccountTransactionsRequest request,
        CancellationToken cancellationToken = default)
        => _manualAccountManager.ListTransactionsAsync(request, cancellationToken);

    public Task ProcessPlaidWebhookAsync(
        PlaidAccountWebhookRequest request,
        CancellationToken cancellationToken = default)
        => _plaidWebhookProcessor.ProcessAsync(request, cancellationToken);

    // ── Manual Account CRUD ──────────────────────────────────────

    public Task<AccountResponse> CreateAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken = default)
        => _manualAccountManager.CreateAccountAsync(request, cancellationToken);

    public Task<IReadOnlyList<AccountResponse>> ListAccountsAsync(
        CancellationToken cancellationToken = default)
        => _manualAccountManager.ListAccountsAsync(cancellationToken);

    // ── Manual Transaction CRUD ──────────────────────────────────

    public Task<AccountTransactionResponse> CreateTransactionAsync(
        CreateAccountTransactionRequest request,
        CancellationToken cancellationToken = default)
        => _manualAccountManager.CreateTransactionAsync(request, cancellationToken);

    // ── Transaction Attachments ──────────────────────────────────

    public Task<AccountTransactionAttachmentResponse> AddTransactionAttachmentAsync(
        Guid transactionId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
        => _attachmentHandler.AddAsync(transactionId, fileStream, fileName, contentType, cancellationToken);

    public Task<IReadOnlyList<AccountTransactionAttachmentResponse>> ListTransactionAttachmentsAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
        => _attachmentHandler.ListAsync(transactionId, cancellationToken);

    public Task DeleteTransactionAttachmentAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default)
        => _attachmentHandler.DeleteAsync(attachmentId, cancellationToken);

    // ── Auto-categorization (spec 028) ───────────────────────────

    public Task<AccountTransactionCategoryResult?> SetTransactionCategoryAsync(
        Guid transactionId,
        SetAccountTransactionCategoryRequest request,
        CancellationToken cancellationToken = default)
        => _categoryManager.SetCategoryAsync(transactionId, request, cancellationToken);

    public Task<bool> UnlockTransactionCategoryAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
        => _categoryManager.UnlockCategoryAsync(transactionId, cancellationToken);

    public Task<IReadOnlyList<MerchantCategoryResult>> ListMerchantCategoriesAsync(
        CancellationToken cancellationToken = default)
        => _categoryManager.ListMerchantCategoriesAsync(cancellationToken);

    public Task<bool> DeleteMerchantCategoryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => _categoryManager.DeleteMerchantCategoryAsync(id, cancellationToken);

    public Task<RecategorizeAccountTransactionsResult?> RecategorizeTransactionsAsync(
        Guid connectionId,
        RecategorizeAccountTransactionsRequest request,
        CancellationToken cancellationToken = default)
        => _categoryManager.RecategorizeAsync(connectionId, request, cancellationToken);
}
