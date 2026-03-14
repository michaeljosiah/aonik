using System.Net;
using System.Net.Http.Json;
using System.Text;

using FluentAssertions;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Platform.Contracts.Api.PersonalFinance;

namespace Aonik.Api.Tests;

public class PersonalFinanceEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PersonalFinanceEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PersonalAccounts_CreateAndList_ReturnsCreatedAccount()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser"));
        var request = new CreatePersonalAccountRequest(
            "Main Wallet",
            "Bank",
            "usd",
            "Acme Bank",
            "ACME-REF",
            "Current",
            "1234");

        // Act
        var createResponse = await client.PostAsJsonAsync("/personal-finance/accounts", request);
        var listResponse = await client.GetAsync("/personal-finance/accounts");

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<PersonalAccountResponse>();
        created.Should().NotBeNull();
        created!.Currency.Should().Be("USD");

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listed = await listResponse.Content.ReadFromJsonAsync<List<PersonalAccountResponse>>();
        listed.Should().NotBeNull();
        listed!.Should().ContainSingle(item => item.PersonalAccountId == created.PersonalAccountId);
    }

    [Fact]
    public async Task SetupProfile_SaveGetAndClear_RoundTripsThroughBackend()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create()
                .WithRoles("PersonalUser")
                .WithPermissions("Settings.Read", "Settings.Write"));

        var request = new PayaboSetupProfileRequest(
            new[] { "trackMoney", "saveForGoals" },
            new[] { "ukBank", "cashManual" },
            "skipForNow",
            new[] { "rentOrMortgage" },
            "parents",
            new[] { "saveMore", "buildEmergencyFund" },
            true);

        // Act
        var emptyResponse = await client.GetAsync("/personal-finance/setup-profile");
        var saveResponse = await client.PutAsJsonAsync("/personal-finance/setup-profile", request);
        var saved = await saveResponse.Content.ReadFromJsonAsync<PayaboSetupProfileResponse>();

        var getResponse = await client.GetAsync("/personal-finance/setup-profile");
        var fetched = await getResponse.Content.ReadFromJsonAsync<PayaboSetupProfileResponse>();

        var clearResponse = await client.DeleteAsync("/personal-finance/setup-profile");
        var cleared = await clearResponse.Content.ReadFromJsonAsync<ClearPayaboSetupProfileResponse>();

        var afterClearResponse = await client.GetAsync("/personal-finance/setup-profile");
        var afterClear = await afterClearResponse.Content.ReadFromJsonAsync<PayaboSetupProfileResponse>();

        // Assert
        emptyResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        saveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        saved.Should().NotBeNull();
        saved!.Completed.Should().BeTrue();
        saved.SelectedUseCases.Should().ContainInOrder("trackMoney", "saveForGoals");
        saved.ConnectChoice.Should().Be("skipForNow");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        fetched.Should().NotBeNull();
        fetched!.FinancialGoals.Should().Contain(new[] { "saveMore", "buildEmergencyFund" });
        fetched.SupportType.Should().Be("parents");

        clearResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        cleared.Should().NotBeNull();
        cleared!.Status.Should().Be("ok");

        afterClearResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        afterClear.Should().NotBeNull();
        afterClear!.Completed.Should().BeFalse();
        afterClear.SelectedUseCases.Should().BeEmpty();
        afterClear.AccountSourceTypes.Should().BeEmpty();
    }

    [Fact]
    public async Task AccountLinks_CreateSessionExchangeAndSummary_ReturnLinkedAccounts()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser"));
        var sessionRequest = new CreateAccountLinkSessionRequest("Plaid");

        // Act
        var sessionResponse = await client.PostAsJsonAsync("/personal-finance/account-links/sessions", sessionRequest);
        var session = await sessionResponse.Content.ReadFromJsonAsync<AccountLinkSessionResponse>();

        var exchangeResponse = await client.PostAsJsonAsync(
            "/personal-finance/account-links/exchanges",
            new ExchangeAccountLinkSessionRequest(session!.AccountLinkSessionId, "sandbox-public-token-001"));
        var exchanged = await exchangeResponse.Content.ReadFromJsonAsync<AccountLinkExchangeResponse>();

        var listResponse = await client.GetAsync("/personal-finance/account-links");
        var listed = await listResponse.Content.ReadFromJsonAsync<List<AccountLinkConnectionResponse>>();

        var summaryResponse = await client.GetAsync("/personal-finance/account-links/summary");
        var summary = await summaryResponse.Content.ReadFromJsonAsync<List<AccountLinkSummaryItemResponse>>();

        // Assert
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        session.Should().NotBeNull();
        session!.Provider.Should().Be("Plaid");
        session.Status.Should().Be("Ready");

        exchangeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        exchanged.Should().NotBeNull();
        exchanged!.Connection.Accounts.Should().HaveCount(2);
        exchanged.Connection.InstitutionName.Should().Be("Plaid Sandbox Bank");

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listed.Should().NotBeNull();
        listed!.Should().ContainSingle(item => item.ConnectionId == exchanged.Connection.ConnectionId);

        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        summary.Should().NotBeNull();
        summary!.Should().Contain(item =>
            item.ConnectionId == exchanged.Connection.ConnectionId
            && item.SourceType == "linked"
            && item.Provider == "Plaid");
    }

    [Fact]
    public async Task AccountLinks_ReconnectRefreshAndDisconnect_UpdateConnectionState()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser"));

        var sessionResponse = await client.PostAsJsonAsync(
            "/personal-finance/account-links/sessions",
            new CreateAccountLinkSessionRequest("Plaid"));
        var session = await sessionResponse.Content.ReadFromJsonAsync<AccountLinkSessionResponse>();

        var exchangeResponse = await client.PostAsJsonAsync(
            "/personal-finance/account-links/exchanges",
            new ExchangeAccountLinkSessionRequest(session!.AccountLinkSessionId, "sandbox-public-token-002"));
        var exchanged = await exchangeResponse.Content.ReadFromJsonAsync<AccountLinkExchangeResponse>();

        // Act
        var refreshResponse = await client.PostAsync(
            $"/personal-finance/account-links/{exchanged!.Connection.ConnectionId}/refresh",
            null);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<AccountLinkActionResponse>();

        var reconnectSessionResponse = await client.PostAsJsonAsync(
            "/personal-finance/account-links/sessions",
            new CreateAccountLinkSessionRequest(
                "Plaid",
                Mode: "update",
                ConnectionId: exchanged.Connection.ConnectionId));
        var reconnectSession = await reconnectSessionResponse.Content.ReadFromJsonAsync<AccountLinkSessionResponse>();

        var reconnectExchangeResponse = await client.PostAsJsonAsync(
            "/personal-finance/account-links/exchanges",
            new ExchangeAccountLinkSessionRequest(reconnectSession!.AccountLinkSessionId, "sandbox-public-token-003"));
        var reconnected = await reconnectExchangeResponse.Content.ReadFromJsonAsync<AccountLinkExchangeResponse>();

        var disconnectResponse = await client.PostAsync(
            $"/personal-finance/account-links/{exchanged.Connection.ConnectionId}/disconnect",
            null);
        var disconnected = await disconnectResponse.Content.ReadFromJsonAsync<AccountLinkActionResponse>();

        var summaryResponse = await client.GetAsync("/personal-finance/account-links/summary");
        var summary = await summaryResponse.Content.ReadFromJsonAsync<List<AccountLinkSummaryItemResponse>>();

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        refreshed.Should().NotBeNull();
        refreshed!.Action.Should().Be("refresh");
        refreshed.Connection.LastSyncStatus.Should().Be("RefreshComplete");

        reconnectSessionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        reconnectSession.Should().NotBeNull();
        reconnectSession!.ConnectionId.Should().Be(exchanged.Connection.ConnectionId);
        reconnectSession.Mode.Should().Be("update");

        reconnectExchangeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        reconnected.Should().NotBeNull();
        reconnected!.Connection.ConnectionId.Should().Be(exchanged.Connection.ConnectionId);

        disconnectResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        disconnected.Should().NotBeNull();
        disconnected!.Action.Should().Be("disconnect");
        disconnected.Connection.Status.Should().Be("Disconnected");

        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        summary.Should().NotBeNull();
        summary!.Should().BeEmpty();
    }

    [Fact]
    public async Task AccountLinks_PlaidWebhook_MarksSummaryAsActionRequired()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser"));

        var sessionResponse = await client.PostAsJsonAsync(
            "/personal-finance/account-links/sessions",
            new CreateAccountLinkSessionRequest("Plaid"));
        var session = await sessionResponse.Content.ReadFromJsonAsync<AccountLinkSessionResponse>();

        var exchangeResponse = await client.PostAsJsonAsync(
            "/personal-finance/account-links/exchanges",
            new ExchangeAccountLinkSessionRequest(session!.AccountLinkSessionId, "sandbox-public-token-004"));
        var exchanged = await exchangeResponse.Content.ReadFromJsonAsync<AccountLinkExchangeResponse>();

        // Act
        var webhookResponse = await client.PostAsJsonAsync(
            "/personal-finance/account-links/webhooks/plaid",
            new PlaidAccountLinkWebhookRequest
            {
                WebhookType = "ITEM",
                WebhookCode = "PENDING_DISCONNECT",
                ItemId = exchanged!.Connection.ProviderConnectionReference
            });
        var webhookAck = await webhookResponse.Content.ReadFromJsonAsync<AccountLinkWebhookResponse>();

        var summaryResponse = await client.GetAsync("/personal-finance/account-links/summary");
        var summary = await summaryResponse.Content.ReadFromJsonAsync<List<AccountLinkSummaryItemResponse>>();

        // Assert
        webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        webhookAck.Should().NotBeNull();
        webhookAck!.Status.Should().Be("accepted");

        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        summary.Should().NotBeNull();
        summary!.Should().OnlyContain(item => item.Status == "ActionRequired");
        summary.Should().OnlyContain(item => item.LastSyncStatus == "PENDING_DISCONNECT");
    }

    [Fact]
    public async Task AccountLinks_TransactionSync_PersistsLinkedTransactions()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser"));

        var sessionResponse = await client.PostAsJsonAsync(
            "/personal-finance/account-links/sessions",
            new CreateAccountLinkSessionRequest("Plaid"));
        var session = await sessionResponse.Content.ReadFromJsonAsync<AccountLinkSessionResponse>();

        var exchangeResponse = await client.PostAsJsonAsync(
            "/personal-finance/account-links/exchanges",
            new ExchangeAccountLinkSessionRequest(session!.AccountLinkSessionId, "sandbox-public-token-005"));
        var exchanged = await exchangeResponse.Content.ReadFromJsonAsync<AccountLinkExchangeResponse>();

        // Act
        var syncResponse = await client.PostAsync(
            $"/personal-finance/account-links/{exchanged!.Connection.ConnectionId}/transactions/sync",
            null);
        var synced = await syncResponse.Content.ReadFromJsonAsync<AccountLinkTransactionSyncResponse>();

        var transactionsResponse = await client.GetAsync("/personal-finance/transactions");
        var transactions = await transactionsResponse.Content.ReadFromJsonAsync<List<PersonalTransactionResponse>>();

        // Assert
        syncResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        synced.Should().NotBeNull();
        synced!.TransactionsAdded.Should().Be(2);
        synced.SyncStatus.Should().Be("TransactionsSyncComplete");

        transactionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        transactions.Should().NotBeNull();
        transactions!.Should().Contain(item => item.Merchant == "Blue Bottle");
        transactions.Should().Contain(item => item.Merchant == "Fresh Market");
    }

    [Fact]
    public async Task StatementImport_UploadAndApply_CreatesImportAndAppliesRows()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser"));
        var account = await CreateAccountAsync(client, "Primary Account");

        var csv = "date,amount,description,merchant,currency\n2026-01-10,-20.50,Coffee,Blue Bottle,USD\n";
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(account.PersonalAccountId.ToString()), "personalAccountId");
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "files", "statement.csv");

        // Act
        var uploadResponse = await client.PostAsync("/personal-finance/imports/statements", content);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<StatementImportResponse>();

        var applyResponse = await client.PostAsync($"/personal-finance/imports/statements/{uploaded!.StatementImportId}/apply", null);
        var applied = await applyResponse.Content.ReadFromJsonAsync<StatementImportApplyResponse>();

        var transactionsResponse = await client.GetAsync("/personal-finance/transactions");
        var transactions = await transactionsResponse.Content.ReadFromJsonAsync<List<PersonalTransactionResponse>>();

        // Assert
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        uploaded.Should().NotBeNull();
        uploaded!.RowsTotal.Should().Be(1);

        applyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        applied.Should().NotBeNull();
        applied!.RowsImported.Should().Be(1);

        transactionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        transactions.Should().NotBeNull();
        transactions!.Should().ContainSingle(item => item.Description == "Coffee");
    }

    [Fact]
    public async Task ClassificationReview_OverrideWithRule_CreatesRuleAndClearsQueueItem()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser"));
        var account = await CreateAccountAsync(client, "Classification Account");

        var createTransaction = new CreateManualPersonalTransactionRequest(
            account.PersonalAccountId,
            DateTime.UtcNow,
            -45.20m,
            "USD",
            "SuperMart",
            "Groceries",
            null,
            null,
            null);

        var transactionResponse = await client.PostAsJsonAsync("/personal-finance/transactions", createTransaction);
        var transaction = await transactionResponse.Content.ReadFromJsonAsync<PersonalTransactionResponse>();

        // Act
        var reviewQueueBefore = await client.GetAsync("/personal-finance/classification/review-queue");
        var queuedItemsBefore = await reviewQueueBefore.Content.ReadFromJsonAsync<List<ClassificationReviewItemResponse>>();

        var overrideRequest = new OverrideTransactionClassificationRequest(
            "Groceries",
            "User corrected category",
            true,
            "SuperMart",
            200,
            "contains");

        var overrideResponse = await client.PostAsJsonAsync(
            $"/personal-finance/classification/review/{transaction!.PersonalTransactionId}/override",
            overrideRequest);

        var reviewQueueAfter = await client.GetAsync("/personal-finance/classification/review-queue");
        var queuedItemsAfter = await reviewQueueAfter.Content.ReadFromJsonAsync<List<ClassificationReviewItemResponse>>();

        var rulesResponse = await client.GetAsync("/personal-finance/classification/rules");
        var rules = await rulesResponse.Content.ReadFromJsonAsync<List<CategorisationRuleResponse>>();

        // Assert
        transactionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        reviewQueueBefore.StatusCode.Should().Be(HttpStatusCode.OK);
        queuedItemsBefore.Should().NotBeNull();
        queuedItemsBefore!.Should().ContainSingle(item => item.PersonalTransactionId == transaction.PersonalTransactionId);

        overrideResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var overridden = await overrideResponse.Content.ReadFromJsonAsync<ClassificationReviewItemResponse>();
        overridden.Should().NotBeNull();
        overridden!.Category.Should().Be("Groceries");
        overridden.ReviewStatus.Should().Be("Reviewed");

        reviewQueueAfter.StatusCode.Should().Be(HttpStatusCode.OK);
        queuedItemsAfter.Should().NotBeNull();
        queuedItemsAfter!.Should().NotContain(item => item.PersonalTransactionId == transaction.PersonalTransactionId);

        rulesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        rules.Should().NotBeNull();
        rules!.Should().ContainSingle(item => item.Pattern == "SuperMart" && item.CreatedFromUserCorrection);
    }

    [Fact]
    public async Task ClassificationReview_OverrideWithNullRuleMatchType_ReturnsValidationError()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser"));
        var account = await CreateAccountAsync(client, "Validation Account");

        var transactionResponse = await client.PostAsJsonAsync("/personal-finance/transactions", new CreateManualPersonalTransactionRequest(
            account.PersonalAccountId,
            DateTime.UtcNow,
            -30m,
            "USD",
            "Corner Shop",
            "Snacks",
            null,
            null,
            null));

        var transaction = await transactionResponse.Content.ReadFromJsonAsync<PersonalTransactionResponse>();

        var payload = """
        {
          "category": "Groceries",
          "notes": null,
          "createRuleFromCorrection": true,
          "rulePattern": "Corner Shop",
          "rulePriority": 100,
          "ruleMatchType": null
        }
        """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(
            $"/personal-finance/classification/review/{transaction!.PersonalTransactionId}/override",
            content);

        // Assert
        response.StatusCode.Should().Be((HttpStatusCode)422);
    }

    [Fact]
    public async Task ClassificationRules_CreateWithInvalidRegexPattern_ReturnsValidationError()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser"));

        var request = new CreateCategorisationRuleRequest(
            "(",
            "Groceries",
            100,
            "regex",
            false,
            null,
            null,
            null,
            "User");

        // Act
        var response = await client.PostAsJsonAsync("/personal-finance/classification/rules", request);

        // Assert
        response.StatusCode.Should().Be((HttpStatusCode)422);
    }

    [Fact]
    public async Task InsightsEndpoints_ReturnSummaryAndBreakdowns()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser"));
        var account = await CreateAccountAsync(client, "Insights Account");

        var now = DateTime.UtcNow;
        await client.PostAsJsonAsync("/personal-finance/transactions", new CreateManualPersonalTransactionRequest(
            account.PersonalAccountId,
            now.AddDays(-2),
            -120m,
            "USD",
            "Fresh Foods",
            "Weekly groceries",
            "Groceries",
            null,
            null));

        await client.PostAsJsonAsync("/personal-finance/transactions", new CreateManualPersonalTransactionRequest(
            account.PersonalAccountId,
            now.AddDays(-1),
            1500m,
            "USD",
            "Employer Ltd",
            "Salary",
            "Income",
            null,
            null));

        var query = $"?periodStart={Uri.EscapeDataString(now.AddDays(-10).ToString("O"))}&periodEnd={Uri.EscapeDataString(now.ToString("O"))}";

        // Act
        var summaryResponse = await client.GetAsync($"/personal-finance/insights/spending-summary{query}");
        var categoryResponse = await client.GetAsync($"/personal-finance/insights/category-breakdown{query}");
        var merchantResponse = await client.GetAsync($"/personal-finance/insights/merchant-breakdown{query}");
        var accountResponse = await client.GetAsync($"/personal-finance/insights/account-breakdown{query}");

        // Assert
        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await summaryResponse.Content.ReadFromJsonAsync<SpendingSummaryResponse>();
        summary.Should().NotBeNull();
        summary!.TotalIncome.Should().Be(1500m);
        summary.TotalExpense.Should().Be(120m);

        categoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await categoryResponse.Content.ReadFromJsonAsync<List<CategorySpendingItemResponse>>();
        categories.Should().NotBeNull();
        categories!.Should().Contain(item => item.Category == "Groceries");

        merchantResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var merchants = await merchantResponse.Content.ReadFromJsonAsync<List<MerchantSpendingItemResponse>>();
        merchants.Should().NotBeNull();
        merchants!.Should().Contain(item => item.Merchant == "Fresh Foods");

        accountResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var accounts = await accountResponse.Content.ReadFromJsonAsync<List<AccountSpendingItemResponse>>();
        accounts.Should().NotBeNull();
        accounts!.Should().Contain(item =>
            item.PersonalAccountId == account.PersonalAccountId
            && item.TotalAmount == 120m
            && item.TransactionCount == 1);
    }

    [Fact]
    public async Task InsightsEndpoints_ReturnValidationError_WhenPeriodRangeIsInvalid()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser"));
        var start = DateTime.UtcNow;
        var end = start.AddDays(-2);
        var query = $"?periodStart={Uri.EscapeDataString(start.ToString("O"))}&periodEnd={Uri.EscapeDataString(end.ToString("O"))}";

        var endpoints = new[]
        {
            "/personal-finance/insights/spending-summary",
            "/personal-finance/insights/category-breakdown",
            "/personal-finance/insights/merchant-breakdown",
            "/personal-finance/insights/account-breakdown"
        };

        // Act
        var responses = new List<HttpResponseMessage>();
        foreach (var endpoint in endpoints)
        {
            responses.Add(await client.GetAsync($"{endpoint}{query}"));
        }

        // Assert
        responses.Should().OnlyContain(response => response.StatusCode == (HttpStatusCode)422);
    }

    [Fact]
    public async Task SpendingSummaryEndpoint_ReturnsValidationError_WhenPeriodContainsMultipleCurrencies()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser"));
        var usdAccount = await CreateAccountAsync(client, "USD Account");

        var eurAccountResponse = await client.PostAsJsonAsync("/personal-finance/accounts", new CreatePersonalAccountRequest(
            "EUR Account",
            "Bank",
            "EUR",
            "Acme Bank",
            null,
            "Current",
            "2222"));
        eurAccountResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var eurAccount = await eurAccountResponse.Content.ReadFromJsonAsync<PersonalAccountResponse>();
        eurAccount.Should().NotBeNull();

        var now = DateTime.UtcNow;

        await client.PostAsJsonAsync("/personal-finance/transactions", new CreateManualPersonalTransactionRequest(
            usdAccount.PersonalAccountId,
            now.AddDays(-2),
            -120m,
            "USD",
            "Fresh Foods",
            "Groceries",
            "Groceries",
            null,
            null));

        await client.PostAsJsonAsync("/personal-finance/transactions", new CreateManualPersonalTransactionRequest(
            eurAccount!.PersonalAccountId,
            now.AddDays(-1),
            -80m,
            "EUR",
            "Metro",
            "Transit",
            "Transport",
            null,
            null));

        var query = $"?periodStart={Uri.EscapeDataString(now.AddDays(-10).ToString("O"))}&periodEnd={Uri.EscapeDataString(now.ToString("O"))}";

        // Act
        var response = await client.GetAsync($"/personal-finance/insights/spending-summary{query}");

        // Assert
        response.StatusCode.Should().Be((HttpStatusCode)422);
    }

    [Fact]
    public async Task NarrativeInsightsEndpoint_ReturnsInsightWithAiRunReference()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser"));
        var account = await CreateAccountAsync(client, "Narrative Account");
        var now = DateTime.UtcNow;

        await client.PostAsJsonAsync("/personal-finance/transactions", new CreateManualPersonalTransactionRequest(
            account.PersonalAccountId,
            now.AddDays(-1),
            -89.99m,
            "USD",
            "Bookstore",
            "Books purchase",
            "Education",
            null,
            null));

        var request = new GeneratePersonalSpendingNarrativeRequest(
            now.AddDays(-10),
            now,
            account.PersonalAccountId);

        // Act
        var response = await client.PostAsJsonAsync("/personal-finance/insights/narrative", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var insight = await response.Content.ReadFromJsonAsync<PersonalSpendingNarrativeInsightResponse>();
        insight.Should().NotBeNull();
        insight!.AiRunId.Should().NotBeEmpty();
        insight.InsightId.Should().NotBeEmpty();
        insight.SubjectType.Should().Be("PersonalSpendPeriod");
    }

    private static async Task<PersonalAccountResponse> CreateAccountAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/personal-finance/accounts", new CreatePersonalAccountRequest(
            name,
            "Bank",
            "USD",
            "Acme Bank",
            null,
            "Current",
            "1111"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var account = await response.Content.ReadFromJsonAsync<PersonalAccountResponse>();
        account.Should().NotBeNull();
        return account!;
    }
}
