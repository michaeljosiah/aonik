using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Finance.Contracts.Services.PersonalFinance;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class PlaidAccountLinkProviderGateway : IPersonalAccountLinkProviderGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly PlaidAccountLinkOptions _options;
    private readonly IDataProtector _protector;
    private readonly ILogger<PlaidAccountLinkProviderGateway> _logger;

    public PlaidAccountLinkProviderGateway(
        HttpClient httpClient,
        IOptions<PlaidAccountLinkOptions> options,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<PlaidAccountLinkProviderGateway> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _protector = dataProtectionProvider.CreateProtector("Aonik.Finance.PlaidAccountLinks");
        _logger = logger;
    }

    public string ProviderCode => "Plaid";

    public string DisplayName => "Plaid";

    public async Task<AccountLinkProviderSessionResult> CreateSessionAsync(
        AccountLinkProviderSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var androidPackageName = NormalizeOptionalAndroidPackageName(request.AndroidPackageName);
        var resolvedCountryCodes = DetermineCountryCodes(request.CountryCode);
        var resolvedProducts = DetermineProducts();
        var resolvedLanguage = NormalizeLanguage();
        var resolvedCustomization = TrimNullable(_options.LinkCustomizationName);

        _logger.LogInformation(
            "Plaid link/token/create — user={UserId}, mode={Mode}, countryCodes=[{CountryCodes}], products=[{Products}], "
            + "language={Language}, customization={Customization}, androidPkg={HasAndroidPkg}, requestedCountry={RequestedCountry}",
            request.UserId,
            request.Mode ?? "create",
            string.Join(",", resolvedCountryCodes),
            string.Join(",", resolvedProducts),
            resolvedLanguage,
            resolvedCustomization ?? "(none)",
            androidPackageName != null ? "provided" : "omitted",
            request.CountryCode ?? "(null — using configured default)");

        var plaidRequest = new PlaidLinkTokenCreateRequest
        {
            ClientId = _options.ClientId.Trim(),
            Secret = _options.Secret.Trim(),
            ClientName = string.IsNullOrWhiteSpace(request.ClientName)
                ? _options.ClientName.Trim()
                : request.ClientName.Trim(),
            Language = resolvedLanguage,
            CountryCodes = resolvedCountryCodes,
            Products = resolvedProducts,
            AndroidPackageName = androidPackageName,
            LinkCustomizationName = resolvedCustomization,
            User = new PlaidLinkTokenCreateUser
            {
                ClientUserId = request.UserId.ToString("N"),
                PhoneNumber = TrimNullable(request.PhoneNumber),
            }
        };

        // Per Plaid docs: when android_package_name is provided, redirect_uri must NOT be sent.
        // The Android SDK derives its own redirect URI from the registered package name.
        if (androidPackageName == null)
        {
            var redirectUri = TrimNullable(request.RedirectUri) ?? TrimNullable(_options.RedirectUri);
            if (redirectUri != null)
            {
                plaidRequest.RedirectUri = redirectUri;
            }
        }

        var webhookUrl = TrimNullable(_options.WebhookUrl);
        if (webhookUrl != null)
        {
            plaidRequest.Webhook = webhookUrl;
        }

        if (string.Equals(request.Mode, "update", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.ExistingSecretReference))
            {
                throw new InvalidOperationException("Existing secret reference is required for Plaid update mode.");
            }

            plaidRequest.AccessToken = UnprotectAccessToken(request.ExistingSecretReference);
        }

        var response = await PostAsync<PlaidLinkTokenCreateRequest, PlaidLinkTokenCreateResponse>(
            "link/token/create",
            plaidRequest,
            cancellationToken);

        return new AccountLinkProviderSessionResult(
            response.LinkToken,
            response.RequestId,
            response.Expiration);
    }

    public async Task<AccountLinkProviderExchangeResult> ExchangeSessionAsync(
        AccountLinkProviderExchangeRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        ValidatePublicTokenFormat(request.TemporaryCode);

        var exchangeResponse = await PostAsync<PlaidItemPublicTokenExchangeRequest, PlaidItemPublicTokenExchangeResponse>(
            "item/public_token/exchange",
            new PlaidItemPublicTokenExchangeRequest(
                _options.ClientId.Trim(),
                _options.Secret.Trim(),
                request.TemporaryCode.Trim()),
            cancellationToken);

        return await BuildConnectionResultAsync(
            exchangeResponse.AccessToken,
            request.Mode == "update" ? "UpdateModeComplete" : "InitialSyncComplete",
            null,
            cancellationToken);
    }

    public async Task<AccountLinkProviderExchangeResult> RefreshConnectionAsync(
        AccountLinkProviderRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var accessToken = UnprotectAccessToken(request.SecretReference);

        try
        {
            return await BuildConnectionResultAsync(
                accessToken,
                "RefreshComplete",
                request.ProviderConnectionReference,
                cancellationToken);
        }
        catch (PlaidAccountLinkProviderException ex) when (ex.RequiresReconnect)
        {
            _logger.LogWarning(
                ex,
                "Plaid refresh requires reconnect for connection {ConnectionId}.",
                request.ConnectionId);

            return new AccountLinkProviderExchangeResult(
                request.ProviderConnectionReference,
                request.SecretReference,
                string.Empty,
                null,
                "ActionRequired",
                null,
                ex.PlaidErrorCode ?? "RefreshFailed",
                ex.Message,
                []);
        }
    }

    public async Task DisconnectConnectionAsync(
        AccountLinkProviderDisconnectRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var accessToken = UnprotectAccessToken(request.SecretReference);

        try
        {
            await PostAsync<PlaidItemRemoveRequest, PlaidItemRemoveResponse>(
                "item/remove",
                new PlaidItemRemoveRequest(
                    _options.ClientId.Trim(),
                    _options.Secret.Trim(),
                    accessToken),
                cancellationToken);
        }
        catch (PlaidAccountLinkProviderException ex) when (IsSafeDisconnectFailure(ex))
        {
            _logger.LogWarning(
                ex,
                "Plaid item removal returned a recoverable error for connection {ConnectionId}; continuing with local disconnect.",
                request.ConnectionId);
        }
    }

    public async Task<AccountLinkProviderTransactionsSyncResult> SyncTransactionsAsync(
        AccountLinkProviderTransactionsSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var accessToken = UnprotectAccessToken(request.SecretReference);
        var cursor = TrimNullable(request.Cursor);
        var transactions = new Dictionary<string, AccountLinkProviderTransactionResult>(StringComparer.Ordinal);
        var removedTransactionReferences = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            while (true)
            {
                var response = await PostAsync<PlaidTransactionsSyncRequest, PlaidTransactionsSyncResponse>(
                    "transactions/sync",
                    new PlaidTransactionsSyncRequest(
                        _options.ClientId.Trim(),
                        _options.Secret.Trim(),
                        accessToken,
                        cursor),
                    cancellationToken);

                foreach (var transaction in response.Added)
                {
                    transactions[transaction.TransactionId] = MapTransaction(transaction);
                    removedTransactionReferences.Remove(transaction.TransactionId);
                }

                foreach (var transaction in response.Modified)
                {
                    transactions[transaction.TransactionId] = MapTransaction(transaction);
                    removedTransactionReferences.Remove(transaction.TransactionId);
                }

                foreach (var removed in response.Removed)
                {
                    transactions.Remove(removed.TransactionId);
                    removedTransactionReferences.Add(removed.TransactionId);
                }

                cursor = TrimNullable(response.NextCursor);
                if (!response.HasMore)
                {
                    break;
                }
            }

            return new AccountLinkProviderTransactionsSyncResult(
                cursor,
                DateTime.UtcNow,
                "TransactionsSyncComplete",
                null,
                transactions.Values.ToList(),
                removedTransactionReferences.ToList());
        }
        catch (PlaidAccountLinkProviderException ex) when (ex.RequiresReconnect)
        {
            _logger.LogWarning(
                ex,
                "Plaid transaction sync requires reconnect for connection {ConnectionId}.",
                request.ConnectionId);

            return new AccountLinkProviderTransactionsSyncResult(
                cursor,
                DateTime.UtcNow,
                ex.PlaidErrorCode ?? "TransactionsSyncFailed",
                ex.Message,
                [],
                []);
        }
    }

    private async Task<AccountLinkProviderExchangeResult> BuildConnectionResultAsync(
        string accessToken,
        string syncStatus,
        string? preferredConnectionReference,
        CancellationToken cancellationToken)
    {
        var itemResponse = await PostAsync<PlaidItemGetRequest, PlaidItemGetResponse>(
            "item/get",
            new PlaidItemGetRequest(
                _options.ClientId.Trim(),
                _options.Secret.Trim(),
                accessToken),
            cancellationToken);

        var accountsResponse = await PostAsync<PlaidAccountsGetRequest, PlaidAccountsGetResponse>(
            "accounts/get",
            new PlaidAccountsGetRequest(
                _options.ClientId.Trim(),
                _options.Secret.Trim(),
                accessToken),
            cancellationToken);

        PlaidInstitutionResponse? institutionResponse = null;
        if (!string.IsNullOrWhiteSpace(itemResponse.Item.InstitutionId))
        {
            institutionResponse = await TryGetInstitutionAsync(
                itemResponse.Item.InstitutionId!,
                cancellationToken);
        }

        var providerAccounts = accountsResponse.Accounts
            .Select(MapAccount)
            .ToList();

        var providerConnectionReference = string.IsNullOrWhiteSpace(preferredConnectionReference)
            ? itemResponse.Item.ItemId
            : preferredConnectionReference;

        return new AccountLinkProviderExchangeResult(
            providerConnectionReference,
            ProtectAccessToken(accessToken),
            institutionResponse?.Institution.Name ?? itemResponse.Item.InstitutionId ?? "Connected bank",
            institutionResponse?.Institution.InstitutionId ?? itemResponse.Item.InstitutionId,
            DetermineConsentStatus(itemResponse.Item),
            DateTime.UtcNow,
            syncStatus,
            null,
            providerAccounts);
    }

    private async Task<PlaidInstitutionResponse?> TryGetInstitutionAsync(
        string institutionId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await PostAsync<PlaidInstitutionByIdRequest, PlaidInstitutionResponse>(
                "institutions/get_by_id",
                new PlaidInstitutionByIdRequest(
                    _options.ClientId.Trim(),
                    _options.Secret.Trim(),
                    institutionId,
                    DetermineCountryCodes(null)),
                cancellationToken);
        }
        catch (PlaidAccountLinkProviderException ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to resolve Plaid institution metadata for {InstitutionId}.",
                institutionId);
            return null;
        }
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string relativePath,
        TRequest request,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        using var response = await _httpClient.PostAsJsonAsync(relativePath, request, JsonOptions, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw CreatePlaidException(response.StatusCode, payload, relativePath);
        }

        var typedResponse = JsonSerializer.Deserialize<TResponse>(payload, JsonOptions);
        if (typedResponse == null)
        {
            throw new InvalidOperationException($"Plaid returned an empty payload for '{relativePath}'.");
        }

        return typedResponse;
    }

    private PlaidAccountLinkProviderException CreatePlaidException(
        HttpStatusCode statusCode,
        string payload,
        string relativePath)
    {
        PlaidErrorResponse? errorResponse = null;

        try
        {
            errorResponse = JsonSerializer.Deserialize<PlaidErrorResponse>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            // Ignore parse failure and fall back to raw response.
        }

        var message = errorResponse?.ErrorMessage
            ?? errorResponse?.DisplayMessage
            ?? $"Plaid request to '{relativePath}' failed with status {(int)statusCode}.";

        return new PlaidAccountLinkProviderException(
            message,
            errorResponse?.ErrorCode,
            errorResponse?.ErrorType,
            statusCode);
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured())
        {
            throw new InvalidOperationException(
                "Plaid account-link gateway is not configured. Set Finance:PersonalFinance:Plaid:UseRealPlaidApi=true and provide ClientId/Secret.");
        }
    }

    private void ValidatePublicTokenFormat(string? publicToken)
    {
        var trimmed = TrimNullable(publicToken);
        if (trimmed == null)
        {
            throw new InvalidOperationException(
                "Public token is required to exchange a Plaid session.");
        }

        if (!trimmed.StartsWith("public-", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                "Received non-Plaid public token format: '{TokenPrefix}...'. " +
                "The backend is configured with UseRealPlaidApi=true but the client sent a simulated token. " +
                "Either enable the native Plaid launcher on the mobile client (accountLinkUseNativeLauncher=true) " +
                "or set Finance:PersonalFinance:Plaid:UseRealPlaidApi=false on the backend to use the simulated gateway.",
                trimmed.Length > 20 ? trimmed[..20] : trimmed);

            throw new InvalidOperationException(
                $"Invalid public token format. Plaid expects 'public-<environment>-<identifier>' " +
                $"but received '{(trimmed.Length > 30 ? trimmed[..30] + "..." : trimmed)}'. " +
                $"This typically means the mobile client is using the simulated launcher while the backend " +
                $"has UseRealPlaidApi=true. Either enable the native Plaid SDK on the client or disable " +
                $"real Plaid on the backend.");
        }
    }

    private static bool IsSafeDisconnectFailure(PlaidAccountLinkProviderException exception)
    {
        return string.Equals(exception.PlaidErrorCode, "INVALID_ACCESS_TOKEN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(exception.PlaidErrorCode, "ITEM_NOT_FOUND", StringComparison.OrdinalIgnoreCase)
            || string.Equals(exception.PlaidErrorCode, "ITEM_ALREADY_REMOVED", StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string> DetermineCountryCodes(string? requestedCountryCode)
    {
        if (!string.IsNullOrWhiteSpace(requestedCountryCode))
        {
            return [requestedCountryCode.Trim().ToUpperInvariant()];
        }

        var configured = _options.CountryCodes
            .Select(TrimNullable)
            .Where(value => value != null)
            .Select(value => value!.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return configured.Count == 0 ? ["US"] : configured;
    }

    private IReadOnlyList<string> DetermineProducts()
    {
        var configured = _options.Products
            .Select(TrimNullable)
            .Where(value => value != null)
            .Select(value => value!.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return configured.Count == 0 ? ["transactions"] : configured;
    }

    private string NormalizeLanguage()
    {
        var language = TrimNullable(_options.Language);
        return language ?? "en";
    }

    private static string? NormalizeOptionalAndroidPackageName(string? value)
    {
        return TrimNullable(value);
    }

    private string ProtectAccessToken(string accessToken)
    {
        return $"protected:{_protector.Protect(accessToken)}";
    }

    private string UnprotectAccessToken(string protectedValue)
    {
        var normalized = TrimNullable(protectedValue);
        if (normalized == null)
        {
            throw new InvalidOperationException("Plaid access token reference is missing.");
        }

        if (!normalized.StartsWith("protected:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Plaid access token reference is not in the expected protected format.");
        }

        return _protector.Unprotect(normalized[10..]);
    }

    private static string DetermineConsentStatus(PlaidItem item)
    {
        if (item.ConsentExpirationTime != null && item.ConsentExpirationTime <= DateTime.UtcNow)
        {
            return "ActionRequired";
        }

        return "Granted";
    }

    private static AccountLinkProviderAccountResult MapAccount(PlaidAccount account)
    {
        return new AccountLinkProviderAccountResult(
            account.AccountId,
            account.Name,
            MapAccountType(account.Type),
            MapAccountSubtype(account.Subtype),
            account.Balances?.IsoCurrencyCode?.Trim().ToUpperInvariant() ?? "USD",
            TrimNullable(account.Mask),
            "Connected");
    }

    private static AccountLinkProviderTransactionResult MapTransaction(PlaidTransaction transaction)
    {
        return new AccountLinkProviderTransactionResult(
            transaction.TransactionId,
            transaction.AccountId,
            ResolveOccurredAt(transaction),
            -transaction.Amount,
            transaction.IsoCurrencyCode?.Trim().ToUpperInvariant()
                ?? transaction.UnofficialCurrencyCode?.Trim().ToUpperInvariant()
                ?? "USD",
            TrimNullable(transaction.MerchantName),
            TrimNullable(transaction.Name),
            ResolveCategory(transaction.PersonalFinanceCategory),
            ResolveSubCategory(transaction.PersonalFinanceCategory),
            transaction.Pending);
    }

    private static DateTime ResolveOccurredAt(PlaidTransaction transaction)
    {
        if (transaction.Date.HasValue)
        {
            return transaction.Date.Value;
        }

        if (transaction.AuthorizedDate.HasValue)
        {
            return transaction.AuthorizedDate.Value;
        }

        return DateTime.UtcNow.Date;
    }

    private static string? ResolveCategory(PlaidPersonalFinanceCategory? category)
    {
        return TransactionCategoryReference.MapPlaidCategory(category?.Primary, category?.Detailed);
    }

    private static string? ResolveSubCategory(PlaidPersonalFinanceCategory? category)
    {
        var (_, subCategory) = TransactionCategoryReference.MapPlaidCategoryWithSubCategory(
            category?.Primary, category?.Detailed);
        return subCategory;
    }

    private static string MapAccountType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "depository" => "bank",
            "credit" => "credit",
            "loan" => "loan",
            "investment" => "investment",
            "other" => "other",
            _ => "bank"
        };
    }

    private static string? MapAccountSubtype(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "checking" => "current",
            "savings" => "savings",
            _ => TrimNullable(value)
        };
    }

    private static string? TrimNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class PlaidAccountLinkProviderException : Exception
    {
        public PlaidAccountLinkProviderException(
            string message,
            string? plaidErrorCode,
            string? plaidErrorType,
            HttpStatusCode statusCode)
            : base(message)
        {
            PlaidErrorCode = plaidErrorCode;
            PlaidErrorType = plaidErrorType;
            StatusCode = statusCode;
        }

        public string? PlaidErrorCode { get; }

        public string? PlaidErrorType { get; }

        public HttpStatusCode StatusCode { get; }

        public bool RequiresReconnect =>
            string.Equals(PlaidErrorCode, "ITEM_LOGIN_REQUIRED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(PlaidErrorCode, "PENDING_EXPIRATION", StringComparison.OrdinalIgnoreCase)
            || string.Equals(PlaidErrorCode, "PENDING_DISCONNECT", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class PlaidLinkTokenCreateRequest
    {
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = string.Empty;

        [JsonPropertyName("secret")]
        public string Secret { get; set; } = string.Empty;

        [JsonPropertyName("client_name")]
        public string ClientName { get; set; } = string.Empty;

        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("products")]
        public IReadOnlyList<string> Products { get; set; } = [];

        [JsonPropertyName("country_codes")]
        public IReadOnlyList<string> CountryCodes { get; set; } = [];

        [JsonPropertyName("user")]
        public PlaidLinkTokenCreateUser User { get; set; } = null!;

        [JsonPropertyName("android_package_name")]
        public string? AndroidPackageName { get; set; }

        [JsonPropertyName("redirect_uri")]
        public string? RedirectUri { get; set; }

        [JsonPropertyName("webhook")]
        public string? Webhook { get; set; }

        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("link_customization_name")]
        public string? LinkCustomizationName { get; set; }
    }

    private sealed class PlaidLinkTokenCreateUser
    {
        [JsonPropertyName("client_user_id")]
        public string ClientUserId { get; set; } = string.Empty;

        [JsonPropertyName("phone_number")]
        public string? PhoneNumber { get; set; }
    }

    private sealed class PlaidLinkTokenCreateResponse
    {
        [JsonPropertyName("link_token")]
        public string LinkToken { get; set; } = string.Empty;

        [JsonPropertyName("expiration")]
        public DateTime Expiration { get; set; }

        [JsonPropertyName("request_id")]
        public string? RequestId { get; set; }
    }

    private sealed record PlaidItemPublicTokenExchangeRequest(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("secret")] string Secret,
        [property: JsonPropertyName("public_token")] string PublicToken);

    private sealed class PlaidItemPublicTokenExchangeResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("item_id")]
        public string ItemId { get; set; } = string.Empty;
    }

    private sealed record PlaidItemGetRequest(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("secret")] string Secret,
        [property: JsonPropertyName("access_token")] string AccessToken);

    private sealed class PlaidItemGetResponse
    {
        [JsonPropertyName("item")]
        public PlaidItem Item { get; set; } = null!;
    }

    private sealed class PlaidItem
    {
        [JsonPropertyName("item_id")]
        public string ItemId { get; set; } = string.Empty;

        [JsonPropertyName("institution_id")]
        public string? InstitutionId { get; set; }

        [JsonPropertyName("consent_expiration_time")]
        public DateTime? ConsentExpirationTime { get; set; }
    }

    private sealed record PlaidAccountsGetRequest(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("secret")] string Secret,
        [property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record PlaidTransactionsSyncRequest(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("secret")] string Secret,
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("cursor")] string? Cursor);

    private sealed class PlaidAccountsGetResponse
    {
        [JsonPropertyName("accounts")]
        public List<PlaidAccount> Accounts { get; set; } = [];
    }

    private sealed class PlaidTransactionsSyncResponse
    {
        [JsonPropertyName("added")]
        public List<PlaidTransaction> Added { get; set; } = [];

        [JsonPropertyName("modified")]
        public List<PlaidTransaction> Modified { get; set; } = [];

        [JsonPropertyName("removed")]
        public List<PlaidRemovedTransaction> Removed { get; set; } = [];

        [JsonPropertyName("next_cursor")]
        public string? NextCursor { get; set; }

        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }
    }

    private sealed class PlaidAccount
    {
        [JsonPropertyName("account_id")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("subtype")]
        public string? Subtype { get; set; }

        [JsonPropertyName("mask")]
        public string? Mask { get; set; }

        [JsonPropertyName("balances")]
        public PlaidAccountBalances? Balances { get; set; }
    }

    private sealed class PlaidTransaction
    {
        [JsonPropertyName("transaction_id")]
        public string TransactionId { get; set; } = string.Empty;

        [JsonPropertyName("account_id")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("iso_currency_code")]
        public string? IsoCurrencyCode { get; set; }

        [JsonPropertyName("unofficial_currency_code")]
        public string? UnofficialCurrencyCode { get; set; }

        [JsonPropertyName("merchant_name")]
        public string? MerchantName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("date")]
        public DateTime? Date { get; set; }

        [JsonPropertyName("authorized_date")]
        public DateTime? AuthorizedDate { get; set; }

        [JsonPropertyName("pending")]
        public bool Pending { get; set; }

        [JsonPropertyName("personal_finance_category")]
        public PlaidPersonalFinanceCategory? PersonalFinanceCategory { get; set; }
    }

    private sealed class PlaidRemovedTransaction
    {
        [JsonPropertyName("transaction_id")]
        public string TransactionId { get; set; } = string.Empty;
    }

    private sealed class PlaidPersonalFinanceCategory
    {
        [JsonPropertyName("primary")]
        public string? Primary { get; set; }

        [JsonPropertyName("detailed")]
        public string? Detailed { get; set; }
    }

    private sealed class PlaidAccountBalances
    {
        [JsonPropertyName("iso_currency_code")]
        public string? IsoCurrencyCode { get; set; }
    }

    private sealed record PlaidInstitutionByIdRequest(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("secret")] string Secret,
        [property: JsonPropertyName("institution_id")] string InstitutionId,
        [property: JsonPropertyName("country_codes")] IReadOnlyList<string> CountryCodes);

    private sealed class PlaidInstitutionResponse
    {
        [JsonPropertyName("institution")]
        public PlaidInstitution Institution { get; set; } = null!;
    }

    private sealed class PlaidInstitution
    {
        [JsonPropertyName("institution_id")]
        public string InstitutionId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed record PlaidItemRemoveRequest(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("secret")] string Secret,
        [property: JsonPropertyName("access_token")] string AccessToken);

    private sealed class PlaidItemRemoveResponse
    {
        [JsonPropertyName("removed")]
        public bool Removed { get; set; }
    }

    private sealed class PlaidErrorResponse
    {
        [JsonPropertyName("error_type")]
        public string? ErrorType { get; set; }

        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("display_message")]
        public string? DisplayMessage { get; set; }
    }
}
