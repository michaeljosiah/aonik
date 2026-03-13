using System.Net;
using System.Text;
using System.Text.Json;

using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Aonik.Application.Tests.PersonalFinance;

public class PlaidAccountLinkProviderGatewayTests
{
    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        public void EnqueueJsonResponse(object payload, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json")
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No fake HTTP response was queued.");
            }

            return _responses.Dequeue();
        }
    }

    private static PlaidAccountLinkProviderGateway CreateGateway(RecordingHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://sandbox.plaid.com/")
        };

        var options = Microsoft.Extensions.Options.Options.Create(new PlaidAccountLinkOptions
        {
            UseRealPlaidApi = true,
            BaseUrl = "https://sandbox.plaid.com",
            ClientId = "plaid-client-id",
            Secret = "plaid-secret",
            ClientName = "Payabo",
            Language = "en",
            Products = ["transactions"],
            CountryCodes = ["US"]
        });

        var dataProtectionProvider = DataProtectionProvider.Create("Aonik.PlaidTests");

        return new PlaidAccountLinkProviderGateway(
            httpClient,
            options,
            dataProtectionProvider,
            NullLogger<PlaidAccountLinkProviderGateway>.Instance);
    }

    private static string ProtectAccessToken(string value)
    {
        var provider = DataProtectionProvider.Create("Aonik.PlaidTests");
        var protector = provider.CreateProtector("Aonik.Finance.PlaidAccountLinks");
        return $"protected:{protector.Protect(value)}";
    }

    [Fact]
    public async Task CreateSessionAsync_Should_SendAndroidPackageName_WhenCreatingLinkToken()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        handler.EnqueueJsonResponse(new
        {
            link_token = "link-sandbox-token",
            expiration = DateTime.UtcNow.AddMinutes(30),
            request_id = "req-link-token"
        });
        var gateway = CreateGateway(handler);

        // Act
        var result = await gateway.CreateSessionAsync(
            new AccountLinkProviderSessionRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                null,
                null,
                "connect",
                "com.payabo.mobile",
                null,
                "US",
                "Payabo Android"));

        // Assert
        result.LaunchToken.Should().Be("link-sandbox-token");
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().EndWith("/link/token/create");
        handler.LastRequestBody.Should().Contain("\"android_package_name\":\"com.payabo.mobile\"");
        handler.LastRequestBody.Should().Contain("\"client_user_id\"");
    }

    [Fact]
    public async Task ExchangeSessionAsync_Should_ProtectAccessTokenAndMapAccounts()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        handler.EnqueueJsonResponse(new
        {
            access_token = "access-sandbox-token",
            item_id = "item-sandbox-123"
        });
        handler.EnqueueJsonResponse(new
        {
            item = new
            {
                item_id = "item-sandbox-123",
                institution_id = "ins_109508",
                consent_expiration_time = (DateTime?)null
            }
        });
        handler.EnqueueJsonResponse(new
        {
            accounts = new object[]
            {
                new
                {
                    account_id = "acc-checking-1",
                    name = "Primary Checking",
                    type = "depository",
                    subtype = "checking",
                    mask = "1842",
                    balances = new
                    {
                        iso_currency_code = "USD"
                    }
                },
                new
                {
                    account_id = "acc-savings-1",
                    name = "Rainy Day Savings",
                    type = "depository",
                    subtype = "savings",
                    mask = "8801",
                    balances = new
                    {
                        iso_currency_code = "USD"
                    }
                }
            }
        });
        handler.EnqueueJsonResponse(new
        {
            institution = new
            {
                institution_id = "ins_109508",
                name = "First Platypus Bank"
            }
        });

        var gateway = CreateGateway(handler);

        // Act
        var result = await gateway.ExchangeSessionAsync(
            new AccountLinkProviderExchangeRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                null,
                "link-sandbox-token",
                "public-sandbox-token",
                "connect"));

        // Assert
        result.ProviderConnectionReference.Should().Be("item-sandbox-123");
        result.SecretReference.Should().StartWith("protected:");
        result.InstitutionName.Should().Be("First Platypus Bank");
        result.Accounts.Should().HaveCount(2);
        result.Accounts[0].AccountType.Should().Be("bank");
        result.Accounts[0].AccountSubtype.Should().Be("current");
        result.Accounts[0].Last4.Should().Be("1842");
    }

    [Fact]
    public async Task SyncTransactionsAsync_Should_MapTransactionsAndRemovedReferences()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        handler.EnqueueJsonResponse(new
        {
            added = new object[]
            {
                new
                {
                    transaction_id = "txn-001",
                    account_id = "acc-checking-1",
                    amount = 12.34m,
                    iso_currency_code = "USD",
                    merchant_name = "Blue Bottle",
                    name = "Morning coffee",
                    date = DateTime.UtcNow.Date,
                    authorized_date = DateTime.UtcNow.Date,
                    pending = false,
                    personal_finance_category = new
                    {
                        primary = "FOOD_AND_DRINK"
                    }
                }
            },
            modified = new object[] { },
            removed = new object[]
            {
                new
                {
                    transaction_id = "txn-removed-1"
                }
            },
            next_cursor = "cursor-123",
            has_more = false
        });

        var gateway = CreateGateway(handler);

        // Act
        var result = await gateway.SyncTransactionsAsync(
            new AccountLinkProviderTransactionsSyncRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "item-sandbox-123",
                ProtectAccessToken("access-sandbox-token"),
                null));

        // Assert
        result.NextCursor.Should().Be("cursor-123");
        result.SyncStatus.Should().Be("TransactionsSyncComplete");
        result.Transactions.Should().HaveCount(1);
        result.Transactions[0].Amount.Should().Be(-12.34m);
        result.Transactions[0].Category.Should().Be("FOOD AND DRINK");
        result.RemovedTransactionReferences.Should().ContainSingle("txn-removed-1");
        handler.LastRequestBody.Should().Contain("\"access_token\"");
        handler.LastRequest!.RequestUri!.ToString().Should().EndWith("/transactions/sync");
    }
}
