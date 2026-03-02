using System.Net;
using System.Net.Http.Json;
using System.Text;

using FluentAssertions;

using Aonik.Finance.Contracts.Models.PersonalFinance;

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
        accounts!.Should().Contain(item => item.PersonalAccountId == account.PersonalAccountId);
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
